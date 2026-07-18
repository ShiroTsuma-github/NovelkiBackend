namespace Infrastructure.IntegrationTests;

using Application.Common.Interfaces;
using Domain.Entities;
using Identity;
using Microsoft.EntityFrameworkCore;
using Services;
using TestSupport;

public sealed class AuthorLifecycleServiceTests
{
    [Fact]
    public async Task SetVisibilityAsync_ShouldLocalizeOtherUsersBooksWhenPublicAuthorBecomesPrivate()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var otherOwnerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        context.Users.Add(new User { Id = otherOwnerId, UserName = "other", NormalizedUserName = "OTHER" });
        var author = TestData.Author("Er Gen", database.UserId, true);
        author.Names.Add(new AuthorName
        {
            Name = "Ergen", NormalizedName = "ERGEN", IsPrimary = false, Source = "Test"
        });
        var ownerBook = TestData.Book(database.UserId, "Owner book", author);
        var otherBook = TestData.Book(otherOwnerId, "Other book", author);
        context.AddRange(author, ownerBook, otherBook);
        await context.SaveChangesAsync();

        var service = new AuthorLifecycleService(context, new NoopCache());
        await service.SetVisibilityAsync(author.Id, database.UserId, false, false, CancellationToken.None);
        context.ChangeTracker.Clear();

        var original = await context.Authors.Include(item => item.Names).SingleAsync(item => item.Id == author.Id);
        var localized = await context.Authors.Include(item => item.Names)
            .SingleAsync(item => item.OwnerId == otherOwnerId);
        Assert.False(original.IsPublic);
        Assert.Equal(database.UserId, original.OwnerId);
        Assert.Equal(author.Id, (await context.Books.SingleAsync(book => book.Id == ownerBook.Id)).AuthorId);
        Assert.Equal(localized.Id, (await context.Books.SingleAsync(book => book.Id == otherBook.Id)).AuthorId);
        Assert.Contains(localized.Names, name => name.Name == "Ergen");
    }

    [Fact]
    public async Task DeleteAsync_ShouldReplacePublicAuthorWithPrivateCopyForEveryUsingOwner()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var otherOwnerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        context.Users.Add(new User { Id = otherOwnerId, UserName = "other", NormalizedUserName = "OTHER" });
        var author = TestData.Author("Toika", database.UserId, true);
        var otherBook = TestData.Book(otherOwnerId, "Shared book", author);
        context.AddRange(author, otherBook);
        await context.SaveChangesAsync();

        var service = new AuthorLifecycleService(context, new NoopCache());
        await service.DeleteAsync(author.Id, database.UserId, false, CancellationToken.None);
        context.ChangeTracker.Clear();

        Assert.False(await context.Authors.AnyAsync(item => item.Id == author.Id));
        var copy = await context.Authors.SingleAsync();
        Assert.False(copy.IsPublic);
        Assert.Equal(otherOwnerId, copy.OwnerId);
        Assert.Equal(copy.Id, (await context.Books.SingleAsync()).AuthorId);
    }

    private sealed class NoopCache : IBookListCacheInvalidator
    {
        public Task InvalidateBooksAsync(Guid ownerId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
