namespace Infrastructure.IntegrationTests;

using Domain.Associations;
using Infrastructure.IntegrationTests.TestSupport;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

public sealed class GlobalTagServiceTests
{
    [Fact]
    public async Task CreateGlobalTag_ShouldReplacePrivateDuplicatesAndKeepBookLinks()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var privateTag = TestData.Tag(database.UserId, "favorite");
        var book = TestData.Book(database.UserId, "Tagged book");
        book.BookTags.Add(new BookTag { Book = book, Tag = privateTag });
        context.AddRange(privateTag, book);
        await context.SaveChangesAsync();
        var service = new GlobalTagService(context);

        var global = await service.CreateAsync("favorite", "Shared tag", CancellationToken.None);

        Assert.True(global.IsGlobal);
        Assert.Null(global.OwnerId);
        Assert.False(await context.Tags.AnyAsync(tag => !tag.IsGlobal && tag.NormalizedName == "FAVORITE"));
        Assert.True(await context.Set<BookTag>().AnyAsync(link => link.BookId == book.Id && link.TagId == global.Id));
    }

    [Fact]
    public async Task DeleteGlobalTag_ShouldRemoveItsBookLinks()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var service = new GlobalTagService(context);
        var global = await service.CreateAsync("official", null, CancellationToken.None);
        var book = TestData.Book(database.UserId, "Tagged book");
        book.BookTags.Add(new BookTag { Book = book, Tag = global });
        context.Books.Add(book);
        await context.SaveChangesAsync();

        await service.DeleteAsync(global.Id, CancellationToken.None);

        Assert.False(await context.Tags.AnyAsync(tag => tag.Id == global.Id));
        Assert.False(await context.Set<BookTag>().AnyAsync(link => link.TagId == global.Id));
    }
}
