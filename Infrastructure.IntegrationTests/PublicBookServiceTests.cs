namespace Infrastructure.IntegrationTests;

using Application.Common.Interfaces;
using BookCovers;
using Domain.Associations;
using Domain.Entities;
using Identity;
using Microsoft.EntityFrameworkCore;
using Services;
using TestSupport;

public sealed class PublicBookServiceTests
{
    [Fact]
    public async Task PublishCopyAndUnlist_ShouldSnapshotMetadataAndLocalizeAutoPromotedIdentities()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var otherOwnerId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        context.Users.Add(new User
        {
            Id = otherOwnerId,
            UserName = "other-reader",
            NormalizedUserName = "OTHER-READER"
        });

        var author = TestData.Author("Cuttlefish That Loves Diving", database.UserId, false);
        author.Names.Add(new AuthorName
        {
            Name = "Squid",
            NormalizedName = "SQUID",
            IsPrimary = false,
            Source = "Test"
        });
        var tag = TestData.Tag(database.UserId, "Mystery");
        tag.Description = "Secrets and investigations";
        var genre = TestData.Genre("Fantasy");
        genre.Description = "Magic and the impossible";
        var book = TestData.Book(database.UserId, "Lord of Mysteries", author);
        book.Description = "A snapshot description";
        book.Titles.Add(new BookTitle
        {
            Title = "Guimi Zhi Zhu",
            NormalizedTitle = "GUIMI ZHI ZHU",
            IsPrimary = false,
            Source = "Test"
        });
        book.BookTags.Add(new BookTag { Book = book, Tag = tag });
        book.BookGenres.Add(new BookGenre { Book = book, Genre = genre });
        TestData.MakePublishable(book);
        context.AddRange(author, tag, genre, book);
        await context.SaveChangesAsync();

        var cache = new NoopCache();
        var storage = new NoopStorage();
        var ownerService = CreateService(context, new TestUser(database.UserId), storage, cache);
        var snapshot = await ownerService.PublishAsync(book.Id, CancellationToken.None);

        Assert.True((await context.Authors.FindAsync(author.Id))!.IsPublic);
        Assert.True((await context.Tags.FindAsync(tag.Id))!.IsGlobal);
        Assert.NotNull(await context.BookShareAuthorPromotions.FindAsync(author.Id));
        Assert.NotNull(await context.BookShareTagPromotions.FindAsync(tag.Id));
        Assert.Equal("A snapshot description", snapshot.Description);
        Assert.Contains("Guimi Zhi Zhu", snapshot.AlternativeTitles);
        Assert.Contains("Squid", snapshot.AuthorOtherNames);
        Assert.Contains(snapshot.Tags, item => item.Name == "Mystery" && item.Description == tag.Description);
        Assert.Contains(snapshot.Genres, item => item.Name == "Fantasy" && item.Description == genre.Description);
        Assert.Single((await ownerService.SearchAsync("Squid", 0, 20, false, CancellationToken.None)).Data);

        var otherService = CreateService(context, new TestUser(otherOwnerId), storage, cache);
        var copyResult = await otherService.CopyAsync(snapshot.Id, CancellationToken.None);
        var copiedBeforeUnlist = await context.Books.AsNoTracking()
            .Include(item => item.Titles)
            .SingleAsync(item => item.Id == copyResult.BookId);
        Assert.Equal(author.Id, copiedBeforeUnlist.AuthorId);
        Assert.Equal("A snapshot description", copiedBeforeUnlist.Description);
        Assert.Contains(copiedBeforeUnlist.Titles, title => title.Title == "Guimi Zhi Zhu");

        await ownerService.UnlistAsync(snapshot.Id, CancellationToken.None);
        context.ChangeTracker.Clear();

        Assert.False(await context.PublicBookSnapshots.AnyAsync());
        Assert.False(await context.BookShareAuthorPromotions.AnyAsync());
        Assert.False(await context.BookShareTagPromotions.AnyAsync());

        var ownerAuthor = await context.Authors.SingleAsync(item => item.OwnerId == database.UserId);
        var otherAuthor = await context.Authors.SingleAsync(item => item.OwnerId == otherOwnerId);
        Assert.False(ownerAuthor.IsPublic);
        Assert.False(otherAuthor.IsPublic);
        Assert.Equal(ownerAuthor.Id, (await context.Books.SingleAsync(item => item.Id == book.Id)).AuthorId);
        Assert.Equal(otherAuthor.Id, (await context.Books.SingleAsync(item => item.Id == copyResult.BookId)).AuthorId);

        var ownerTag = await context.Tags.SingleAsync(item => item.OwnerId == database.UserId);
        var otherTag = await context.Tags.SingleAsync(item => item.OwnerId == otherOwnerId);
        Assert.False(ownerTag.IsGlobal);
        Assert.False(otherTag.IsGlobal);
        Assert.Equal(ownerTag.Id, (await context.Set<BookTag>().SingleAsync(item => item.BookId == book.Id)).TagId);
        Assert.Equal(otherTag.Id,
            (await context.Set<BookTag>().SingleAsync(item => item.BookId == copyResult.BookId)).TagId);

