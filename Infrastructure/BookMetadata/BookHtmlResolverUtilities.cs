namespace Infrastructure.BookMetadata;

using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

internal static partial class BookHtmlResolverUtilities
{
    public static string? ReadText(IElement? element)
    {
        if (element == null)
        {
            return null;
        }

        var value = NormalizeInlineText(element.TextContent);
        return value.Length == 0 ? null : value;
    }

    public static string? ReadMultilineText(IElement? element)
    {
        if (element == null)
        {
            return null;
        }

        return HtmlToMultilineText(element.InnerHtml, element.Owner);
    }

    public static string? HtmlToMultilineText(string? html, IDocument? document)
    {
        if (string.IsNullOrWhiteSpace(html) || document == null)
        {
            return null;
        }

        var withLines = BlockBreakRegex().Replace(html, "\n");
        var container = document.CreateElement("div");
        container.InnerHtml = withLines;
        var lines = container.TextContent
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries)
            .Select(NormalizeInlineText)
            .Where(line => line.Length > 0);
        var result = string.Join("\n", lines);
        return result.Length == 0 ? null : result;
    }

    public static IReadOnlyCollection<string> ReadDistinctTexts(IEnumerable<IElement> elements)
    {
        return elements.Select(ReadText)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string? ReadCanonicalUrl(IDocument document)
    {
        return document.QuerySelector("link[rel~='canonical']")?.GetAttribute("href")
               ?? ReadMeta(document, "og:url");
    }

    public static string? ReadMeta(IDocument document, string property)
    {
        return document.QuerySelector($"meta[property='{property}']")?.GetAttribute("content")
               ?? document.QuerySelector($"meta[name='{property}']")?.GetAttribute("content");
    }

    public static string? NormalizeHttpUrl(string? raw, string? baseUrl = null)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            value = $"https:{value}";
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            return IsHttp(absolute) ? absolute.ToString() : null;
        }

        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) &&
            Uri.TryCreate(baseUri, value, out var resolved) && IsHttp(resolved))
        {
            return resolved.ToString();
        }

        return null;
    }

    public static bool HasHostAndPath(string? url, string host, string pathPrefix)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               (uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith($".{host}", StringComparison.OrdinalIgnoreCase)) &&
               uri.AbsolutePath.StartsWith(pathPrefix, StringComparison.OrdinalIgnoreCase);
    }

    public static string? ReadBookJsonLdString(IDocument document, string property)
    {
        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            try
            {
                using var json = JsonDocument.Parse(script.TextContent);
                var book = FindBook(json.RootElement);
                if (book is { } value && value.TryGetProperty(property, out var field))
                {
                    var text = ReadJsonString(field);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text.Trim();
                    }
                }
            }
            catch (JsonException)
            {
                // Some providers emit invalid JSON-LD. DOM and OpenGraph remain authoritative fallbacks.
            }
        }

        return null;
    }

    public static string? ReadBookJsonLdAuthor(IDocument document)
    {
        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            try
            {
                using var json = JsonDocument.Parse(script.TextContent);
                var book = FindBook(json.RootElement);
                if (book is not { } value || !value.TryGetProperty("author", out var author))
                {
                    continue;
                }

                if (author.ValueKind == JsonValueKind.Array)
                {
                    author = author.EnumerateArray().FirstOrDefault();
                }

                if (author.ValueKind == JsonValueKind.Object && author.TryGetProperty("name", out var name))
                {
                    var text = ReadJsonString(name);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text.Trim();
                    }
                }
            }
            catch (JsonException)
            {
                // See ReadBookJsonLdString.
            }
        }

        return null;
    }

    private static JsonElement? FindBook(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("@type", out var type) && IsBookType(type))
            {
                return element;
            }

            if (element.TryGetProperty("@graph", out var graph))
            {
                var graphBook = FindBook(graph);
                if (graphBook != null)
                {
                    return graphBook;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var book = FindBook(item);
                if (book != null)
                {
                    return book;
                }
            }
        }

        return null;
    }

    private static bool IsBookType(JsonElement type)
    {
        return type.ValueKind switch
        {
            JsonValueKind.String => string.Equals(type.GetString(), "Book", StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Array => type.EnumerateArray().Any(IsBookType),
            _ => false
        };
    }

    private static string? ReadJsonString(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray().Select(ReadJsonString)
                .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
        }

        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("url", out var url))
        {
            return ReadJsonString(url);
        }

        return null;
    }

    private static string NormalizeInlineText(string value)
    {
        return InlineWhitespaceRegex().Replace(value.Replace('\u00a0', ' '), " ").Trim();
    }

    private static bool IsHttp(Uri uri)
    {
        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
               uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"(?is)<\s*(?:br\s*/?|/p|/div|/li|/h[1-6])\s*>")]
    private static partial Regex BlockBreakRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex InlineWhitespaceRegex();
}
