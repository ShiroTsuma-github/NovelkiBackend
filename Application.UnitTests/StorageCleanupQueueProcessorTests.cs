namespace Application.UnitTests;

using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Exceptions;
using Infrastructure.BookCovers;
using Infrastructure.Contexts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

public sealed class StorageCleanupQueueProcessorTests
{
    [Fact]
    public async Task ProcessBatchAsync_ShouldRemoveSuccessfulAndMissingFiles()
    {
        await using var database = await ProcessorDatabase.CreateAsync();
        var now = new DateTimeOffset(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);
        database.Context.StorageCleanupQueueItems.AddRange(
            Item("covers/delete.jpg", now),
            Item("covers/missing.jpg", now));
        await database.Context.SaveChangesAsync();
        var storage = new ProcessorStorage((path, _) => path.Contains("missing", StringComparison.Ordinal)
            ? new EntityNotFoundException<BookCover, Guid>(Guid.Empty)
            : null);
        var processor = CreateProcessor(database.Context, storage, new TestTimeProvider(now));

        var processed = await processor.ProcessBatchAsync(CancellationToken.None);

        Assert.Equal(2, processed);
        Assert.Empty(await database.Context.StorageCleanupQueueItems.ToListAsync());
        Assert.Equal(["covers/delete.jpg", "covers/missing.jpg"], storage.RequestedPaths.OrderBy(path => path));
    }

    [Fact]
    public async Task ProcessBatchAsync_ShouldRetainFailureWithBackoffAndBoundedError()
    {
        await using var database = await ProcessorDatabase.CreateAsync();
        var now = new DateTimeOffset(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);
        database.Context.StorageCleanupQueueItems.Add(Item("covers/retry.jpg", now));
        await database.Context.SaveChangesAsync();
        var longMessage = new string('x', 2500);
        var storage = new ProcessorStorage((_, _) => new IOException(longMessage));
        var clock = new TestTimeProvider(now);
        var processor = CreateProcessor(database.Context, storage, clock);

        Assert.Equal(1, await processor.ProcessBatchAsync(CancellationToken.None));
        var retained = await database.Context.StorageCleanupQueueItems.SingleAsync();
        Assert.Equal(1, retained.AttemptCount);
        Assert.Equal(now.AddMinutes(1).UtcDateTime, retained.NextAttemptAt);
        Assert.Equal(2000, retained.LastError!.Length);

        Assert.Equal(0, await processor.ProcessBatchAsync(CancellationToken.None));
        Assert.Single(storage.RequestedPaths);

        clock.UtcNow = now.AddMinutes(1);
        Assert.Equal(1, await processor.ProcessBatchAsync(CancellationToken.None));
        retained = await database.Context.StorageCleanupQueueItems.SingleAsync();
        Assert.Equal(2, retained.AttemptCount);
        Assert.Equal(clock.UtcNow.AddMinutes(2).UtcDateTime, retained.NextAttemptAt);
        Assert.Equal(2, storage.RequestedPaths.Count);
    }

    private static StorageCleanupQueueProcessor CreateProcessor(ApplicationDbContext context,
        IBookCoverStorage storage, TimeProvider timeProvider) => new(
        context, storage, timeProvider, Mock.Of<ILogger<StorageCleanupQueueProcessor>>());

    private static StorageCleanupQueueItem Item(string path, DateTimeOffset nextAttemptAt) => new()
    {
        StoragePath = path,
        NextAttemptAt = nextAttemptAt.UtcDateTime
    };

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class ProcessorStorage(Func<string, int, Exception?> failureFactory) : IBookCoverStorage
    {
        public List<string> RequestedPaths { get; } = [];

        public Task<BookCoverStoredFiles> SaveAsync(Guid ownerId, Guid bookId, Stream content, string fileName,
            string? contentType, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DeleteIfExistsAsync(string? storagePath, CancellationToken cancellationToken)
        {
            var path = Assert.IsType<string>(storagePath);
            RequestedPaths.Add(path);
            var failure = failureFactory(path, RequestedPaths.Count);
            return failure is null ? Task.CompletedTask : Task.FromException(failure);
        }
    }

    private sealed class ProcessorDatabase(SqliteConnection connection, ApplicationDbContext context)
        : IAsyncDisposable
    {
        public ApplicationDbContext Context { get; } = context;

        public static async Task<ProcessorDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
            var user = Mock.Of<IUser>(item => item.Id == Guid.NewGuid());
            var context = new ApplicationDbContext(options, user);
            await context.Database.EnsureCreatedAsync();
            return new ProcessorDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
