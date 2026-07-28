namespace Infrastructure.IntegrationTests;

using Application.Common;
using Domain.Associations;
using Domain.Entities;
using Infrastructure.Identity;
using Persistence;
using TestSupport;

public sealed class BookSearchSuggestionQueryServiceTests
{
    [Fact]
    public async Task AuthorSuggestions_ShouldRankExactBeforeFrequencyAndScopeCountsToOwner()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var otherOwnerId = Guid.NewGuid();
        context.Users.Add(User(otherOwnerId));
        var exactAuthor = TestData.Author("Aro");
        var frequentAuthor = TestData.Author("Arou Rei");
        frequentAuthor.Names.Add(new AuthorName
        {
            Name = "Silver Pen",
            NormalizedName = MappingExtensions.NormalizeName("Silver Pen"),
            IsPrimary = false,
            Source = "Test"
        });
        context.Authors.AddRange(exactAuthor, frequentAuthor);
        context.Books.Add(TestData.Book(database.UserId, "Exact Book", exactAuthor));
        context.Books.AddRange(
            TestData.Book(database.UserId, "Frequent One", frequentAuthor),
            TestData.Book(database.UserId, "Frequent Two", frequentAuthor),
            TestData.Book(database.UserId, "Frequent Three", frequentAuthor));
        context.Books.Add(TestData.Book(otherOwnerId, "Other Owner Book", exactAuthor));
        await context.SaveChangesAsync();
        var service = new BookSearchSuggestionQueryService(context);

        var result = await service.GetSuggestionsAsync(
            database.UserId,
            BookSearchSuggestionFields.Author,
            "aro",
            10,
            CancellationToken.None);
        var aliasResult = await service.GetSuggestionsAsync(
            database.UserId,
            BookSearchSuggestionFields.Author,
            "silver pen",
            10,
            CancellationToken.None);

        Assert.Collection(
            result,
            suggestion =>
            {
                Assert.Equal("Aro", suggestion.Value);
                Assert.Equal(1, suggestion.Count);
                Assert.True(suggestion.IsExact);
            },
            suggestion =>
            {
                Assert.Equal("Arou Rei", suggestion.Value);
                Assert.Equal(3, suggestion.Count);
                Assert.False(suggestion.IsExact);
            });
        Assert.Equal("Arou Rei", Assert.Single(aliasResult).Value);
        Assert.True(Assert.Single(aliasResult).IsExact);
    }

    [Fact]
    public async Task Suggestions_ShouldCoverBookRelations()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var genre = TestData.Genre("Fantasy");
        var tag = TestData.Tag(database.UserId, "favorite");
        var book = TestData.Book(database.UserId, "Lord of Mysteries");
        book.BookGenres.Add(new BookGenre { Book = book, Genre = genre });
        book.BookTags.Add(new BookTag { Book = book, Tag = tag });
        context.Books.Add(book);
        await context.SaveChangesAsync();
        var service = new BookSearchSuggestionQueryService(context);

        var tags = await service.GetSuggestionsAsync(
            database.UserId, BookSearchSuggestionFields.Tag, null, 10, CancellationToken.None);
        var genres = await service.GetSuggestionsAsync(
            database.UserId, BookSearchSuggestionFields.Genre, null, 10, CancellationToken.None);
        var statuses = await service.GetSuggestionsAsync(
            database.UserId, BookSearchSuggestionFields.Status, null, 10, CancellationToken.None);
        var types = await service.GetSuggestionsAsync(
            database.UserId, BookSearchSuggestionFields.Type, null, 10, CancellationToken.None);

        Assert.Equal(("favorite", 1), (Assert.Single(tags).Value, Assert.Single(tags).Count));
        Assert.Equal(("Fantasy", 1), (Assert.Single(genres).Value, Assert.Single(genres).Count));
        Assert.Equal(("Reading", 1), (Assert.Single(statuses).Value, Assert.Single(statuses).Count));
        Assert.Equal(("Novel", 1), (Assert.Single(types).Value, Assert.Single(types).Count));
    }

    private static User User(Guid id)
    {
        return new User
        {
            Id = id,
            UserName = $"reader-{id:N}",
            NormalizedUserName = $"READER-{id:N}",
            Email = $"{id:N}@example.com",
            NormalizedEmail = $"{id:N}@EXAMPLE.COM"
        };
    }
}
