namespace Infrastructure.Middleware;

using System.Buffers;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Services;

public sealed class SuspiciousRequestMiddleware
{
    private const int MaxInspectedBodyBytes = 256 * 1024;

    private const RegexOptions SafeRegexOptions =
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking |
        RegexOptions.Singleline;

    private static readonly Regex SqlUnionRegex = new(
        @"\bunion\s+(?:all\s+)?select\b.{0,256}(?:\bfrom\b|--|#|/\*)",
        SafeRegexOptions, TimeSpan.FromMilliseconds(100));

    private static readonly Regex SqlTautologyRegex = new(
        """['"]\s*(?:or|and)\s+(?:\d+\s*=\s*\d+|['"][^'"]{0,64}['"]\s*=\s*['"][^'"]{0,64}['"])[\s;]*(?:--|#|/\*)""",
        SafeRegexOptions, TimeSpan.FromMilliseconds(100));

    private static readonly Regex SqlStackedStatementRegex = new(
        @";\s*(?:drop|truncate|alter)\s+(?:table|database|schema)\b",
        SafeRegexOptions, TimeSpan.FromMilliseconds(100));

    private static readonly Regex SqlTimeBasedRegex = new(
        """(?:\bselect\s+(?:pg_sleep|sleep|benchmark)\s*\(|\bwaitfor\s+delay\s+['"]|\bbenchmark\s*\(\s*\d+\s*,)""",
        SafeRegexOptions, TimeSpan.FromMilliseconds(100));

    private static readonly Regex SqlDestructiveDmlRegex = new(
        @";\s*(?:delete\s+from\s+\S+\s+where\b|update\s+\S+\s+set\b|insert\s+into\s+\S+\s*\()",
        SafeRegexOptions, TimeSpan.FromMilliseconds(100));

    private static readonly Regex SqlSystemAccessRegex = new(
        @"(?:\bxp_cmdshell\b|\bcopy\b.{0,128}\bto\s+program\b|\bload_file\s*\(|\binto\s+outfile\b)",
        SafeRegexOptions, TimeSpan.FromMilliseconds(100));

    private static readonly Regex ScriptTagRegex = new(
        @"<\s*/?\s*(?:script|iframe|object|embed)\b",
        SafeRegexOptions, TimeSpan.FromMilliseconds(100));

    private static readonly Regex ScriptEventHandlerRegex = new(
        @"<\s*[a-z][a-z0-9:-]{0,30}\b[^>]{0,512}\bon(?:error|load|toggle|focus|click|mouseover|animationstart|begin)\s*=",
        SafeRegexOptions, TimeSpan.FromMilliseconds(100));

    private static readonly Regex ScriptProtocolRegex = new(
        @"\b(?:javascript\s*:|vbscript\s*:|data\s*:\s*text/html\b)",
        SafeRegexOptions, TimeSpan.FromMilliseconds(100));

    private readonly RequestDelegate _next;

