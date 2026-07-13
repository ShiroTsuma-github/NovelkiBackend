using Application.Common;
using Domain.Associations;
using Domain.Entities;
using Domain.Repositories;
using Infrastructure.IntegrationTests.TestSupport;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests;

public class RepositoryTests
{
    [Fact]
    public async Task BookRepository_ShouldScopeListAndGetByOwner()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var otherOwnerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        context.Users.Add(new Infrastructure.Identity.User { Id = otherOwnerId, UserName = "other", NormalizedUserName = "OTHER" });
        await TestData.AddBookAsync(context, database.UserId, "Mine");
        await TestData.AddBookAsync(context, otherOwnerId, "Other");
        var repository = new BookRepository(context);

        var books = (await repository.GetAllAsync(database.UserId, 0, 10, CancellationToken.None)).ToList();
        var count = await repository.GetCountAsync(database.UserId, CancellationToken.None);
        var otherBook = await repository.GetByNameAsync("Other", database.UserId, Guid.Parse("10000000-0000-0000-0000-000000000001"), CancellationToken.None);

        Assert.Single(books);
        Assert.Equal(1, count);
        Assert.Null(otherBook);
    }

    [Fact]
    public async Task BookRepository_ShouldIncludeBookDetails()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var book = await TestData.AddBookWithRelationsAsync(context, database.UserId);
        var repository = new BookRepository(context);

        var result = await repository.GetByIdAsync(book.Id, database.UserId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(result.Author);
        Assert.NotEmpty(result.Titles);
        Assert.NotEmpty(result.BookGenres);
        Assert.NotEmpty(result.BookTags);
        Assert.NotEmpty(result.Links);
        Assert.NotEmpty(result.ProgressHistory);
        Assert.NotNull(result.Cover);
        Assert.Equal(BookCoverStatus.Found, result.Cover.Status);
        Assert.Equal("Novel", result.ContentType.Name);
        Assert.Equal("Reading", result.Status.Name);
    }

    [Fact]
    public async Task BookRepository_ShouldSearchByCustomCriteria()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var matching = await TestData.AddBookWithRelationsAsync(context, database.UserId);
        matching.Rating = 9;
        matching.CurrentChapterNumber = 42;
        await TestData.AddBookAsync(context, database.UserId, "Unrelated Title");
        await context.SaveChangesAsync();
        var repository = new BookRepository(context);
        var criteria = new BookSearchCriteria(
            new[] { "returnee" },
            new[] { new BookSearchFieldFilter(BookSearchField.Tag, "favorite"), new BookSearchFieldFilter(BookSearchField.Author, "toi") },
            new[] { new BookSearchNumberFilter(BookSearchNumberField.Rating, BookSearchOperator.GreaterThanOrEqual, 8) });

        var books = (await repository.SearchAsync(database.UserId, criteria, 0, 10, CancellationToken.None)).ToList();
        var count = await repository.GetSearchCountAsync(database.UserId, criteria, CancellationToken.None);

        Assert.Single(books);
        Assert.Equal(matching.Id, books[0].Id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task BookRepository_ShouldSearchByWildcardTitleCriteria()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var matching = await TestData.AddBookAsync(context, database.UserId, "I Shall Seal the Heavens");
        await TestData.AddBookAsync(context, database.UserId, "Lord of Mysteries");
        var repository = new BookRepository(context);
        var criteria = new BookSearchCriteria(
            Array.Empty<string>(),
            new[] { new BookSearchFieldFilter(BookSearchField.Title, "i sha*") },
            Array.Empty<BookSearchNumberFilter>());

        var books = (await repository.SearchAsync(database.UserId, criteria, 0, 10, CancellationToken.None)).ToList();

        Assert.Single(books);
        Assert.Equal(matching.Id, books[0].Id);
    }

    [Fact]
    public async Task BookRepository_ShouldSortByLastModifiedDescendingByDefault()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var older = await TestData.AddBookAsync(context, database.UserId, "Older");
        var newer = await TestData.AddBookAsync(context, database.UserId, "Newer");
        var olderTimestamp = DateTimeOffset.Parse("2026-07-01T10:00:00+00:00");
        var newerTimestamp = DateTimeOffset.Parse("2026-07-02T10:00:00+00:00");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Books SET LastModified = {olderTimestamp} WHERE Id = {older.Id}");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Books SET LastModified = {newerTimestamp} WHERE Id = {newer.Id}");
        context.ChangeTracker.Clear();
        var repository = new BookRepository(context);

        var books = (await repository.GetAllAsync(database.UserId, 0, 10, CancellationToken.None)).ToList();

        Assert.Equal(newer.Id, books[0].Id);
        Assert.Equal(older.Id, books[1].Id);
    }

    [Fact]
    public async Task BookRepository_ShouldSortTitlesCaseInsensitivelyAndDeterministically()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var apple = await TestData.AddBookAsync(context, database.UserId, "apple");
        var bananaLower = await TestData.AddBookAsync(context, database.UserId, "banana");
        var bananaUpper = TestData.Book(database.UserId, "Banana");
        bananaUpper.ContentTypeId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        context.Books.Add(bananaUpper);
        await context.SaveChangesAsync();
        var zebra = await TestData.AddBookAsync(context, database.UserId, "Zebra");
        var repository = new BookRepository(context);

        var ascending = (await repository.GetAllAsync(database.UserId, 0, 10, "title", "asc", CancellationToken.None)).ToList();
        var descending = (await repository.GetAllAsync(database.UserId, 0, 10, "title", "desc", CancellationToken.None)).ToList();

        Assert.Equal([apple.Id, bananaUpper.Id, bananaLower.Id, zebra.Id], ascending.Select(book => book.Id));
        Assert.Equal([zebra.Id, bananaLower.Id, bananaUpper.Id, apple.Id], descending.Select(book => book.Id));
    }

    [Fact]
    public async Task BookRepository_ShouldSortStatusAndTypeByDomainOrder()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var novelReading = await TestData.AddBookAsync(context, database.UserId, "Novel Reading");
        var mangaCompleted = await TestData.AddBookAsync(context, database.UserId, "Manga Completed");
        var manhwaPlanToRead = await TestData.AddBookAsync(context, database.UserId, "Manhwa Plan To Read");

        mangaCompleted.ContentTypeId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        mangaCompleted.StatusId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        manhwaPlanToRead.ContentTypeId = Guid.Parse("10000000-0000-0000-0000-000000000003");
        manhwaPlanToRead.StatusId = Guid.Parse("20000000-0000-0000-0000-000000000003");
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new BookRepository(context);
        const string novelTypeName = "Novel";
        const string mangaTypeName = "Manga";
        const string readingStatusName = "Reading";
        const string completedStatusName = "Completed";

        var typeAscending = (await repository.GetAllAsync(database.UserId, 0, 10, "type", novelTypeName, CancellationToken.None)).ToList();
        var typeRotated = (await repository.GetAllAsync(database.UserId, 0, 10, "type", mangaTypeName, CancellationToken.None)).ToList();
        var statusAscending = (await repository.GetAllAsync(database.UserId, 0, 10, "status", readingStatusName, CancellationToken.None)).ToList();
        var statusRotated = (await repository.GetAllAsync(database.UserId, 0, 10, "status", completedStatusName, CancellationToken.None)).ToList();

        Assert.Equal([novelReading.Id, mangaCompleted.Id, manhwaPlanToRead.Id], typeAscending.Select(book => book.Id));
        Assert.Equal([mangaCompleted.Id, manhwaPlanToRead.Id, novelReading.Id], typeRotated.Select(book => book.Id));
        Assert.Equal([novelReading.Id, mangaCompleted.Id, manhwaPlanToRead.Id], statusAscending.Select(book => book.Id));
        Assert.Equal([mangaCompleted.Id, manhwaPlanToRead.Id, novelReading.Id], statusRotated.Select(book => book.Id));
    }

    [Fact]
    public async Task BookRepository_ShouldResolveNextCycleDirectionUsingOnlyAvailableValues()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var novelReading = await TestData.AddBookAsync(context, database.UserId, "Novel Reading");
        var mangaCompleted = await TestData.AddBookAsync(context, database.UserId, "Manga Completed");

        mangaCompleted.ContentTypeId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        mangaCompleted.StatusId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        await context.SaveChangesAsync();

        var repository = new BookRepository(context);
        const string missingTypeName = "Manhwa";
        const string missingStatusName = "Plan To Read";

        var nextType = await repository.GetNextCycleSortDirectionAsync(database.UserId, BookSearchCriteria.Empty, "type", missingTypeName, CancellationToken.None);
        var nextStatus = await repository.GetNextCycleSortDirectionAsync(database.UserId, BookSearchCriteria.Empty, "status", missingStatusName, CancellationToken.None);

        Assert.Equal("Novel", nextType);
        Assert.Equal("Reading", nextStatus);
    }

    [Fact]
    public async Task BookRepository_ShouldBuildSummaryForFilteredOwnerBooks()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var reading = await TestData.AddBookAsync(context, database.UserId, "Reading Rated");
        var completed = await TestData.AddBookAsync(context, database.UserId, "Completed Rated");
        var unrated = await TestData.AddBookAsync(context, database.UserId, "Reading Unrated");

        reading.Rating = 9;
        completed.Rating = 7;
        completed.StatusId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        unrated.Rating = null;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new BookRepository(context);
        var criteria = new BookSearchCriteria(
            Array.Empty<string>(),
            Array.Empty<BookSearchFieldFilter>(),
            [new BookSearchNumberFilter(BookSearchNumberField.Rating, BookSearchOperator.GreaterThanOrEqual, 1)]);

        var summary = await repository.GetSummaryAsync(database.UserId, criteria, CancellationToken.None);

        Assert.Equal(2, summary.TotalBooks);
        Assert.Equal(2, summary.RatedBooks);
        Assert.Equal(8.0, summary.AverageRating);
        Assert.Equal(0, summary.CurrentChapters);
        Assert.Equal(0, summary.BooksWithKnownCurrentChapter);
        Assert.Equal(["Completed", "Reading"], summary.StatusCounts.Select(item => item.Status));
        Assert.Equal([1, 1], summary.StatusCounts.Select(item => item.Count));
        Assert.Equal("Novel", summary.TypeCounts[0].Type);
        Assert.Equal(2, summary.TypeCounts[0].BookCount);
        Assert.Empty(summary.GenreCounts);
        Assert.Equal([7, 9], summary.RatingCounts.Select(item => item.Rating));
    }

    [Fact]
    public async Task BookRepository_ShouldFilterAdminSearchWithSortingAndPaging()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var otherOwnerId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        context.Users.Add(new Infrastructure.Identity.User
        {
            Id = otherOwnerId,
            UserName = "other-admin-search",
            NormalizedUserName = "OTHER-ADMIN-SEARCH"
        });

        var firstMatch = await TestData.AddBookAsync(context, database.UserId, "Lord of Mysteries");
        var secondMatch = await TestData.AddBookAsync(context, otherOwnerId, "Lord of the Secrets");
        await TestData.AddBookAsync(context, database.UserId, "Shadow Slave");
        var repository = new BookRepository(context);
        var criteria = BookSearchQueryParser.Parse("title:Lord");

        var firstPage = (await repository.SearchAdminListAsync(criteria, 0, 1, "title", "asc", CancellationToken.None)).ToList();
        var secondPage = (await repository.SearchAdminListAsync(criteria, 1, 1, "title", "asc", CancellationToken.None)).ToList();
        var count = await repository.GetSearchCountAsync(criteria, CancellationToken.None);

        Assert.Equal(2, count);
        Assert.Single(firstPage);
        Assert.Single(secondPage);
        Assert.Equal(firstMatch.Id, firstPage[0].Id);
        Assert.Equal(secondMatch.Id, secondPage[0].Id);
    }

    [Fact]
    public async Task BookRepository_ShouldReturnNoAdminSearchResultsForMissingQuery()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        await TestData.AddBookAsync(context, database.UserId, "Lord of Mysteries");
        var repository = new BookRepository(context);
        var criteria = BookSearchQueryParser.Parse("title:Missing");

        var results = (await repository.SearchAdminListAsync(criteria, 0, 10, "title", "asc", CancellationToken.None)).ToList();
        var count = await repository.GetSearchCountAsync(criteria, CancellationToken.None);

        Assert.Empty(results);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task BookRepository_ShouldReplaceCollectionsAfterDuplicateTitleLookup()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var book = await TestData.AddBookWithRelationsAsync(context, database.UserId);
        var genreId = book.BookGenres.First().GenreId;
        var tagId = book.BookTags.First().TagId;
        var repository = new BookRepository(context);

        var editableBook = await repository.GetForUpdateAsync(book.Id, database.UserId, CancellationToken.None);
        var duplicateCheck = await repository.GetByNameAsync(book.PrimaryTitle, database.UserId, book.ContentTypeId, CancellationToken.None);

        Assert.NotNull(editableBook);
        Assert.NotNull(duplicateCheck);

        await repository.ReplaceEditableCollectionsAsync(
            book.Id,
            new[] { "Updated Title".ToPrimaryTitle() },
            Array.Empty<BookLink>(),
            new[] { genreId },
            new[] { tagId },
            null,
            CancellationToken.None);
        await repository.SaveAsync(CancellationToken.None);

        context.ChangeTracker.Clear();
        var savedBook = await context.Books
            .Include(b => b.BookGenres)
            .Include(b => b.BookTags)
            .Include(b => b.Titles)
            .FirstAsync(b => b.Id == book.Id);

        Assert.Single(savedBook.BookGenres);
        Assert.Single(savedBook.BookTags);
        Assert.Contains(savedBook.Titles, t => t.Title == "Updated Title");
    }

    [Fact]
    public async Task DictionaryRepositories_ShouldReturnDomainOrderedStatusesAndTypes()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var statusRepository = new StatusRepository(context);
        var typeRepository = new TypeRepository(context);

        var statuses = (await statusRepository.GetAllAsync(0, 20, CancellationToken.None)).Select(status => status.Name).ToArray();
        var types = (await typeRepository.GetAllAsync(0, 20, CancellationToken.None)).Select(type => type.Name).ToArray();

        Assert.Equal(["Reading", "Completed", "Plan To Read", "On Hold", "Dropped", "Unknown"], statuses);
        Assert.Equal(["Novel", "Manga", "Manhwa", "Manhua", "Other"], types);
    }

    [Fact]
    public async Task AuthorRepository_ShouldFindAuthorByAlias()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var author = TestData.Author("Er Gen");
        author.Names.Add(new AuthorName
        {
            Name = "Ergen",
            NormalizedName = MappingExtensions.NormalizeName("Ergen"),
            IsPrimary = false,
            Source = "Test"
        });
        context.Authors.Add(author);
        await context.SaveChangesAsync();
        var repository = new AuthorRepository(context);

        var result = await repository.GetByNameAsync("Ergen", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Er Gen", result.PrimaryName);
    }

    [Fact]
    public async Task TagRepository_ShouldReturnOnlyOwnerTags()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var otherOwnerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        context.Users.Add(new Infrastructure.Identity.User { Id = otherOwnerId, UserName = "other", NormalizedUserName = "OTHER" });
        context.Tags.Add(TestData.Tag(database.UserId, "favorite"));
        context.Tags.Add(TestData.Tag(otherOwnerId, "favorite"));
        await context.SaveChangesAsync();
        var repository = new TagRepository(context);

        var result = (await repository.SearchAsync(database.UserId, "fav", 10, CancellationToken.None)).ToList();

        Assert.Single(result);
        Assert.Equal(database.UserId, result[0].OwnerId);
    }

    [Fact]
    public async Task GenreRepository_ShouldPageAndCount()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        context.Genres.AddRange(TestData.Genre("Fantasy"), TestData.Genre("Drama"), TestData.Genre("Action"));
        await context.SaveChangesAsync();
        var repository = new GenreRepository(context);

        var page = (await repository.GetAllAsync(1, 1, CancellationToken.None)).ToList();
        var count = await repository.GetCountAsync(CancellationToken.None);

        Assert.Single(page);
        Assert.Equal(3, count);
    }
}
