namespace Infrastructure.IntegrationTests.PostgreSql;

using Application.Common;
using Application.Common.Interfaces;
using Application.Common.DTOs.Book;
using Domain.Associations;
using Domain.Entities;
using Domain.Exceptions;
using FluentValidation;
using Infrastructure.BookCovers;
using Infrastructure.Contexts;
using Infrastructure.Identity;
using Infrastructure.Services;
using Infrastructure.IntegrationTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

[Collection(PostgreSqlCollection.CollectionName)]
public sealed class PublicBookPostgreSqlTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly Guid OwnerId = Guid.Parse("90000000-0000-0000-0000-000000000001");

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Migrations_ShouldApplyCompletelyToEmptyDatabase()
    {
        await using var context = fixture.CreateContext(OwnerId);

        var expected = context.Database.GetMigrations().ToArray();
        var applied = (await context.Database.GetAppliedMigrationsAsync()).ToArray();

        Assert.NotEmpty(expected);
        Assert.Equal(expected, applied);
        Assert.True(await context.Database.CanConnectAsync());
    }

    [Fact]
    public async Task Database_ShouldEnforceOneSnapshotPerSourceBook()
    {
        await using var context = fixture.CreateContext(OwnerId);
        var book = await SeedBookAsync(context, OwnerId, "Unique source");
        context.PublicBookSnapshots.Add(Snapshot(book));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        context.PublicBookSnapshots.Add(Snapshot(book));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task ParallelPublishOfSameBook_ShouldReturnSingleSnapshot()
    {
        await using (var seed = fixture.CreateContext(OwnerId))
        {
            await SeedBookAsync(seed, OwnerId, "Parallel source");
        }

        Guid bookId;
        await using (var lookup = fixture.CreateContext(OwnerId))
        {
            bookId = await lookup.Books.Select(book => book.Id).SingleAsync();
        }

        await using var firstContext = fixture.CreateContext(OwnerId);
        await using var secondContext = fixture.CreateContext(OwnerId);
        var first = CreateService(firstContext, OwnerId);
        var second = CreateService(secondContext, OwnerId);

        var results = await Task.WhenAll(
            first.PublishAsync(bookId, CancellationToken.None),
            second.PublishAsync(bookId, CancellationToken.None));

        Assert.Equal(results[0].Id, results[1].Id);
        await using var verify = fixture.CreateContext(OwnerId);
        Assert.Single(await verify.PublicBookSnapshots.ToListAsync());
    }

    [Fact]
    public async Task ParallelPublishWithSameNormalizedAuthorAndTag_ShouldResolveToPublicWinner()
    {
        var secondOwner = Guid.Parse("90000000-0000-0000-0000-000000000002");
        Guid firstBookId;
        Guid secondBookId;
        await using (var seed = fixture.CreateContext(OwnerId))
        {
            seed.Users.AddRange(User(OwnerId, "first"), User(secondOwner, "second"));
            var firstAuthor = TestData.Author("Same Identity", OwnerId, false);
            var secondAuthor = TestData.Author("Same Identity", secondOwner, false);
            var firstTag = TestData.Tag(OwnerId, "same-tag");
            var secondTag = TestData.Tag(secondOwner, "same-tag");
            var firstBook = TestData.Book(OwnerId, "First race", firstAuthor);
            var secondBook = TestData.Book(secondOwner, "Second race", secondAuthor);
            firstBook.BookTags.Add(new BookTag { Book = firstBook, Tag = firstTag });
            secondBook.BookTags.Add(new BookTag { Book = secondBook, Tag = secondTag });
            seed.AddRange(firstBook, secondBook);
            await seed.SaveChangesAsync();
            firstBookId = firstBook.Id;
            secondBookId = secondBook.Id;
        }

        await using var firstContext = fixture.CreateContext(OwnerId);
        await using var secondContext = fixture.CreateContext(secondOwner);
        var results = await Task.WhenAll(
            CreateService(firstContext, OwnerId).PublishAsync(firstBookId, CancellationToken.None),
            CreateService(secondContext, secondOwner).PublishAsync(secondBookId, CancellationToken.None));

        Assert.Equal(2, results.Length);
        await using var verify = fixture.CreateContext(OwnerId);
        var snapshots = await verify.PublicBookSnapshots.ToListAsync();
        Assert.Equal(2, snapshots.Count);
        Assert.Single(snapshots.Select(item => item.PublicAuthorId).Distinct());
        var tagIds = snapshots.SelectMany(item =>
            System.Text.Json.JsonSerializer.Deserialize<Guid[]>(item.PublicTagIdsJson)!).Distinct().ToArray();
        Assert.Single(tagIds);
        Assert.Single(await verify.Authors.Where(author => author.IsPublic).ToListAsync());
        Assert.Single(await verify.Tags.Where(tag => tag.IsGlobal).ToListAsync());
        Assert.Single(await verify.BookShareAuthorPromotions.ToListAsync());
        Assert.Single(await verify.BookShareTagPromotions.ToListAsync());
    }

    [Fact]
    public async Task ParallelCopy_ShouldCreateOneBookAndReturnDuplicateValidationForLoser()
    {
        var targetOwner = Guid.Parse("90000000-0000-0000-0000-000000000003");
        Guid snapshotId;
        await using (var seed = fixture.CreateContext(OwnerId))
        {
            seed.Users.AddRange(User(OwnerId, "source"), User(targetOwner, "target"));
            var book = TestData.Book(OwnerId, "Copy race");
            seed.Books.Add(book);
            await seed.SaveChangesAsync();
            snapshotId = (await CreateService(seed, OwnerId).PublishAsync(book.Id, CancellationToken.None)).Id;
        }

        await using var firstContext = fixture.CreateContext(targetOwner);
        await using var secondContext = fixture.CreateContext(targetOwner);
        var outcomes = await Task.WhenAll(
            CaptureCopyAsync(CreateService(firstContext, targetOwner), snapshotId),
            CaptureCopyAsync(CreateService(secondContext, targetOwner), snapshotId));

        Assert.Single(outcomes, outcome => outcome.Result is not null);
        Assert.IsType<ValidationException>(Assert.Single(outcomes, outcome => outcome.Error is not null).Error);
        await using var verify = fixture.CreateContext(targetOwner);
        Assert.Single(await verify.Books.Where(book => book.OwnerId == targetOwner).ToListAsync());
    }

    [Fact]
    public async Task DoubleUnlistAndRefreshUnlistRace_ShouldEndWithoutServerErrors()
    {
        Guid bookId;
        Guid snapshotId;
        await using (var seed = fixture.CreateContext(OwnerId))
        {
            seed.Users.Add(User(OwnerId, "owner"));
            var book = TestData.Book(OwnerId, "Double unlist");
            seed.Books.Add(book);
            await seed.SaveChangesAsync();
            bookId = book.Id;
            snapshotId = (await CreateService(seed, OwnerId).PublishAsync(book.Id, CancellationToken.None)).Id;
        }

        await using (var firstContext = fixture.CreateContext(OwnerId))
        await using (var secondContext = fixture.CreateContext(OwnerId))
        {
            var outcomes = await Task.WhenAll(
                CaptureAsync(() => CreateService(firstContext, OwnerId).UnlistAsync(snapshotId, CancellationToken.None)),
                CaptureAsync(() => CreateService(secondContext, OwnerId).UnlistAsync(snapshotId, CancellationToken.None)));
            Assert.Single(outcomes, error => error is null);
            Assert.IsType<EntityNotFoundException<PublicBookSnapshot, Guid>>(
                Assert.Single(outcomes, error => error is not null));
        }

        await using (var publishContext = fixture.CreateContext(OwnerId))
        {
            snapshotId = (await CreateService(publishContext, OwnerId)
                .PublishAsync(bookId, CancellationToken.None)).Id;
        }
        await using (var refreshContext = fixture.CreateContext(OwnerId))
        await using (var unlistContext = fixture.CreateContext(OwnerId))
        {
            var refresh = CaptureAsync(async () =>
            {
                await CreateService(refreshContext, OwnerId).RefreshAsync(snapshotId, CancellationToken.None);
            });
            var unlist = CaptureAsync(() =>
                CreateService(unlistContext, OwnerId).UnlistAsync(snapshotId, CancellationToken.None));
            var outcomes = await Task.WhenAll(refresh, unlist);
            Assert.DoesNotContain(outcomes, error => error is not null &&
                error is not EntityNotFoundException<PublicBookSnapshot, Guid>);
        }

        await using var verify = fixture.CreateContext(OwnerId);
        Assert.False(await verify.PublicBookSnapshots.AnyAsync());
    }

    [Fact]
    public async Task ForcedSaveFailure_ShouldRollbackSnapshotPromotionsAndRelations()
    {
        Guid bookId;
        await using (var seed = fixture.CreateContext(OwnerId))
        {
            seed.Users.Add(User(OwnerId, "rollback-owner"));
            var author = TestData.Author("Rollback PG", OwnerId, false);
            var tag = TestData.Tag(OwnerId, "rollback-pg");
            var book = TestData.Book(OwnerId, "Rollback PG Book", author);
            book.BookTags.Add(new BookTag { Book = book, Tag = tag });
            seed.Books.Add(book);
            await seed.SaveChangesAsync();
            bookId = book.Id;
        }

        await using (var failing = fixture.CreateContext(OwnerId, new FailSnapshotSaveInterceptor()))
        {
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                CreateService(failing, OwnerId).PublishAsync(bookId, CancellationToken.None));
        }

        await using var verify = fixture.CreateContext(OwnerId);
        Assert.False(await verify.PublicBookSnapshots.AnyAsync());
        Assert.False(await verify.BookShareAuthorPromotions.AnyAsync());
        Assert.False(await verify.BookShareTagPromotions.AnyAsync());
        Assert.False((await verify.Authors.SingleAsync()).IsPublic);
        Assert.False((await verify.Tags.SingleAsync()).IsGlobal);
    }

    private static async Task<Book> SeedBookAsync(ApplicationDbContext context, Guid ownerId, string title)
    {
        context.Users.Add(User(ownerId, title.Replace(' ', '-').ToLowerInvariant()));
        var book = TestData.Book(ownerId, title);
        context.Books.Add(book);
        await context.SaveChangesAsync();
        return book;
    }

    private static PublicBookSnapshot Snapshot(Book book) => new()
    {
        SourceBookId = book.Id,
        SourceBook = book,
        OwnerId = book.OwnerId,
        PrimaryTitle = book.PrimaryTitle,
        NormalizedPrimaryTitle = book.NormalizedPrimaryTitle,
        AlternativeTitlesJson = "[]",
        AuthorOtherNamesJson = "[]",
        ContentType = "Novel",
        GenresJson = "[]",
        TagsJson = "[]",
        PublicTagIdsJson = "[]",
        SnapshotAt = DateTimeOffset.UtcNow
    };

    private static PublicBookService CreateService(ApplicationDbContext context, Guid userId)
    {
        var cache = new NoopCache();
        return new PublicBookService(context, new TestUser(userId), new NoopStorage(),
            new AuthorLifecycleService(context, cache), cache,
            new StorageCleanupQueue(context, TimeProvider.System));
    }

    private static async Task<(CopyPublicBookResult? Result, Exception? Error)> CaptureCopyAsync(
        IPublicBookService service, Guid snapshotId)
    {
        try
        {
            return (await service.CopyAsync(snapshotId, CancellationToken.None), null);
        }
        catch (Exception exception)
        {
            return (null, exception);
        }
    }

    private static async Task<Exception?> CaptureAsync(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static User User(Guid id, string name) => new()
    {
        Id = id,
        UserName = name,
        NormalizedUserName = name.ToUpperInvariant()
    };

    private sealed record TestUser(Guid UserId) : IUser
    {
        public Guid? Id => UserId;
        public Guid RequiredId => UserId;
        public string? Email => null;
        public string? Username => null;
        public IEnumerable<string> Roles => [];
        public bool IsAuthenticated => true;
        public bool Valid => true;
    }

    private sealed class NoopCache : IBookListCacheInvalidator
    {
        public Task InvalidateBooksAsync(Guid ownerId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NoopStorage : IBookCoverStorage
    {
        public Task<BookCoverStoredFiles> SaveAsync(Guid ownerId, Guid bookId, Stream content, string fileName,
            string? contentType, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DeleteIfExistsAsync(string? storagePath, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FailSnapshotSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<PublicBookSnapshot>()
                .Any(entry => entry.State == EntityState.Added))
            {
                throw new DbUpdateException("forced PostgreSQL snapshot failure");
            }

            return ValueTask.FromResult(result);
        }
    }
}
