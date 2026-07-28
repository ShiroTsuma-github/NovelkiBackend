namespace Infrastructure.BookSearch;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

public sealed class BookSearchIndexBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<BookSearchIndexBackgroundService> logger) : BackgroundService
{
    private const string NotificationChannel = "book_search_index_changed";
    private static readonly TimeSpan RecoveryPollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = configuration.GetConnectionString("DB");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogError("Book search index worker cannot start because connection string 'DB' is missing.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ListenAndProcessAsync(connectionString, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Book search index listener failed. Reconnecting.");
                await Task.Delay(ReconnectDelay, timeProvider, stoppingToken);
            }
        }
    }

    private async Task ListenAndProcessAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using (var command = new NpgsqlCommand($"LISTEN {NotificationChannel}", connection))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            await ProcessAvailableAsync(cancellationToken);
            await connection.WaitAsync(RecoveryPollInterval, cancellationToken);
        }
    }

    private async Task ProcessAvailableAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<BookSearchIndexQueueProcessor>();
            var processed = await processor.ProcessBatchAsync(cancellationToken);
            if (processed < BookSearchIndexQueueProcessor.BatchSize)
            {
                return;
            }
        }
    }
}
