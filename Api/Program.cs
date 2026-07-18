using System.Diagnostics;
using System.Security.Claims;
using Api;
using Api.Health;
using Api.Observability;
using Application;
using Infrastructure;
using Infrastructure.Middleware;
using Serilog.Events;
using DependencyInjection = Api.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

builder.AddObservability();
builder.AddWebServices();
builder.AddApplicationServices();
builder.AddInfrastructureServices();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadyHealthCheck>("database");

var app = builder.Build();

await app.ApplyDatabaseMigrationsAsync();

app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (httpContext, _, exception) =>
    {
        if (exception != null || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            return LogEventLevel.Error;
        }

        return ObservabilityExtensions.IsHealthCheckPath(httpContext.Request.Path)
            ? LogEventLevel.Verbose
            : LogEventLevel.Information;
    };

    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? string.Empty);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("RequestId", httpContext.TraceIdentifier);
        diagnosticContext.Set("TraceId", Activity.Current?.TraceId.ToString() ?? string.Empty);
        diagnosticContext.Set("SpanId", Activity.Current?.SpanId.ToString() ?? string.Empty);
        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            diagnosticContext.Set("UserId", userId);
        }
    };
});

app.UseResponseCompression();
app.UseSecurityHeaders();
app.UseErrorHandlingMiddleware();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint(ApiDocumentation.SwaggerEndpoint, ApiDocumentation.DisplayName);
        app.Map("/", () => Results.Redirect("/swagger"));
    });
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseCors(DependencyInjection.FrontendCorsPolicy);

app.UseAuthentication();
app.UseAccountBlock();
app.UseSuspiciousRequestDetection();
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthChecks("/health/ready");
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));
app.MapControllers();

app.Run();

public partial class Program;
