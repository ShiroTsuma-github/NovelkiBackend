using Application.Common.Interfaces;
using Domain.Associations;
using Infrastructure.IntegrationTests.TestSupport;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests;

using Contexts;
using Domain.Entities;

public class AdminLibraryServiceTests
{
    [Fact]
    public async Task DeleteAllBooksForOwnerAsync_ShouldDeleteOnlyOwnersLibraryAndCleanupUnusedRelations()
    {
        using var database = new SqliteTestDatabase();
        await using ApplicationDbContext context = database.CreateContext();
        var otherOwnerId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        context.Users.Add(new Identity.User { Id = otherOwnerId, UserName = "other", NormalizedUserName = "OTHER" });

        Author sharedAuthor = TestData.Author("Shared Author");
        Author ownedOnlyAuthor = TestData.Author("Owned Only Author");
        Tag ownerTag = TestData.Tag(database.UserId, "favorite");
        Tag otherTag = TestData.Tag(otherOwnerId, "favorite");
        context.Authors.AddRange(sharedAuthor, ownedOnlyAuthor);
        context.Tags.AddRange(ownerTag, otherTag);

        Book ownedBook = TestData.Book(database.UserId, "Owned Book", ownedOnlyAuthor);
        ownedBook.BookTags.Add(new BookTag { Book = ownedBook, Tag = ownerTag });
        Book ownedBookWithSharedAuthor = TestData.Book(database.UserId, "Owned Book 2", sharedAuthor);
        Book otherBook = TestData.Book(otherOwnerId, "Other Book", sharedAuthor);
        otherBook.BookTags.Add(new BookTag { Book = otherBook, Tag = otherTag });

        context.Books.AddRange(ownedBook, ownedBookWithSharedAuthor, otherBook);
        await context.SaveChangesAsync();

        var cacheInvalidator = new TrackingCacheInvalidator();
        var storage = new TrackingBookCoverStorage();
        context.BookCovers.AddRange(
            new Domain.Entities.BookCover
            {
                Id = Guid.Empty, BookId = ownedBook.Id, StoragePath = "owned/one.jpg", MimeType = "image/jpeg"
            },
            new Domain.Entities.BookCover
            {
                Id = Guid.Empty,
                BookId = ownedBookWithSharedAuthor.Id,
                StoragePath = "owned/two.jpg",
                MimeType = "image/jpeg"
            },
            new Domain.Entities.BookCover
            {
                Id = Guid.Empty, BookId = otherBook.Id, StoragePath = "other/keep.jpg", MimeType = "image/jpeg"
            });
        await context.SaveChangesAsync();

        var service = new AdminLibraryService(context, storage, cacheInvalidator);

        AdminLibraryPurgeResult result =
            await service.DeleteAllBooksForOwnerAsync(database.UserId, CancellationToken.None);

        Assert.Equal(2, result.DeletedBooks);
        Assert.Equal(1, result.DeletedAuthors);
        Assert.Equal(1, result.DeletedTags);
        Assert.Equal(database.UserId, cacheInvalidator.InvalidatedOwnerId);
        Assert.Equal(["owned/one.jpg", "owned/two.jpg"], storage.DeletedPaths.OrderBy(path => path).ToArray());

        Assert.Single(await context.Books.ToListAsync());
        Assert.Equal(otherOwnerId, (await context.Books.SingleAsync()).OwnerId);
        Assert.DoesNotContain(await context.Authors.Select(a => a.PrimaryName).ToListAsync(),
            name => name == "Owned Only Author");
        Assert.Contains(await context.Authors.Select(a => a.PrimaryName).ToListAsync(),
            name => name == "Shared Author");
        Assert.DoesNotContain(
            await context.Tags.Where(t => t.OwnerId == database.UserId).Select(t => t.Name).ToListAsync(),
            name => name == "favorite");
    }

    [Fact]
    public async Task DeleteAllBooksForOwnerAsync_ShouldInvalidateCacheEvenWhenOwnerHasNoBooks()
    {
        using var database = new SqliteTestDatabase();
        await using ApplicationDbContext context = database.CreateContext();
        var cacheInvalidator = new TrackingCacheInvalidator();
        var storage = new TrackingBookCoverStorage();
        var service = new AdminLibraryService(context, storage, cacheInvalidator);

        AdminLibraryPurgeResult result =
            await service.DeleteAllBooksForOwnerAsync(database.UserId, CancellationToken.None);

        Assert.Equal(new AdminLibraryPurgeResult(0, 0, 0), result);
        Assert.Equal(database.UserId, cacheInvalidator.InvalidatedOwnerId);
        Assert.Empty(storage.DeletedPaths);
    }

    private sealed class TrackingCacheInvalidator : IBookListCacheInvalidator
    {
        public Guid? InvalidatedOwnerId { get; private set; }

        public Task InvalidateBooksAsync(Guid ownerId, CancellationToken cancellationToken)
        {
            InvalidatedOwnerId = ownerId;
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingBookCoverStorage : IBookCoverStorage
    {
        public List<string> DeletedPaths { get; } = [];

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
            if (!string.IsNullOrWhiteSpace(storagePath))
            {
                DeletedPaths.Add(storagePath);
            }

            return Task.CompletedTask;
        }
    }
}
