namespace Infrastructure.IntegrationTests;

using Application.Common;
using Domain.Associations;
using Domain.Entities;
using Infrastructure.Contexts;
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
            null,
            10,
            CancellationToken.None);
        var aliasResult = await service.GetSuggestionsAsync(
            database.UserId,
            BookSearchSuggestionFields.Author,
            "silver pen",
            null,
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
            database.UserId, BookSearchSuggestionFields.Tag, null, null, 10, CancellationToken.None);
        var genres = await service.GetSuggestionsAsync(
            database.UserId, BookSearchSuggestionFields.Genre, null, null, 10, CancellationToken.None);
        var statuses = await service.GetSuggestionsAsync(
            database.UserId, BookSearchSuggestionFields.Status, null, null, 10, CancellationToken.None);
        var types = await service.GetSuggestionsAsync(
            database.UserId, BookSearchSuggestionFields.Type, null, null, 10, CancellationToken.None);

        Assert.Equal(("favorite", 1), (Assert.Single(tags).Value, Assert.Single(tags).Count));
        Assert.Equal(("Fantasy", 1), (Assert.Single(genres).Value, Assert.Single(genres).Count));
        Assert.Equal(("Reading", 1), (Assert.Single(statuses).Value, Assert.Single(statuses).Count));
        Assert.Equal(("Novel", 1), (Assert.Single(types).Value, Assert.Single(types).Count));
    }

    [Fact]
    public async Task Suggestions_ShouldCountAgainstEvaluatedQueryScope()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        AddBooks(context, database.UserId, "Novel Reading", TestData.NovelTypeId, TestData.ReadingStatusId, 3);
        AddBooks(
            context,
            database.UserId,
            "Novel Completed",
            TestData.NovelTypeId,
            Guid.Parse("20000000-0000-0000-0000-000000000002"),
            2);
        AddBooks(
            context,
            database.UserId,
            "Manga Reading",
            Guid.Parse("10000000-0000-0000-0000-000000000002"),
            TestData.ReadingStatusId,
            4);
        await context.SaveChangesAsync();
        var service = new BookSearchSuggestionQueryService(context);

        var statuses = await service.GetSuggestionsAsync(
            database.UserId,
            BookSearchSuggestionFields.Status,
            "read",
            BookSearchQueryParser.Parse("type:Novel"),
            10,
            CancellationToken.None);
        var types = await service.GetSuggestionsAsync(
            database.UserId,
            BookSearchSuggestionFields.Type,
            "manga",
            BookSearchQueryParser.Parse("status:Reading"),
            10,
            CancellationToken.None);

        Assert.Equal(("Reading", 3), (Assert.Single(statuses).Value, Assert.Single(statuses).Count));
        Assert.Equal(("Manga", 4), (Assert.Single(types).Value, Assert.Single(types).Count));
        Assert.True(Assert.Single(statuses).IsAvailable);
        Assert.True(Assert.Single(types).IsAvailable);
    }

    [Fact]
    public async Task Suggestions_ShouldReturnUnavailableExistingValuesOutsideTheEvaluatedQueryScope()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        AddBooks(
            context,
            database.UserId,
            "Novel Completed",
            TestData.NovelTypeId,
            Guid.Parse("20000000-0000-0000-0000-000000000002"),
            2);
        AddBooks(
            context,
            database.UserId,
            "Manga Reading",
            Guid.Parse("10000000-0000-0000-0000-000000000002"),
            TestData.ReadingStatusId,
            4);
        await context.SaveChangesAsync();
        var service = new BookSearchSuggestionQueryService(context);

        var types = await service.GetSuggestionsAsync(
            database.UserId,
            BookSearchSuggestionFields.Type,
            "manga",
            BookSearchQueryParser.Parse("status:Completed"),
            10,
            CancellationToken.None);

        var type = Assert.Single(types);
        Assert.Equal("Manga", type.Value);
        Assert.Equal(0, type.Count);
        Assert.True(type.IsExact);
        Assert.False(type.IsAvailable);
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

    private static void AddBooks(
        ApplicationDbContext context,
        Guid ownerId,
        string titlePrefix,
        Guid typeId,
        Guid statusId,
        int count)
    {
        for (var index = 1; index <= count; index++)
        {
            var book = TestData.Book(ownerId, $"{titlePrefix} {index}");
            book.ContentTypeId = typeId;
            book.StatusId = statusId;
            context.Books.Add(book);
        }
    }
}
