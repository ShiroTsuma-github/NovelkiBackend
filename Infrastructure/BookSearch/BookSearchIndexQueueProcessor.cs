namespace Infrastructure.BookSearch;

using Microsoft.Extensions.Logging;

public sealed class BookSearchIndexQueueProcessor(
    ApplicationDbContext context,
    IBookListCacheInvalidator cacheInvalidator,
    TimeProvider timeProvider,
    ILogger<BookSearchIndexQueueProcessor> logger)
{
    internal const int BatchSize = 50;
    private const int LastErrorMaxLength = 2000;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MaximumBackoff = TimeSpan.FromMinutes(10);

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        if (!context.Database.IsNpgsql())
        {
            return 0;
        }

        var leaseId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        var leaseUntil = now + LeaseDuration;

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "BookSearchIndexQueueItems" AS queue
            SET "LeaseId" = {leaseId},
                "LeaseUntil" = {leaseUntil}
            WHERE queue."BookId" IN (
                SELECT candidate."BookId"
                FROM "BookSearchIndexQueueItems" AS candidate
                WHERE candidate."NextAttemptAt" <= {now}
                  AND (candidate."LeaseUntil" IS NULL OR candidate."LeaseUntil" <= {now})
                ORDER BY candidate."NextAttemptAt", candidate."EnqueuedAt", candidate."BookId"
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
            )
            """, cancellationToken);

        var items = await context.BookSearchIndexQueueItems
            .AsNoTracking()
            .Where(item => item.LeaseId == leaseId)
            .OrderBy(item => item.NextAttemptAt)
            .ThenBy(item => item.EnqueuedAt)
            .ThenBy(item => item.BookId)
            .ToArrayAsync(cancellationToken);

        if (items.Length == 0)
        {
            return 0;
        }

        try
        {
            var bookIds = items.Select(item => item.BookId).ToArray();
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT refresh_book_search_index(requested."BookId")
                FROM unnest({bookIds}) AS requested("BookId")
                """, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            foreach (var item in items)
            {
                await MarkFailedAsync(item, leaseId, exception, cancellationToken);
            }

            return items.Length;
        }

        foreach (var item in items)
        {
            await CompleteItemAsync(item, leaseId, cancellationToken);
        }

        return items.Length;
    }

    private async Task CompleteItemAsync(
        BookSearchIndexQueueItem item,
        Guid leaseId,
        CancellationToken cancellationToken)
    {
        try
        {
            var ownerId = await context.Books
                .Where(book => book.Id == item.BookId)
                .Select(book => (Guid?)book.OwnerId)
                .SingleOrDefaultAsync(cancellationToken);
            if (ownerId.HasValue)
            {
                await cacheInvalidator.InvalidateBooksAsync(ownerId.Value, cancellationToken);
            }

            var removed = await context.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM "BookSearchIndexQueueItems"
                WHERE "BookId" = {item.BookId}
                  AND "LeaseId" = {leaseId}
                  AND "EnqueuedAt" = {item.EnqueuedAt}
                """, cancellationToken);

            if (removed == 0)
            {
                await ReleaseLeaseAsync(item.BookId, leaseId, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await MarkFailedAsync(item, leaseId, exception, cancellationToken);
        }
    }

    private async Task MarkFailedAsync(
        BookSearchIndexQueueItem item,
        Guid leaseId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var attemptCount = item.AttemptCount + 1;
        var nextAttemptAt = timeProvider.GetUtcNow() + CalculateBackoff(attemptCount);
        var lastError = Truncate(exception.Message, LastErrorMaxLength);
        var updated = await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "BookSearchIndexQueueItems"
            SET "AttemptCount" = {attemptCount},
                "NextAttemptAt" = {nextAttemptAt},
                "LastError" = {lastError},
                "LeaseId" = NULL,
                "LeaseUntil" = NULL
            WHERE "BookId" = {item.BookId}
              AND "LeaseId" = {leaseId}
              AND "EnqueuedAt" = {item.EnqueuedAt}
            """, cancellationToken);
        if (updated == 0)
        {
            await ReleaseLeaseAsync(item.BookId, leaseId, cancellationToken);
        }

        logger.LogWarning(
            exception,
            "Book search indexing failed for {BookId}. Attempt={AttemptCount} NextAttemptAt={NextAttemptAt}",
            item.BookId,
            attemptCount,
            nextAttemptAt);
    }

    private Task ReleaseLeaseAsync(Guid bookId, Guid leaseId, CancellationToken cancellationToken)
    {
        return context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "BookSearchIndexQueueItems"
            SET "LeaseId" = NULL,
                "LeaseUntil" = NULL
            WHERE "BookId" = {bookId}
              AND "LeaseId" = {leaseId}
            """, cancellationToken);
    }

    internal static TimeSpan CalculateBackoff(int attemptCount)
    {
        var exponent = Math.Clamp(attemptCount - 1, 0, 20);
        var delay = TimeSpan.FromSeconds(Math.Pow(2, exponent));
        return delay <= MaximumBackoff ? delay : MaximumBackoff;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
