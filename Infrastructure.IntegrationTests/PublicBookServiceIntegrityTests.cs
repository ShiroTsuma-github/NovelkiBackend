namespace Infrastructure.IntegrationTests;

using Application.Common.Interfaces;
using Domain.Associations;
using Domain.Entities;
using FluentValidation;
using Infrastructure.BookCovers;
using Infrastructure.Contexts;
using Infrastructure.Identity;
using Infrastructure.Services;
using Infrastructure.IntegrationTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public sealed class PublicBookServiceIntegrityTests
{
    [Fact]
    public async Task Search_ShouldMatchAllNamesClampPaginationAndMarkOwnership()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var otherOwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        context.Users.Add(User(otherOwnerId, "other"));
        var mine = TestData.Book(database.UserId, "Primary Needle");
        var other = TestData.Book(otherOwnerId, "Other title");
        context.Books.AddRange(mine, other);
        context.PublicBookSnapshots.AddRange(
            Snapshot(mine, database.UserId, "Primary Needle", "[\"Alternative Match\"]", "Main Author",
                "[\"Alias Match\"]"),
            Snapshot(other, otherOwnerId, "Other title", "[]", "Second Author", "[]"));
        await context.SaveChangesAsync();
        var service = CreateService(context, database.UserId, new MemoryStorage());

        Assert.Single((await service.SearchAsync("primary", 0, 20, false, CancellationToken.None)).Data);
        Assert.Single((await service.SearchAsync("alternative", 0, 20, false, CancellationToken.None)).Data);
        Assert.Single((await service.SearchAsync("main author", 0, 20, false, CancellationToken.None)).Data);
        Assert.Single((await service.SearchAsync("alias", 0, 20, false, CancellationToken.None)).Data);

        var mineOnly = await service.SearchAsync(null, -10, 500, true, CancellationToken.None);
        Assert.Equal(0, mineOnly.Skip);
        Assert.Equal(50, mineOnly.Take);
        Assert.Single(mineOnly.Data);
        Assert.True(mineOnly.Data[0].IsOwner);

        var all = await service.SearchAsync(null, 0, 1, false, CancellationToken.None);
        Assert.Equal(2, all.Total);
        Assert.Single(all.Data);
        Assert.False(all.Data[0].IsOwner);
    }

    [Fact]
    public async Task Publish_ShouldBeOwnedBareIdempotentAndSnapshotOnlyOnRefresh()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var otherOwnerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        context.Users.Add(User(otherOwnerId, "foreign"));
        var book = TestData.Book(database.UserId, "Original");
        var foreign = TestData.Book(otherOwnerId, "Foreign");
        context.Books.AddRange(book, foreign);
        await context.SaveChangesAsync();
        var service = CreateService(context, database.UserId, new MemoryStorage());

        var published = await service.PublishAsync(book.Id, CancellationToken.None);
        Assert.Null(published.Author);
        Assert.Empty(published.Tags);
        Assert.Empty(published.Genres);
        Assert.Null(published.CoverUrl);
        context.ChangeTracker.Clear();
        book = await context.Books.SingleAsync(item => item.Id == book.Id);
        book.PrimaryTitle = "Edited source";
        book.NormalizedPrimaryTitle = "EDITED SOURCE";
        await context.SaveChangesAsync();
        var beforeRefresh = await service.SearchAsync("original", 0, 20, true, CancellationToken.None);
        Assert.Single(beforeRefresh.Data);
        Assert.Equal("Original", beforeRefresh.Data[0].PrimaryTitle);

        var republished = await service.PublishAsync(book.Id, CancellationToken.None);
        Assert.Equal(published.Id, republished.Id);
        Assert.Equal("Edited source", republished.PrimaryTitle);
        Assert.Single(await context.PublicBookSnapshots.ToListAsync());
        await Assert.ThrowsAsync<Domain.Exceptions.EntityNotFoundException<Book, Guid>>(() =>
            service.PublishAsync(foreign.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Refresh_ShouldReplaceMetadataAndCleanupOldAutomaticPromotions()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var oldAuthor = TestData.Author("Old Author", database.UserId, false);
        var newAuthor = TestData.Author("New Author", database.UserId, false);
        var oldTag = TestData.Tag(database.UserId, "old-tag");
        var newTag = TestData.Tag(database.UserId, "new-tag");
        var oldGenre = TestData.Genre("Old Genre");
        var newGenre = TestData.Genre("New Genre");
        var book = TestData.Book(database.UserId, "Changing", oldAuthor);
        book.BookTags.Add(new BookTag { Book = book, Tag = oldTag });
        book.BookGenres.Add(new BookGenre { Book = book, Genre = oldGenre });
        context.AddRange(newAuthor, newTag, newGenre, book);
        await context.SaveChangesAsync();
        var service = CreateService(context, database.UserId, new MemoryStorage());
        var published = await service.PublishAsync(book.Id, CancellationToken.None);

        await context.Set<BookTag>().Where(item => item.BookId == book.Id).ExecuteDeleteAsync();
        await context.Set<BookGenre>().Where(item => item.BookId == book.Id).ExecuteDeleteAsync();
        context.ChangeTracker.Clear();
        book = await context.Books.SingleAsync(item => item.Id == book.Id);
        newAuthor = await context.Authors.SingleAsync(item => item.Id == newAuthor.Id);
        newTag = await context.Tags.SingleAsync(item => item.Id == newTag.Id);
        newGenre = await context.Genres.SingleAsync(item => item.Id == newGenre.Id);
        book.Author = newAuthor;
        book.AuthorId = newAuthor.Id;
        book.BookTags.Add(new BookTag { Book = book, Tag = newTag });
        book.BookGenres.Add(new BookGenre { Book = book, Genre = newGenre });
        book.Description = "refreshed";
        context.BookTitles.Add(new BookTitle
        {
            BookId = book.Id, Title = "Fresh Alias", NormalizedTitle = "FRESH ALIAS", IsPrimary = false,
            Source = "test"
        });
        await context.SaveChangesAsync();

        var refreshed = await service.RefreshAsync(published.Id, CancellationToken.None);

        Assert.Equal("New Author", refreshed.Author);
        Assert.Equal("refreshed", refreshed.Description);
        Assert.Contains("Fresh Alias", refreshed.AlternativeTitles);
        Assert.Equal("new-tag", Assert.Single(refreshed.Tags).Name);
        Assert.Equal("New Genre", Assert.Single(refreshed.Genres).Name);
        Assert.False(await context.Authors.AnyAsync(item => item.Id == oldAuthor.Id));
        Assert.False(await context.Tags.AnyAsync(item => item.Id == oldTag.Id));
        Assert.Null(await context.BookShareAuthorPromotions.FindAsync(oldAuthor.Id));
        Assert.Null(await context.BookShareTagPromotions.FindAsync(oldTag.Id));
        Assert.NotNull(await context.BookShareAuthorPromotions.FindAsync(newAuthor.Id));
        Assert.NotNull(await context.BookShareTagPromotions.FindAsync(newTag.Id));
    }

    [Fact]
    public async Task Publish_ShouldReuseExistingPublicMetadataAndRetainSharedPromotionUntilLastSnapshot()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var publicAuthor = TestData.Author("Canonical", null, true);
        var privateAuthor = TestData.Author("Canonical", database.UserId, false);
        var publicTag = TestData.Tag(database.UserId, "canonical-tag");
        publicTag.IsGlobal = true;
        var privateTagOwner = Guid.Parse("44444444-4444-4444-4444-444444444444");
        context.Users.Add(User(privateTagOwner, "private-tag-owner"));
        var privateTag = TestData.Tag(privateTagOwner, "canonical-tag");
        var reused = TestData.Book(database.UserId, "Reuse", privateAuthor);
        reused.BookTags.Add(new BookTag { Book = reused, Tag = privateTag });
        context.AddRange(publicAuthor, publicTag, reused);
        await context.SaveChangesAsync();
        var service = CreateService(context, database.UserId, new MemoryStorage());

        var reusedDto = await service.PublishAsync(reused.Id, CancellationToken.None);
        var reusedSnapshot = await context.PublicBookSnapshots.SingleAsync(item => item.Id == reusedDto.Id);
        Assert.Equal(publicAuthor.Id, reusedSnapshot.PublicAuthorId);
        Assert.Contains(publicTag.Id, System.Text.Json.JsonSerializer.Deserialize<Guid[]>(reusedSnapshot.PublicTagIdsJson)!);
        Assert.Null(await context.BookShareAuthorPromotions.FindAsync(publicAuthor.Id));
        Assert.Null(await context.BookShareTagPromotions.FindAsync(publicTag.Id));
        Assert.False(privateAuthor.IsPublic);
        Assert.False(privateTag.IsGlobal);

        var sharedAuthor = TestData.Author("Shared automatic", database.UserId, false);
        var sharedTag = TestData.Tag(database.UserId, "shared-automatic");
        var first = TestData.Book(database.UserId, "First shared", sharedAuthor);
        var second = TestData.Book(database.UserId, "Second shared", sharedAuthor);
        first.BookTags.Add(new BookTag { Book = first, Tag = sharedTag });
        second.BookTags.Add(new BookTag { Book = second, Tag = sharedTag });
        context.AddRange(first, second);
        await context.SaveChangesAsync();
        var firstSnapshot = await service.PublishAsync(first.Id, CancellationToken.None);
        var secondSnapshot = await service.PublishAsync(second.Id, CancellationToken.None);

        await service.UnlistAsync(firstSnapshot.Id, CancellationToken.None);
        Assert.NotNull(await context.BookShareAuthorPromotions.FindAsync(sharedAuthor.Id));
        Assert.NotNull(await context.BookShareTagPromotions.FindAsync(sharedTag.Id));
        await service.UnlistAsync(secondSnapshot.Id, CancellationToken.None);
        Assert.Null(await context.BookShareAuthorPromotions.FindAsync(sharedAuthor.Id));
        Assert.Null(await context.BookShareTagPromotions.FindAsync(sharedTag.Id));
    }

    [Fact]
    public async Task Copy_ShouldUsePrivateMetadataSkipMissingGenreAndNotCopyPersonalData()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var targetOwner = Guid.Parse("55555555-5555-5555-5555-555555555555");
        context.Users.Add(User(targetOwner, "copy-owner"));
        var sourceAuthor = TestData.Author("Source Author", database.UserId, false);
        sourceAuthor.Names.Add(new AuthorName
        {
            Name = "Source Alias", NormalizedName = "SOURCE ALIAS", IsPrimary = false, Source = "test"
        });
        var sourceTag = TestData.Tag(database.UserId, "snapshot-tag");
        var genre = TestData.Genre("Temporary Genre");
        var source = TestData.Book(database.UserId, "Copy Me", sourceAuthor);
        source.Notes = "private notes";
        source.CurrentChapterNumber = 99;
        source.Links.Add(new BookLink { Url = "https://private.example", SourceType = "private" });
        source.ProgressHistory.Add(new BookProgressHistory { ChapterNumber = 99, Comment = "private" });
        source.BookTags.Add(new BookTag { Book = source, Tag = sourceTag });
        source.BookGenres.Add(new BookGenre { Book = source, Genre = genre });
        context.Add(source);
        await context.SaveChangesAsync();
        var storage = new MemoryStorage();
        var ownerService = CreateService(context, database.UserId, storage);
        var snapshot = await ownerService.PublishAsync(source.Id, CancellationToken.None);

        context.Remove(source.BookGenres.Single());
        context.Genres.Remove(genre);
        var preferredAuthor = TestData.Author("Preferred Local", targetOwner, false);
        preferredAuthor.Names.Add(new AuthorName
        {
            Name = "Source Author", NormalizedName = "SOURCE AUTHOR", IsPrimary = false, Source = "test"
        });
        var preferredTag = TestData.Tag(targetOwner, "snapshot-tag");
        context.AddRange(preferredAuthor, preferredTag);
        await context.SaveChangesAsync();

        var copyService = CreateService(context, targetOwner, storage);
        var copiedId = (await copyService.CopyAsync(snapshot.Id, CancellationToken.None)).BookId;
        var copied = await context.Books.AsNoTracking().Include(item => item.Status).Include(item => item.BookGenres)
            .Include(item => item.BookTags).Include(item => item.Links).Include(item => item.ProgressHistory)
            .SingleAsync(item => item.Id == copiedId);
        Assert.Equal(preferredAuthor.Id, copied.AuthorId);
        Assert.Equal(preferredTag.Id, Assert.Single(copied.BookTags).TagId);
        Assert.Empty(copied.BookGenres);
        Assert.Equal("plan-to-read", copied.Status.Slug);
        Assert.Equal(0, copied.CurrentChapterNumber);
        Assert.Null(copied.Notes);
        Assert.Empty(copied.Links);
        Assert.Empty(copied.ProgressHistory);

        var counts = (await context.Books.CountAsync(), await context.Authors.CountAsync(), await context.Tags.CountAsync());
        await Assert.ThrowsAsync<ValidationException>(() =>
            copyService.CopyAsync(snapshot.Id, CancellationToken.None));
        Assert.Equal(counts,
            (await context.Books.CountAsync(), await context.Authors.CountAsync(), await context.Tags.CountAsync()));
    }

    [Fact]
    public async Task Copy_ShouldReturnValidationWhenSnapshotContentTypeNoLongerExists()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var book = await TestData.AddBookAsync(context, database.UserId, "Unavailable type");
        var service = CreateService(context, database.UserId, new MemoryStorage());
        var snapshot = await service.PublishAsync(book.Id, CancellationToken.None);
        var entity = await context.PublicBookSnapshots.SingleAsync(item => item.Id == snapshot.Id);
        entity.ContentType = "Removed Type";
        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CopyAsync(snapshot.Id, CancellationToken.None));

        Assert.Contains("Removed Type", exception.Message);
        Assert.Single(await context.Books.ToListAsync());
    }

    [Fact]
    public async Task CoverLifecycle_ShouldVersionSnapshotPathsQueueBothVariantsAndCopyCover()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var targetOwner = Guid.Parse("66666666-6666-6666-6666-666666666666");
        context.Users.Add(User(targetOwner, "cover-copy"));
        var book = TestData.Book(database.UserId, "Covered");
        book.Cover = new BookCover
        {
            Book = book,
            BookId = book.Id,
            StoragePath = "source/original.jpg",
            ThumbnailStoragePath = "source/original.thumb.jpg",
            MimeType = "image/jpeg",
            Status = BookCoverStatus.Uploaded
        };
        context.Add(book);
        await context.SaveChangesAsync();
        var storage = new MemoryStorage();
        storage.Files["source/original.jpg"] = [1, 2, 3];
        var service = CreateService(context, database.UserId, storage);

        var published = await service.PublishAsync(book.Id, CancellationToken.None);
        var firstPaths = await context.PublicBookSnapshots.Where(item => item.Id == published.Id)
            .Select(item => new { item.CoverStoragePath, item.CoverThumbnailStoragePath }).SingleAsync();
        await using (var opened = (await service.OpenCoverAsync(published.Id, CancellationToken.None)).Content)
        {
            Assert.Equal(3, opened.Length);
        }
        var copiedId = (await CreateService(context, targetOwner, storage)
            .CopyAsync(published.Id, CancellationToken.None)).BookId;
        Assert.NotNull((await context.BookCovers.SingleAsync(item => item.BookId == copiedId)).ThumbnailStoragePath);

        var refreshed = await service.RefreshAsync(published.Id, CancellationToken.None);
        var secondPaths = await context.PublicBookSnapshots.Where(item => item.Id == refreshed.Id)
            .Select(item => new { item.CoverStoragePath, item.CoverThumbnailStoragePath }).SingleAsync();
        Assert.NotEqual(firstPaths.CoverStoragePath, secondPaths.CoverStoragePath);
        Assert.NotEqual(firstPaths.CoverThumbnailStoragePath, secondPaths.CoverThumbnailStoragePath);
        await service.UnlistAsync(published.Id, CancellationToken.None);

        var queued = await context.StorageCleanupQueueItems.Select(item => item.StoragePath).ToListAsync();
        Assert.Contains(firstPaths.CoverStoragePath!, queued);
        Assert.Contains(firstPaths.CoverThumbnailStoragePath!, queued);
        Assert.Contains(secondPaths.CoverStoragePath!, queued);
        Assert.Contains(secondPaths.CoverThumbnailStoragePath!, queued);
    }

    [Fact]
    public async Task PublishStorageOrDatabaseFailure_ShouldRollbackMetadataAndQueueCreatedFiles()
    {
        using var database = new SqliteTestDatabase();
        Guid bookId;
        await using (var seed = database.CreateContext())
        {
            var author = TestData.Author("Rollback Author", database.UserId, false);
            var tag = TestData.Tag(database.UserId, "rollback-tag");
            var book = TestData.Book(database.UserId, "Rollback publish", author);
            book.BookTags.Add(new BookTag { Book = book, Tag = tag });
            book.Cover = new BookCover
            {
                Book = book, BookId = book.Id, StoragePath = "source/failure.jpg", MimeType = "image/jpeg"
            };
            seed.Add(book);
            await seed.SaveChangesAsync();
            bookId = book.Id;
        }

        var failingStorage = new MemoryStorage { FailSave = true };
        failingStorage.Files["source/failure.jpg"] = [1];
        await using (var context = database.CreateContext())
        {
            await Assert.ThrowsAsync<IOException>(() =>
                CreateService(context, database.UserId, failingStorage)
                    .PublishAsync(bookId, CancellationToken.None));
        }
        await using (var verify = database.CreateContext())
        {
            Assert.False(await verify.PublicBookSnapshots.AnyAsync());
            Assert.False(await verify.BookShareAuthorPromotions.AnyAsync());
            Assert.False(await verify.BookShareTagPromotions.AnyAsync());
            Assert.False((await verify.Authors.SingleAsync()).IsPublic);
            Assert.False((await verify.Tags.SingleAsync()).IsGlobal);
        }

        var storage = new MemoryStorage();
        storage.Files["source/failure.jpg"] = [1];
        await using (var context = database.CreateContext(new FailSnapshotSaveInterceptor()))
        {
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                CreateService(context, database.UserId, storage).PublishAsync(bookId, CancellationToken.None));
        }
        await using (var verify = database.CreateContext())
        {
            Assert.False(await verify.PublicBookSnapshots.AnyAsync());
            Assert.False(await verify.BookShareAuthorPromotions.AnyAsync());
            Assert.False(await verify.BookShareTagPromotions.AnyAsync());
            Assert.Equal(6, await verify.StorageCleanupQueueItems.CountAsync());
        }
    }

    [Fact]
    public async Task UnlistHelpersAndMissingSnapshotOperations_ShouldBeControlled()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var first = await TestData.AddBookAsync(context, database.UserId, "First helper");
        var second = await TestData.AddBookAsync(context, database.UserId, "Second helper");
        var service = CreateService(context, database.UserId, new MemoryStorage());

        await service.UnlistBySourceBookAsync(Guid.NewGuid(), CancellationToken.None);
        await service.UnlistAllForOwnerAsync(Guid.NewGuid(), CancellationToken.None);
        var firstSnapshot = await service.PublishAsync(first.Id, CancellationToken.None);
        await service.UnlistBySourceBookAsync(first.Id, CancellationToken.None);
        Assert.False(await context.PublicBookSnapshots.AnyAsync(item => item.Id == firstSnapshot.Id));

        await service.PublishAsync(first.Id, CancellationToken.None);
        await service.PublishAsync(second.Id, CancellationToken.None);
        await service.UnlistAllForOwnerAsync(database.UserId, CancellationToken.None);
        Assert.False(await context.PublicBookSnapshots.AnyAsync());

        var missing = Guid.NewGuid();
        await Assert.ThrowsAsync<Domain.Exceptions.EntityNotFoundException<PublicBookSnapshot, Guid>>(() =>
            service.RefreshAsync(missing, CancellationToken.None));
        await Assert.ThrowsAsync<Domain.Exceptions.EntityNotFoundException<PublicBookSnapshot, Guid>>(() =>
            service.UnlistAsync(missing, CancellationToken.None));
        await Assert.ThrowsAsync<Domain.Exceptions.EntityNotFoundException<PublicBookSnapshot, Guid>>(() =>
            service.CopyAsync(missing, CancellationToken.None));
        await Assert.ThrowsAsync<Domain.Exceptions.EntityNotFoundException<PublicBookSnapshot, Guid>>(() =>
            service.OpenCoverAsync(missing, CancellationToken.None));
    }

    [Fact]
    public async Task Copy_ShouldCreatePrivateAuthorAliasesAndTagWhenNoIdentityExists()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var targetOwner = Guid.Parse("77777777-7777-7777-7777-777777777777");
        context.Users.Add(User(targetOwner, "new-identities"));
        var source = TestData.Book(database.UserId, "Orphan metadata snapshot");
        context.Books.Add(source);
        var snapshot = Snapshot(source, database.UserId, source.PrimaryTitle, "[]", "Orphan Author",
            "[\"Orphan Author\",\"Useful Alias\"]");
        snapshot.TagsJson = "[{\"name\":\"created-tag\",\"description\":\"created\"}]";
        context.PublicBookSnapshots.Add(snapshot);
        await context.SaveChangesAsync();

        var copiedId = (await CreateService(context, targetOwner, new MemoryStorage())
            .CopyAsync(snapshot.Id, CancellationToken.None)).BookId;

        var copied = await context.Books.AsNoTracking().Include(item => item.Author)!
            .ThenInclude(author => author!.Names).Include(item => item.BookTags).ThenInclude(item => item.Tag)
            .SingleAsync(item => item.Id == copiedId);
        Assert.False(copied.Author!.IsPublic);
        Assert.Equal(targetOwner, copied.Author.OwnerId);
        Assert.Equal(2, copied.Author.Names.Count);
        Assert.Contains(copied.Author.Names, name => name.Name == "Useful Alias");
        var tag = Assert.Single(copied.BookTags).Tag;
        Assert.False(tag.IsGlobal);
        Assert.Equal(targetOwner, tag.OwnerId);
    }

    private static PublicBookService CreateService(ApplicationDbContext context, Guid userId,
        IBookCoverStorage storage)
    {
        var cache = new NoopCache();
        return new PublicBookService(context, new TestUser(userId), storage,
            new AuthorLifecycleService(context, cache), cache,
            new StorageCleanupQueue(context, TimeProvider.System));
    }

    private static PublicBookSnapshot Snapshot(Book book, Guid ownerId, string title, string alternatives,
        string? author, string aliases) => new()
    {
        SourceBookId = book.Id,
        SourceBook = book,
        OwnerId = ownerId,
        PrimaryTitle = title,
        NormalizedPrimaryTitle = title.ToUpperInvariant(),
        AlternativeTitlesJson = alternatives,
        AuthorName = author,
        AuthorOtherNamesJson = aliases,
        ContentType = "Novel",
        GenresJson = "[]",
        TagsJson = "[]",
        PublicTagIdsJson = "[]",
        SnapshotAt = DateTimeOffset.UtcNow
    };

    private static User User(Guid id, string name) => new()
    {
        Id = id, UserName = name, NormalizedUserName = name.ToUpperInvariant()
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

    private sealed class MemoryStorage : IBookCoverStorage
    {
        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
        public bool FailSave { get; init; }

        public async Task<BookCoverStoredFiles> SaveAsync(Guid ownerId, Guid bookId, Stream content, string fileName,
            string? contentType, CancellationToken cancellationToken)
        {
            if (FailSave) throw new IOException("forced storage failure");
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            var originalPath = $"{ownerId:N}/{bookId:N}.jpg";
            var thumbnailPath = $"{ownerId:N}/{bookId:N}.thumb.jpg";
            Files[originalPath] = buffer.ToArray();
            Files[thumbnailPath] = buffer.ToArray();
            return new BookCoverStoredFiles(
                new BookCoverStoredVariant(originalPath, "image/jpeg", buffer.Length, 10, 20),
                new BookCoverStoredVariant(thumbnailPath, "image/jpeg", buffer.Length, 5, 10));
        }

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken) =>
            Files.TryGetValue(storagePath, out var bytes)
                ? Task.FromResult<Stream>(new MemoryStream(bytes, false))
                : Task.FromException<Stream>(new Domain.Exceptions.EntityNotFoundException<BookCover, Guid>(Guid.Empty));

        public Task DeleteIfExistsAsync(string? storagePath, CancellationToken cancellationToken)
        {
            if (storagePath is not null) Files.Remove(storagePath);
            return Task.CompletedTask;
        }
    }

    private sealed class FailSnapshotSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<PublicBookSnapshot>()
                .Any(entry => entry.State == EntityState.Added))
            {
                throw new DbUpdateException("forced snapshot save failure");
            }

            return ValueTask.FromResult(result);
        }
    }
}
