namespace Infrastructure.BookCovers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public sealed class StorageCleanupBackgroundService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<StorageCleanupBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<StorageCleanupQueueProcessor>();
                await processor.ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Storage cleanup batch failed.");
            }

            await Task.Delay(PollInterval, timeProvider, stoppingToken);
        }
    }
}
