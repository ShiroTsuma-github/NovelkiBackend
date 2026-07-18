namespace Api;

using System.IO.Compression;
using System.Net.Mime;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;

internal static class DependencyInjection
{
    public const string FrontendCorsPolicy = "Frontend";
    public const string AccountAuthRateLimitPolicy = "account-auth";
    public const string ExpensiveUserActionRateLimitPolicy = "expensive-user-action";
    public const string FullBackupRateLimitPolicy = "full-backup";

    private const string CorsAllowedOriginsKey = "Cors:AllowedOrigins";
    private const string AccountPermitLimitKey = "RateLimiting:Account:PermitLimit";
    private const string AccountWindowSecondsKey = "RateLimiting:Account:WindowSeconds";
    private const string ExpensivePermitLimitKey = "RateLimiting:Expensive:PermitLimit";
    private const string ExpensiveWindowSecondsKey = "RateLimiting:Expensive:WindowSeconds";
    private const string ProblemJsonMediaType = "application/problem+json";
    private const string AdminRateLimitPartition = "admin";
    private const string UnknownRateLimitPartition = "unknown";

    public static void AddWebServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddControllers();
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
            [
                MediaTypeNames.Application.Json,
                ProblemJsonMediaType
            ]);
        });
        builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Fastest;
        });
        builder.Services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Fastest;
        });
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(FrontendCorsPolicy, policy =>
            {
                var origins = builder.Configuration.GetSection(CorsAllowedOriginsKey).Get<string[]>() ??
                              Array.Empty<string>();
                if (origins.Length > 0)
                {
                    policy.WithOrigins(origins)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                }
                else if (builder.Environment.IsDevelopment())
                {
                    policy.AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                }
                else
                {
                    policy.SetIsOriginAllowed(_ => false)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                }
            });
        });
        builder.Services.AddRateLimiter(options =>
        {
            var accountPermitLimit = builder.Configuration.GetValue<int?>(AccountPermitLimitKey) ?? 10;
            var accountWindowSeconds = builder.Configuration.GetValue<int?>(AccountWindowSecondsKey) ?? 60;
            var expensivePermitLimit = builder.Configuration.GetValue<int?>(ExpensivePermitLimitKey) ?? 20;
            var expensiveWindowSeconds =
                builder.Configuration.GetValue<int?>(ExpensiveWindowSecondsKey) ?? 60;

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                TimeSpan? retryAfterValue = null;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    retryAfterValue = retryAfter;
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(retryAfter.TotalSeconds).ToString("0");
                }

                var detail = retryAfterValue.HasValue
                    ? $"Too many requests. Try again in {FormatRetryAfter(retryAfterValue.Value)}."
                    : "Too many requests. Please retry later.";
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("RateLimiting");
                logger.LogWarning(
                    "Rate limit exceeded for {Method} {Path}. Detail={Detail} TraceId={TraceId}",
                    context.HttpContext.Request.Method,
                    context.HttpContext.Request.Path,
                    detail,
                    context.HttpContext.TraceIdentifier);

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        type = "RateLimitExceeded",
                        title = "Too Many Requests",
                        status = StatusCodes.Status429TooManyRequests,
                        detail,
                        instance = context.HttpContext.Request.Path.Value,
                        errors = (object?)null
                    }, cancellationToken);
            };

            options.AddPolicy(AccountAuthRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetRemoteIpPartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = accountPermitLimit,
                        Window = TimeSpan.FromSeconds(accountWindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy(ExpensiveUserActionRateLimitPolicy, httpContext =>
            {
                if (httpContext.User.IsInRole(AuthorizationRoles.Admin))
                {
                    return RateLimitPartition.GetNoLimiter(AdminRateLimitPartition);
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    GetAuthenticatedUserPartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = expensivePermitLimit,
                        Window = TimeSpan.FromSeconds(expensiveWindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            options.AddPolicy(FullBackupRateLimitPolicy, httpContext =>
            {
                if (httpContext.User.IsInRole(AuthorizationRoles.Admin))
                {
                    return RateLimitPartition.GetNoLimiter(AdminRateLimitPartition);
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    $"{GetAuthenticatedUserPartitionKey(httpContext)}:{httpContext.Request.Path.Value?.ToLowerInvariant()}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 1, Window = TimeSpan.FromMinutes(30), QueueLimit = 0, AutoReplenishment = true
                    });
            });
        });
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc(ApiDocumentation.Version,
                new OpenApiInfo { Title = ApiDocumentation.Title, Version = ApiDocumentation.Version });

            c.AddSecurityDefinition(AuthenticationSchemes.Bearer, new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme.\r\n\r\n" +
                              "Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\n" +
                              "Example: \"Bearer abcdef12345\"",
                Name = HeaderNames.Authorization,
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = AuthenticationSchemes.Bearer
            });

            c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference(AuthenticationSchemes.Bearer, document), [] }
            });
            c.CustomSchemaIds(type =>
            {
                var name = type.Name;
                if (name.EndsWith("Command"))
                {
                    return name.Substring(0, name.Length - "Command".Length);
                }

                if (name.EndsWith("Query"))
                {
                    return name.Substring(0, name.Length - "Query".Length);
                }

                if (name.EndsWith("Request"))
                {
                    return name.Substring(0, name.Length - "Request".Length);
                }

                return name;
            });
        });
    }

    private static string GetRemoteIpPartitionKey(HttpContext httpContext)
    {
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? UnknownRateLimitPartition;
    }

    private static string GetAuthenticatedUserPartitionKey(HttpContext httpContext)
    {
        return httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? GetRemoteIpPartitionKey(httpContext);
    }

    private static string FormatRetryAfter(TimeSpan retryAfter)
    {
        if (retryAfter.TotalMinutes >= 1)
        {
            var minutes = (int)Math.Ceiling(retryAfter.TotalMinutes);
            return $"{minutes} minute{(minutes == 1 ? string.Empty : "s")}";
        }

        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        return $"{seconds} second{(seconds == 1 ? string.Empty : "s")}";
    }
}
