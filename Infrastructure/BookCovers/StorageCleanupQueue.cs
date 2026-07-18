namespace Infrastructure.BookCovers;

public sealed class StorageCleanupQueue(ApplicationDbContext context, TimeProvider timeProvider)
    : IStorageCleanupQueue
{
    public async Task EnqueueAsync(IEnumerable<string?> storagePaths, CancellationToken cancellationToken)
    {
        foreach (var storagePath in storagePaths.Where(path => !string.IsNullOrWhiteSpace(path))
                     .Cast<string>().Distinct(StringComparer.Ordinal))
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "StorageCleanupQueueItems"
                    ("Id", "StoragePath", "AttemptCount", "NextAttemptAt", "LastError")
                VALUES
                    ({Guid.NewGuid()}, {storagePath}, {0}, {timeProvider.GetUtcNow().UtcDateTime}, {null})
                ON CONFLICT ("StoragePath") DO NOTHING
                """, cancellationToken);
        }
    }
}
