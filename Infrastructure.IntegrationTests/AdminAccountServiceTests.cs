namespace Infrastructure.IntegrationTests;

using Application.Common.Interfaces;
using Domain.Associations;
using Domain.Exceptions;
using Identity;
using Microsoft.EntityFrameworkCore;
using Services;
using TestSupport;

public sealed class AdminAccountServiceTests
{
    [Fact]
    public async Task DeleteAsync_ShouldRemoveAccountAndAllExclusiveLibraryData()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var targetId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        context.Users.Add(new User
        {
            Id = targetId,
            UserName = "target",
            NormalizedUserName = "TARGET",
            Email = "target@example.com",
            NormalizedEmail = "TARGET@EXAMPLE.COM"
        });
        var author = TestData.Author("Owned Author", targetId, false);
        author.CreatedBy = targetId;
        var tag = TestData.Tag(targetId, "private");
        var book = TestData.Book(targetId, "Owned Book", author);
        book.BookTags.Add(new BookTag { Book = book, Tag = tag });
        context.AddRange(author, tag, book);
        await context.SaveChangesAsync();

        var libraryService = new AdminLibraryService(context, new NoopStorage(), new NoopCache());
        var lifecycle = new AuthorLifecycleService(context, new NoopCache());
        var service = new AdminAccountService(context, libraryService, lifecycle);
        var result = await service.DeleteAsync(targetId, database.UserId, CancellationToken.None);

        Assert.Equal(1, result.DeletedBooks);
        Assert.Equal(1, result.DeletedTags);
        Assert.Equal(1, result.DeletedAuthors);
        Assert.False(await context.Users.AnyAsync(user => user.Id == targetId));
        Assert.False(await context.Books.AnyAsync(item => item.OwnerId == targetId));
        Assert.False(await context.Tags.AnyAsync(item => item.OwnerId == targetId));
        Assert.False(await context.Authors.AnyAsync(item => item.Id == author.Id));
    }

    [Fact]
    public async Task DeleteAsync_ShouldRejectCurrentAdministrator()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var service = new AdminAccountService(
            context,
            new AdminLibraryService(context, new NoopStorage(), new NoopCache()),
            new AuthorLifecycleService(context, new NoopCache()));

        await Assert.ThrowsAsync<CannotDeleteCurrentAccountException>(() =>
            service.DeleteAsync(database.UserId, database.UserId, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_ShouldLocalizePublicAuthorsUsedByOtherAccounts()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var targetId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var otherId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        context.Users.AddRange(
            new User { Id = targetId, UserName = "target", NormalizedUserName = "TARGET" },
            new User { Id = otherId, UserName = "other", NormalizedUserName = "OTHER" });
        var publicAuthor = TestData.Author("Shared identity", targetId, true);
        var otherBook = TestData.Book(otherId, "Other library book", publicAuthor);
        context.AddRange(publicAuthor, otherBook);
        await context.SaveChangesAsync();

        var cache = new NoopCache();
        var service = new AdminAccountService(
            context,
            new AdminLibraryService(context, new NoopStorage(), cache),
            new AuthorLifecycleService(context, cache));
        await service.DeleteAsync(targetId, database.UserId, CancellationToken.None);
        context.ChangeTracker.Clear();

        Assert.False(await context.Authors.AnyAsync(item => item.Id == publicAuthor.Id));
        var copy = await context.Authors.SingleAsync();
        Assert.False(copy.IsPublic);
        Assert.Equal(otherId, copy.OwnerId);
        Assert.Equal(copy.Id, (await context.Books.SingleAsync()).AuthorId);
    }

    private sealed class NoopCache : IBookListCacheInvalidator
    {
        public Task InvalidateBooksAsync(Guid ownerId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NoopStorage : IBookCoverStorage
    {
        public Task<BookCoverStoredFiles> SaveAsync(Guid ownerId, Guid bookId, Stream content, string fileName,
            string? contentType, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task DeleteIfExistsAsync(string? storagePath, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
