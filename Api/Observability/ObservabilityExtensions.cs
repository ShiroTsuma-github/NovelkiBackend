namespace Api.Observability;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;

public static class ObservabilityExtensions
{
    public static void AddObservability(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, logger) =>
        {
            logger
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "NovelkiBackend")
                .Enrich.WithProperty("ServiceName", GetServiceName(context.Configuration));

            var endpoint = context.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                logger.WriteTo.OpenTelemetry(options =>
                {
                    options.Endpoint = endpoint;
                    options.Protocol = OtlpProtocol.Grpc;
                    options.RestrictedToMinimumLevel = LogEventLevel.Information;
                    options.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = GetServiceName(context.Configuration),
                        ["deployment.environment"] = context.HostingEnvironment.EnvironmentName
                    };
                });
            }
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(GetServiceName(builder.Configuration))
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = builder.Environment.EnvironmentName
                }))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(NovelkiTelemetry.ActivitySourceName)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = context => !IsHealthCheckPath(context.Request.Path);
                    })
                    .AddHttpClientInstrumentation();

                AddOtlpTracingExporter(tracing, builder.Configuration);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(NovelkiTelemetry.MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                AddOtlpMetricsExporter(metrics, builder.Configuration);
            });
    }

    private static void AddOtlpTracingExporter(TracerProviderBuilder tracing, IConfiguration configuration)
    {
        var endpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return;
        }

        tracing.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(endpoint);
        });
    }

    private static void AddOtlpMetricsExporter(MeterProviderBuilder metrics, IConfiguration configuration)
    {
        var endpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return;
        }

        metrics.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(endpoint);
        });
    }

    private static string GetServiceName(IConfiguration configuration) =>
        configuration["OTEL_SERVICE_NAME"] ?? "novelki-api";

    public static bool IsHealthCheckPath(PathString path) =>
        path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
}
