using Domain.Entities;
using Infrastructure.IntegrationTests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests;

using Contexts;

public class ApplicationDbContextTests
{
    [Fact]
    public async Task EnsureCreated_ShouldSeedSystemDictionaries()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();

        Assert.True(await context.ContentTypes.AnyAsync(t => t.Slug == "novel"));
        Assert.True(await context.Statuses.AnyAsync(s => s.Slug == "reading"));
    }

    [Fact]
    public async Task SaveChanges_ShouldPopulateAuditFields()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var genre = TestData.Genre("Fantasy");

        context.Genres.Add(genre);
        await context.SaveChangesAsync();

        Assert.Equal(database.UserId, genre.CreatedBy);
        Assert.Equal(database.UserId, genre.LastModifiedBy);
        Assert.True(genre.Created > DateTimeOffset.MinValue);
        Assert.True(genre.LastModified > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task AuthorNormalizedPrimaryName_ShouldBeUnique()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        context.Authors.Add(TestData.Author("Toika"));
        context.Authors.Add(TestData.Author("Toika"));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task TagNormalizedName_ShouldBeUniquePerOwner()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        context.Tags.Add(TestData.Tag(database.UserId, "favorite"));
        context.Tags.Add(TestData.Tag(database.UserId, "favorite"));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task SameTagName_ShouldBeAllowedForDifferentOwners()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var otherOwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        context.Users.Add(new Identity.User { Id = otherOwnerId, UserName = "other", NormalizedUserName = "OTHER" });
        context.Tags.Add(TestData.Tag(database.UserId, "favorite"));
        context.Tags.Add(TestData.Tag(otherOwnerId, "favorite"));

        await context.SaveChangesAsync();

        Assert.Equal(2, await context.Tags.CountAsync());
    }

    [Fact]
    public async Task DeletingBook_ShouldCascadeToTitlesLinksAndProgress()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var book = await TestData.AddBookWithRelationsAsync(context, database.UserId);

        context.Books.Remove(book);
        await context.SaveChangesAsync();

        Assert.Empty(await context.BookTitles.ToListAsync());
        Assert.Empty(await context.BookLinks.ToListAsync());
        Assert.Empty(await context.BookProgressHistory.ToListAsync());
        Assert.Empty(await context.BookCovers.ToListAsync());
    }

    [Fact]
    public async Task DeletingUsedAuthor_ShouldBeRestricted()
    {
        using var database = new SqliteTestDatabase();
        Guid authorId;
        await using (var arrangeContext = database.CreateContext())
        {
            var existingAuthor = TestData.Author("Toika");
            await TestData.AddBookAsync(arrangeContext, database.UserId, "Novel", existingAuthor);
            authorId = existingAuthor.Id;
        }

        await using var context = database.CreateContext();
        var author = await context.Authors.SingleAsync(a => a.Id == authorId);
        context.Authors.Remove(author);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
