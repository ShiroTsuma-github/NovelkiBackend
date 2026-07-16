namespace Api.Observability;

using System.Data;
using Infrastructure.Observability;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;

public static class ObservabilityExtensions
{
    private const string OtlpEndpointVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string DeploymentEnvironmentAttribute = "deployment.environment";

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

            var endpoint = context.Configuration[OtlpEndpointVariable];
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
                        [DeploymentEnvironmentAttribute] = context.HostingEnvironment.EnvironmentName
                    };
                });
            }
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(GetServiceName(builder.Configuration))
                .AddAttributes(new Dictionary<string, object>
                {
                    [DeploymentEnvironmentAttribute] = builder.Environment.EnvironmentName
                }))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(NovelkiTelemetry.ActivitySourceName)
                    .AddSource(InfrastructureTelemetry.ActivitySourceName)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = context => !IsHealthCheckPath(context.Request.Path);
                    })
                    .AddEntityFrameworkCoreInstrumentation(options =>
                    {
                        options.EnrichWithIDbCommand = (activity, command) =>
                        {
                            activity.DisplayName = $"db {command.CommandType.ToString().ToLowerInvariant()}";
                            activity.SetTag("db.system", GetDbSystem(command));
                            activity.SetTag("db.operation.name", command.CommandType.ToString());

                            if (command.CommandType == CommandType.Text)
                            {
                                activity.SetTag("db.statement", command.CommandText);
                            }
                            else if (!string.IsNullOrWhiteSpace(command.CommandText))
                            {
                                activity.SetTag("db.statement.name", command.CommandText);
                            }
                        };
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
        var endpoint = configuration[OtlpEndpointVariable];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return;
        }

        tracing.AddOtlpExporter(options => { options.Endpoint = new Uri(endpoint); });
    }

    private static void AddOtlpMetricsExporter(MeterProviderBuilder metrics, IConfiguration configuration)
    {
        var endpoint = configuration[OtlpEndpointVariable];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return;
        }

        metrics.AddOtlpExporter(options => { options.Endpoint = new Uri(endpoint); });
    }

    private static string GetDbSystem(IDbCommand command)
    {
        var provider = command.GetType().Namespace ?? string.Empty;
        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return "postgresql";
        }

        if (provider.Contains("SqlClient", StringComparison.OrdinalIgnoreCase))
        {
            return "mssql";
        }

        if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return "sqlite";
        }

        return "database";
    }

    private static string GetServiceName(IConfiguration configuration)
    {
        return configuration["OTEL_SERVICE_NAME"] ?? "novelki-api";
    }

    public static bool IsHealthCheckPath(PathString path)
    {
        return path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
    }
}