        var copiedAfterUnlist = await context.Books.AsNoTracking()
            .Include(item => item.Titles)
            .SingleAsync(item => item.Id == copyResult.BookId);
        Assert.Equal("A snapshot description", copiedAfterUnlist.Description);
        Assert.Contains(copiedAfterUnlist.Titles, title => title.Title == "Guimi Zhi Zhu");
    }

    [Fact]
    public async Task Unlist_ShouldNotDemoteIdentitiesThatWereAlreadyPublic()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var author = TestData.Author("Public Author", database.UserId, true);
        var tag = TestData.Tag(database.UserId, "Public Tag");
        tag.IsGlobal = true;
        var book = TestData.Book(database.UserId, "Public Metadata Book", author);
        book.BookTags.Add(new BookTag { Book = book, Tag = tag });
        TestData.MakePublishable(book);
        context.AddRange(author, tag, book);
        await context.SaveChangesAsync();

        var service = CreateService(context, new TestUser(database.UserId), new NoopStorage(), new NoopCache());
        var snapshot = await service.PublishAsync(book.Id, CancellationToken.None);
        await service.UnlistAsync(snapshot.Id, CancellationToken.None);
        context.ChangeTracker.Clear();

        Assert.True((await context.Authors.FindAsync(author.Id))!.IsPublic);
        Assert.True((await context.Tags.FindAsync(tag.Id))!.IsGlobal);
        Assert.False(await context.BookShareAuthorPromotions.AnyAsync());
        Assert.False(await context.BookShareTagPromotions.AnyAsync());
    }

    [Fact]
    public async Task DeleteAccount_ShouldUnlistSnapshotsAndKeepPrivateCopiesForOtherLibraries()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var targetId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var otherId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        context.Users.AddRange(
            new User { Id = targetId, UserName = "target", NormalizedUserName = "TARGET" },
            new User { Id = otherId, UserName = "copy-owner", NormalizedUserName = "COPY-OWNER" });
        var author = TestData.Author("Snapshot Owner", targetId, false);
        var tag = TestData.Tag(targetId, "Shared by snapshot");
        var sourceBook = TestData.Book(targetId, "Account deletion snapshot", author);
        sourceBook.BookTags.Add(new BookTag { Book = sourceBook, Tag = tag });
        TestData.MakePublishable(sourceBook);
        context.AddRange(author, tag, sourceBook);
        await context.SaveChangesAsync();

        var cache = new NoopCache();
        var storage = new NoopStorage();
        var targetPublicBooks = CreateService(context, new TestUser(targetId), storage, cache);
        var snapshot = await targetPublicBooks.PublishAsync(sourceBook.Id, CancellationToken.None);
        var otherPublicBooks = CreateService(context, new TestUser(otherId), storage, cache);
        var copiedBookId = (await otherPublicBooks.CopyAsync(snapshot.Id, CancellationToken.None)).BookId;

        var adminPublicBooks = CreateService(context, new TestUser(database.UserId), storage, cache);
        var accountService = new AdminAccountService(
            context,
            new AdminLibraryService(context, storage, cache, adminPublicBooks),
            new AuthorLifecycleService(context, cache));
        await accountService.DeleteAsync(targetId, database.UserId, CancellationToken.None);
        context.ChangeTracker.Clear();

        Assert.False(await context.Users.AnyAsync(item => item.Id == targetId));
        Assert.False(await context.PublicBookSnapshots.AnyAsync());
        Assert.True(await context.Books.AnyAsync(item => item.Id == copiedBookId && item.OwnerId == otherId));
        Assert.True(await context.Authors.AnyAsync(item => item.OwnerId == otherId && !item.IsPublic));
        Assert.True(await context.Tags.AnyAsync(item => item.OwnerId == otherId && !item.IsGlobal));
        Assert.False(await context.Authors.AnyAsync(item => item.OwnerId == targetId));
        Assert.False(await context.Tags.AnyAsync(item => item.OwnerId == targetId));
    }

    private static PublicBookService CreateService(
        Contexts.ApplicationDbContext context,
        IUser user,
        IBookCoverStorage storage,
        IBookListCacheInvalidator cache)
    {
        return new PublicBookService(context, user, storage, new AuthorLifecycleService(context, cache), cache,
            new StorageCleanupQueue(context, TimeProvider.System));
    }

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
            string? contentType, CancellationToken cancellationToken) => Task.FromResult(new BookCoverStoredFiles(
                new BookCoverStoredVariant($"{ownerId:N}/{bookId:N}.jpg", "image/jpeg", 1, 10, 20),
                new BookCoverStoredVariant($"{ownerId:N}/{bookId:N}.thumb.jpg", "image/jpeg", 1, 5, 10)));

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream([1], false));

        public Task DeleteIfExistsAsync(string? storagePath, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
