namespace Infrastructure.BookSearch;

using Microsoft.Extensions.Logging;

public sealed class BookSearchIndexUpdater(
    ApplicationDbContext context,
    ILogger<BookSearchIndexUpdater> logger)
{
    public async Task RefreshAsync(Guid bookId, CancellationToken cancellationToken)
    {
        if (!context.Database.IsNpgsql())
        {
            return;
        }

        try
        {
            var enqueuedAt = await context.BookSearchIndexQueueItems
                .AsNoTracking()
                .Where(item => item.BookId == bookId)
                .Select(item => (DateTimeOffset?)item.EnqueuedAt)
                .SingleOrDefaultAsync(cancellationToken);

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT refresh_book_search_index({bookId})",
                cancellationToken);

            if (enqueuedAt.HasValue)
            {
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    DELETE FROM "BookSearchIndexQueueItems"
                    WHERE "BookId" = {bookId}
                      AND "EnqueuedAt" = {enqueuedAt.Value}
                      AND "LeaseId" IS NULL
                    """, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Synchronous book search indexing failed for {BookId}; the persistent queue will retry it.",
                bookId);
        }
    }
}
