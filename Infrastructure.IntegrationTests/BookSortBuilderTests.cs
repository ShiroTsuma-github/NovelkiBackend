using Domain.Entities;
using Infrastructure.IntegrationTests.TestSupport;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests;

public class BookSortBuilderTests
{
    [Theory]
    [InlineData("created", true)]
    [InlineData("createdAt", true)]
    [InlineData("lastModified", true)]
    [InlineData("updatedAt", true)]
    [InlineData("title", false)]
    [InlineData(null, true)]
    public void ShouldSortDateOnClient_ShouldOnlyUseClientSortForSqliteDateFields(string? sortBy, bool expected)
    {
        using var database = new SqliteTestDatabase();
        using var context = database.CreateContext();
        var sortBuilder = new BookSortBuilder(context);

        Assert.Equal(expected, sortBuilder.ShouldSortDateOnClient(sortBy));
    }

    [Fact]
    public async Task ApplySortingAsync_ShouldSortByAuthorAndTitle()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var betaAuthor = TestData.Author("Beta Author");
        var alphaAuthor = TestData.Author("Alpha Author");
        context.Authors.AddRange(betaAuthor, alphaAuthor);
        var noAuthor = TestData.Book(database.UserId, "No Author");
        var alpha = TestData.Book(database.UserId, "Alpha Book", alphaAuthor);
        var beta = TestData.Book(database.UserId, "Beta Book", betaAuthor);
        context.Books.AddRange(beta, noAuthor, alpha);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var sortBuilder = new BookSortBuilder(context);

        var ascending = await (await sortBuilder.ApplySortingAsync(context.Books.AsNoTracking(), "author", "asc", CancellationToken.None))
            .Select(book => book.Id)
            .ToListAsync();
        var descending = await (await sortBuilder.ApplySortingAsync(context.Books.AsNoTracking(), "author", "desc", CancellationToken.None))
            .Select(book => book.Id)
            .ToListAsync();

        Assert.Equal([noAuthor.Id, alpha.Id, beta.Id], ascending);
        Assert.Equal([beta.Id, alpha.Id, noAuthor.Id], descending);
    }

    [Fact]
    public async Task ToSortedPageAsync_ShouldSortDateFieldsOnClientAndApplyPaging()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var first = await TestData.AddBookAsync(context, database.UserId, "First");
        var second = await TestData.AddBookAsync(context, database.UserId, "Second");
        var third = await TestData.AddBookAsync(context, database.UserId, "Third");
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE Books SET Created = {DateTimeOffset.Parse("2026-01-01T00:00:00+00:00")} WHERE Id = {first.Id}");
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE Books SET Created = {DateTimeOffset.Parse("2026-01-03T00:00:00+00:00")} WHERE Id = {second.Id}");
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE Books SET Created = {DateTimeOffset.Parse("2026-01-02T00:00:00+00:00")} WHERE Id = {third.Id}");
        context.ChangeTracker.Clear();
        var sortBuilder = new BookSortBuilder(context);

        var page = (await sortBuilder.ToSortedPageAsync(context.Books.AsNoTracking(), 1, 1, "created", "desc", CancellationToken.None)).ToList();

        Assert.Single(page);
        Assert.Equal(third.Id, page[0].Id);
    }

    [Fact]
    public async Task GetNextCycleSortDirectionAsync_ShouldReturnNullWhenNoCycleValuesExist()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var sortBuilder = new BookSortBuilder(context);

        var nextType = await sortBuilder.GetNextCycleSortDirectionAsync(context.Books.AsNoTracking(), "type", null, CancellationToken.None);
        var nextTitle = await sortBuilder.GetNextCycleSortDirectionAsync(context.Books.AsNoTracking(), "title", null, CancellationToken.None);

        Assert.Null(nextType);
        Assert.Null(nextTitle);
    }

    [Fact]
    public async Task ToSortedPageAsync_ShouldFallbackToLastModifiedForUnknownSort()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var older = await TestData.AddBookAsync(context, database.UserId, "Older");
        var newer = await TestData.AddBookAsync(context, database.UserId, "Newer");
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE Books SET LastModified = {DateTimeOffset.Parse("2026-01-01T00:00:00+00:00")} WHERE Id = {older.Id}");
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE Books SET LastModified = {DateTimeOffset.Parse("2026-01-02T00:00:00+00:00")} WHERE Id = {newer.Id}");
        context.ChangeTracker.Clear();
        var sortBuilder = new BookSortBuilder(context);

        var sorted = (await sortBuilder.ToSortedPageAsync(context.Books.AsNoTracking(), 0, 10, "not-a-sort", null, CancellationToken.None))
            .Select(book => book.Id)
            .ToList();

        Assert.Equal([newer.Id, older.Id], sorted);
    }

    [Fact]
    public async Task ApplySortingAsync_ShouldKeepNullableNumericFieldsLastInBothDirections()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var low = await TestData.AddBookAsync(context, database.UserId, "Low");
        low.Rating = 3;
        low.Priority = 1;
        low.CurrentChapterNumber = 10;
        low.TotalChapters = 50;
        var high = await TestData.AddBookAsync(context, database.UserId, "High");
        high.Rating = 8;
        high.Priority = 4;
        high.CurrentChapterNumber = 40;
        high.TotalChapters = 200;
        var unknown = await TestData.AddBookAsync(context, database.UserId, "Unknown");
        unknown.Rating = null;
        unknown.Priority = null;
        unknown.CurrentChapterNumber = null;
        unknown.TotalChapters = null;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var sortBuilder = new BookSortBuilder(context);

        async Task<Guid[]> SortIds(string sortBy, string sortDirection)
        {
            return await (await sortBuilder.ApplySortingAsync(context.Books.AsNoTracking(), sortBy, sortDirection, CancellationToken.None))
                .Select(book => book.Id)
                .ToArrayAsync();
        }

        Assert.Equal([low.Id, high.Id, unknown.Id], await SortIds("rating", "asc"));
        Assert.Equal([high.Id, low.Id, unknown.Id], await SortIds("rating", "desc"));
        Assert.Equal([low.Id, high.Id, unknown.Id], await SortIds("priority", "asc"));
        Assert.Equal([high.Id, low.Id, unknown.Id], await SortIds("priority", "desc"));
        Assert.Equal([low.Id, high.Id, unknown.Id], await SortIds("progress", "asc"));
        Assert.Equal([high.Id, low.Id, unknown.Id], await SortIds("progress", "desc"));
        Assert.Equal([low.Id, high.Id, unknown.Id], await SortIds("chapters", "asc"));
        Assert.Equal([high.Id, low.Id, unknown.Id], await SortIds("chapters", "desc"));
    }
}
