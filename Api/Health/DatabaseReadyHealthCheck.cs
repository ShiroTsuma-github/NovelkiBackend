namespace Api.Health;

using Infrastructure.Contexts;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public sealed class DatabaseReadyHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _context;

    public DatabaseReadyHealthCheck(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        bool canConnect = await _context.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? HealthCheckResult.Healthy("Database connection is ready.")
            : HealthCheckResult.Unhealthy("Database connection is not ready.");
    }
}
