namespace Infrastructure.BookCovers;

using Microsoft.Extensions.Logging;

public sealed class StorageCleanupQueueProcessor(
    ApplicationDbContext context,
    IBookCoverStorage storage,
    TimeProvider timeProvider,
    ILogger<StorageCleanupQueueProcessor> logger)
{
    private const int BatchSize = 20;
    private const int LastErrorMaxLength = 2000;
    private static readonly TimeSpan MaximumBackoff = TimeSpan.FromHours(24);

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var items = await context.StorageCleanupQueueItems
            .Where(item => item.NextAttemptAt <= now)
            .OrderBy(item => item.NextAttemptAt)
            .ThenBy(item => item.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            try
            {
                await storage.DeleteIfExistsAsync(item.StoragePath, cancellationToken);
                context.StorageCleanupQueueItems.Remove(item);
            }
            catch (EntityNotFoundException<BookCover, Guid>)
            {
                context.StorageCleanupQueueItems.Remove(item);
            }
            catch (Exception exception)
            {
                item.AttemptCount++;
                item.NextAttemptAt = now + CalculateBackoff(item.AttemptCount);
                item.LastError = Truncate(exception.Message, LastErrorMaxLength);
                logger.LogWarning(exception,
                    "Storage cleanup failed for {StoragePath}. Attempt={AttemptCount} NextAttemptAt={NextAttemptAt}",
                    item.StoragePath, item.AttemptCount, item.NextAttemptAt);
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        return items.Count;
    }

    internal static TimeSpan CalculateBackoff(int attemptCount)
    {
        var exponent = Math.Clamp(attemptCount - 1, 0, 20);
        var delay = TimeSpan.FromMinutes(Math.Pow(2, exponent));
        return delay <= MaximumBackoff ? delay : MaximumBackoff;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