    public SuspiciousRequestMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IUser user, AccountAbuseGuard abuseGuard)
    {
        if (!user.IsAuthenticated || AccountAbuseGuard.IsAdmin(user))
        {
            await _next(context);
            return;
        }

        var queryValues = GetQueryValues(context.Request);
        var reasonCode = DetectPathTraversal(context.Request);
        reasonCode ??= DetectInjection([GetRawPath(context.Request)]);
        reasonCode ??= DetectTraversalPayload(queryValues);
        reasonCode ??= DetectInjection(queryValues);
        if (reasonCode == null)
        {
            reasonCode = DetectInjection(await GetBodyValuesAsync(context.Request, context.RequestAborted));
        }

        if (reasonCode != null)
        {
            var blockedUntil = await abuseGuard.BlockAsync(user, reasonCode, CancellationToken.None);
            throw new AccountTemporarilyBlockedException(blockedUntil);
        }

        await _next(context);
    }

    private static string? DetectPathTraversal(HttpRequest request)
    {
        var normalized = NormalizeRepeatedly(GetRawPath(request)).Replace('\\', '/');
        if (normalized.IndexOf('\0') >= 0 || normalized.Split('/').Any(segment => segment == "..") ||
            normalized.Equals("/etc/passwd", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("/proc/self/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("/windows/system32/", StringComparison.OrdinalIgnoreCase))
        {
            return "url-path-traversal";
        }

        return null;
    }

    private static string GetRawPath(HttpRequest request)
    {
        var rawTarget = request.HttpContext.Features.Get<IHttpRequestFeature>()?.RawTarget;
        return !string.IsNullOrEmpty(rawTarget)
            ? rawTarget.Split('?', 2)[0]
            : request.Path.Value ?? string.Empty;
    }

    private static IEnumerable<string> GetQueryValues(HttpRequest request)
    {
        try
        {
            return request.Query.SelectMany(pair => pair.Value.Select(value => value ?? string.Empty)).ToArray();
        }
        catch (BadHttpRequestException)
        {
            return [];
        }
    }

    private static async Task<IEnumerable<string>> GetBodyValuesAsync(HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Body == Stream.Null || request.ContentLength == 0 ||
            request.ContentLength > MaxInspectedBodyBytes || string.IsNullOrWhiteSpace(request.ContentType))
        {
            return [];
        }

        var mediaType = request.ContentType.Split(';', 2)[0].Trim();
        if (!mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) &&
            !mediaType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        request.EnableBuffering();
        var rented = ArrayPool<byte>.Shared.Rent(MaxInspectedBodyBytes + 1);
        try
        {
            var total = 0;
            while (total <= MaxInspectedBodyBytes)
            {
                var read = await request.Body.ReadAsync(
                    rented.AsMemory(total, MaxInspectedBodyBytes + 1 - total), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            request.Body.Position = 0;
            if (total == 0 || total > MaxInspectedBodyBytes)
            {
                return [];
            }

            var body = Encoding.UTF8.GetString(rented, 0, total);
            return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
                ? GetJsonStringValues(body)
                : GetFormValues(body);
        }
        finally
        {
            if (request.Body.CanSeek)
            {
                request.Body.Position = 0;
            }

            ArrayPool<byte>.Shared.Return(rented, true);
        }
    }

    private static IEnumerable<string> GetJsonStringValues(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 32 });
            var values = new List<string>();
            CollectJsonStringValues(document.RootElement, values);
            return values;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void CollectJsonStringValues(JsonElement element, ICollection<string> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                values.Add(element.GetString() ?? string.Empty);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectJsonStringValues(item, values);
                }

                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    CollectJsonStringValues(property.Value, values);
                }

                break;
        }
    }

    private static IEnumerable<string> GetFormValues(string body)
    {
        return body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Select(parts => (parts.Length == 2 ? parts[1] : parts[0]).Replace('+', ' '))
            .ToArray();
    }

    private static string? DetectTraversalPayload(IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            var normalized = NormalizeRepeatedly(value).Replace('\\', '/');
            var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var traversalSegments = segments.Count(segment => segment == "..");
            if (traversalSegments >= 2 || (traversalSegments == 1 && TargetsSensitivePath(segments)))
            {
                return "url-path-traversal";
            }

            if (normalized.StartsWith("/etc/", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("/proc/", StringComparison.OrdinalIgnoreCase) ||
                (normalized.Length >= 3 && char.IsAsciiLetter(normalized[0]) && normalized[1] == ':' &&
                 normalized[2] == '/'))
            {
                return "url-path-traversal";
            }
        }

        return null;
    }

    private static bool TargetsSensitivePath(IReadOnlyList<string> segments)
    {
        var traversalIndex = segments.ToList().FindIndex(segment => segment == "..");
        var remainingPath = traversalIndex >= 0
            ? string.Join('/', segments.Skip(traversalIndex + 1))
            : string.Empty;
        return remainingPath.StartsWith("etc/", StringComparison.OrdinalIgnoreCase) ||
               remainingPath.StartsWith("proc/", StringComparison.OrdinalIgnoreCase) ||
               remainingPath.StartsWith("windows/", StringComparison.OrdinalIgnoreCase) ||
               (remainingPath.Length >= 3 && char.IsAsciiLetter(remainingPath[0]) && remainingPath[1] == ':' &&
                remainingPath[2] == '/') ||
               remainingPath.Equals(".env", StringComparison.OrdinalIgnoreCase) ||
               remainingPath.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase) ||
               remainingPath.EndsWith("/appsettings.json", StringComparison.OrdinalIgnoreCase);
    }

    private static string? DetectInjection(IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            var normalized = NormalizeRepeatedly(value);
            if (SqlUnionRegex.IsMatch(normalized) || SqlTautologyRegex.IsMatch(normalized) ||
                SqlStackedStatementRegex.IsMatch(normalized) || SqlTimeBasedRegex.IsMatch(normalized) ||
                SqlDestructiveDmlRegex.IsMatch(normalized) || SqlSystemAccessRegex.IsMatch(normalized))
            {
                return "sql-injection-payload";
            }

            if (ScriptTagRegex.IsMatch(normalized) || ScriptEventHandlerRegex.IsMatch(normalized) ||
                ScriptProtocolRegex.IsMatch(normalized))
            {
                return "script-injection-payload";
            }
        }

        return null;
    }

    private static string NormalizeRepeatedly(string value)
    {
        var normalized = value;
        for (var iteration = 0; iteration < 3; iteration++)
        {
            string decoded;
            try
            {
                decoded = Uri.UnescapeDataString(normalized.Replace('+', ' '));
            }
            catch (UriFormatException)
            {
                break;
            }

            decoded = WebUtility.HtmlDecode(decoded);
            if (decoded == normalized)
            {
                break;
            }

            normalized = decoded;
        }

        return normalized;
    }
}

public static class SuspiciousRequestMiddlewareExtensions
{
    public static IApplicationBuilder UseSuspiciousRequestDetection(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SuspiciousRequestMiddleware>();
    }
}
