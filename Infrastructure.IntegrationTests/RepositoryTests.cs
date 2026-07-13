using Application.Common;
using Domain.Associations;
using Domain.Entities;
using Domain.Repositories;
using Infrastructure.Contexts;
using Infrastructure.IntegrationTests.TestSupport;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests;

public class RepositoryTests
{
    private static BookReadQueryService CreateReadQueryService(ApplicationDbContext context)
    {
        var criteriaApplier = new BookSearchCriteriaApplier(context);
        var sortBuilder = new BookSortBuilder(context);
        var projectionQuery = new BookListProjectionQuery(context, sortBuilder);
        return new BookReadQueryService(context, criteriaApplier, sortBuilder, projectionQuery);
    }

    private static BookSummaryQueryService CreateSummaryQueryService(ApplicationDbContext context)
    {
        return new BookSummaryQueryService(context, new BookSearchCriteriaApplier(context));
    }

    private static BookExportQueryService CreateExportQueryService(ApplicationDbContext context)
    {
        var criteriaApplier = new BookSearchCriteriaApplier(context);
        return new BookExportQueryService(context, criteriaApplier, new BookSortBuilder(context));
    }

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
        var queryService = CreateReadQueryService(context);

        var books = (await queryService.GetBooksAsync(database.UserId, BookSearchCriteria.Empty, 0, 10, null, null, CancellationToken.None)).ToList();
        var count = await queryService.GetBookCountAsync(database.UserId, BookSearchCriteria.Empty, CancellationToken.None);
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
    public async Task BookRepository_ShouldSupportCountsAdminLookupAddDeleteAndProgress()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var otherOwnerId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        context.Users.Add(new Infrastructure.Identity.User { Id = otherOwnerId, UserName = "other-progress", NormalizedUserName = "OTHER-PROGRESS" });
        await context.SaveChangesAsync();
        var repository = new BookRepository(context);
        var book = TestData.Book(database.UserId, "Repository Path");
        book.TotalChapters = 20;

        await repository.AddAsync(book, CancellationToken.None);
        await TestData.AddBookAsync(context, otherOwnerId, "Other Owner Path");

        Assert.Equal(1, await repository.GetCountAsync(database.UserId, CancellationToken.None));
        Assert.Equal(2, await repository.GetCountAsync(CancellationToken.None));
        Assert.NotNull(await repository.GetByIdAsync(book.Id, CancellationToken.None));
        Assert.NotNull(await repository.GetForUpdateAsync(book.Id, database.UserId, CancellationToken.None));
        Assert.NotNull(await repository.GetForUpdateAsync(book.Id, CancellationToken.None));
        Assert.Equal(20, await repository.GetTotalChaptersAsync(book.Id, database.UserId, CancellationToken.None));
        Assert.False(await repository.UpdateProgressAsync(Guid.NewGuid(), database.UserId, 1, "1", null, CancellationToken.None));
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            repository.UpdateProgressAsync(book.Id, database.UserId, 21, "21", null, CancellationToken.None));

        Assert.True(await repository.UpdateProgressAsync(book.Id, database.UserId, 10, "10", null, CancellationToken.None));
        Assert.True(await repository.UpdateProgressAsync(book.Id, database.UserId, 10, "10", "note", CancellationToken.None));
        Assert.Equal(2, await context.BookProgressHistory.CountAsync(history => history.BookId == book.Id));
        await repository.DeleteAsync(Guid.NewGuid(), database.UserId, CancellationToken.None);
        await repository.DeleteAsync(book.Id, database.UserId, CancellationToken.None);

        Assert.Equal(0, await repository.GetCountAsync(database.UserId, CancellationToken.None));
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
        var queryService = CreateReadQueryService(context);
        var criteria = new BookSearchCriteria(
            new[] { "returnee" },
            new[] { new BookSearchFieldFilter(BookSearchField.Tag, "favorite"), new BookSearchFieldFilter(BookSearchField.Author, "toi") },
            new[] { new BookSearchNumberFilter(BookSearchNumberField.Rating, BookSearchOperator.GreaterThanOrEqual, 8) });

        var books = (await queryService.GetBooksAsync(database.UserId, criteria, 0, 10, null, null, CancellationToken.None)).ToList();
        var count = await queryService.GetBookCountAsync(database.UserId, criteria, CancellationToken.None);

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
        var queryService = CreateReadQueryService(context);
        var criteria = new BookSearchCriteria(
            Array.Empty<string>(),
            new[] { new BookSearchFieldFilter(BookSearchField.Title, "i sha*") },
            Array.Empty<BookSearchNumberFilter>());

        var books = (await queryService.GetBooksAsync(database.UserId, criteria, 0, 10, null, null, CancellationToken.None)).ToList();

        Assert.Single(books);
        Assert.Equal(matching.Id, books[0].Id);
    }

    [Fact]
    public async Task BookRepository_ShouldSearchByProgressAndChaptersAliases()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var matching = await TestData.AddBookAsync(context, database.UserId, "Long Runner");
        matching.CurrentChapterNumber = 75;
        matching.TotalChapters = 150;
        var tooShort = await TestData.AddBookAsync(context, database.UserId, "Short Runner");
        tooShort.CurrentChapterNumber = 75;
        tooShort.TotalChapters = 90;
        var tooEarly = await TestData.AddBookAsync(context, database.UserId, "Early Runner");
        tooEarly.CurrentChapterNumber = 20;
        tooEarly.TotalChapters = 150;
        await context.SaveChangesAsync();
        var queryService = CreateReadQueryService(context);
        var criteria = BookSearchQueryParser.Parse("progress:>=50 chapters:>=100");

        var books = (await queryService.GetBooksAsync(database.UserId, criteria, 0, 10, null, null, CancellationToken.None)).ToList();

        Assert.Single(books);
        Assert.Equal(matching.Id, books[0].Id);
    }

    [Fact]
    public async Task BookRepository_ShouldSearchAcrossFieldValuesAndDictionaryFields()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var matching = await TestData.AddBookWithRelationsAsync(context, database.UserId);
        var completedManga = await TestData.AddBookAsync(context, database.UserId, "Second Match");
        completedManga.ContentTypeId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        completedManga.StatusId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        await TestData.AddBookAsync(context, database.UserId, "Unrelated");
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var queryService = CreateReadQueryService(context);
        var titleCriteria = new BookSearchCriteria(
            Array.Empty<string>(),
            [new BookSearchFieldFilter(BookSearchField.Title, ["Everyone Else is a Returnee", "Second Match"])],
            Array.Empty<BookSearchNumberFilter>());

        var titleOr = (await queryService.GetBooksAsync(
            database.UserId,
            titleCriteria,
            0,
            10,
            "title",
            "asc",
            CancellationToken.None)).ToList();
        var relationMatch = (await queryService.GetBooksAsync(
            database.UserId,
            BookSearchQueryParser.Parse("author:Toika genre:Fantasy tag:favorite"),
            0,
            10,
            null,
            null,
            CancellationToken.None)).ToList();
        var dictionaryMatch = (await queryService.GetBooksAsync(
            database.UserId,
            BookSearchQueryParser.Parse("status:completed type:manga"),
            0,
            10,
            null,
            null,
            CancellationToken.None)).ToList();

        Assert.Equal([matching.Id, completedManga.Id], titleOr.Select(book => book.Id));
        Assert.Single(relationMatch);
        Assert.Equal(matching.Id, relationMatch[0].Id);
        Assert.Single(dictionaryMatch);
        Assert.Equal(completedManga.Id, dictionaryMatch[0].Id);
    }

    [Fact]
    public async Task BookRepository_ShouldApplyAllNumericSearchOperators()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var low = await TestData.AddBookAsync(context, database.UserId, "Low");
        low.Rating = 3;
        low.Priority = 1;
        low.CurrentChapterNumber = 10;
        low.TotalChapters = 50;
        var mid = await TestData.AddBookAsync(context, database.UserId, "Mid");
        mid.Rating = 5;
        mid.Priority = 2;
        mid.CurrentChapterNumber = 20;
        mid.TotalChapters = 100;
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
        var queryService = CreateReadQueryService(context);

        async Task<Guid[]> FindIds(string query)
        {
            var books = await queryService.GetBooksAsync(database.UserId, BookSearchQueryParser.Parse(query), 0, 10, "title", "asc", CancellationToken.None);
            return books.Select(book => book.Id).ToArray();
        }

        async Task<Guid[]> FindIdsByNumber(BookSearchNumberField field, BookSearchOperator op, decimal value)
        {
            var criteria = new BookSearchCriteria(
                Array.Empty<string>(),
                Array.Empty<BookSearchFieldFilter>(),
                [new BookSearchNumberFilter(field, op, value)]);
            var books = await queryService.GetBooksAsync(database.UserId, criteria, 0, 10, "title", "asc", CancellationToken.None);
            return books.Select(book => book.Id).ToArray();
        }

        Assert.Equal([high.Id], await FindIdsByNumber(BookSearchNumberField.Rating, BookSearchOperator.GreaterThan, 5));
        Assert.Equal([mid.Id], await FindIdsByNumber(BookSearchNumberField.CurrentChapter, BookSearchOperator.Equal, 20));
        Assert.Equal([low.Id, mid.Id], await FindIdsByNumber(BookSearchNumberField.Rating, BookSearchOperator.LessThanOrEqual, 5));
        Assert.Equal([low.Id], await FindIdsByNumber(BookSearchNumberField.Priority, BookSearchOperator.LessThan, 2));
        Assert.Equal([high.Id, mid.Id], await FindIdsByNumber(BookSearchNumberField.Priority, BookSearchOperator.GreaterThanOrEqual, 2));
        Assert.Equal([high.Id], await FindIdsByNumber(BookSearchNumberField.CurrentChapter, BookSearchOperator.GreaterThan, 20));
        Assert.Equal([low.Id, mid.Id], await FindIdsByNumber(BookSearchNumberField.CurrentChapter, BookSearchOperator.LessThanOrEqual, 20));
        Assert.Equal([high.Id], await FindIds("total:>100"));
        Assert.Equal([low.Id, mid.Id], await FindIds("chapters:<=100"));
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
        var queryService = CreateReadQueryService(context);

        var books = (await queryService.GetBooksAsync(database.UserId, BookSearchCriteria.Empty, 0, 10, null, null, CancellationToken.None)).ToList();

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
        var queryService = CreateReadQueryService(context);

        var ascending = (await queryService.GetBooksAsync(database.UserId, BookSearchCriteria.Empty, 0, 10, "title", "asc", CancellationToken.None)).ToList();
        var descending = (await queryService.GetBooksAsync(database.UserId, BookSearchCriteria.Empty, 0, 10, "title", "desc", CancellationToken.None)).ToList();

        Assert.Equal([apple.Id, bananaUpper.Id, bananaLower.Id, zebra.Id], ascending.Select(book => book.Id));
        Assert.Equal([zebra.Id, bananaLower.Id, bananaUpper.Id, apple.Id], descending.Select(book => book.Id));
    }

    [Fact]
    public async Task BookRepository_ShouldSortByTotalChaptersAlias()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var shortBook = await TestData.AddBookAsync(context, database.UserId, "Short");
        var longBook = await TestData.AddBookAsync(context, database.UserId, "Long");
        shortBook.TotalChapters = 20;
        longBook.TotalChapters = 200;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var queryService = CreateReadQueryService(context);

        var ascending = (await queryService.GetBooksAsync(database.UserId, BookSearchCriteria.Empty, 0, 10, "chapters", "asc", CancellationToken.None)).ToList();
        var descending = (await queryService.GetBooksAsync(database.UserId, BookSearchCriteria.Empty, 0, 10, "chapters", "desc", CancellationToken.None)).ToList();

        Assert.Equal([shortBook.Id, longBook.Id], ascending.Select(book => book.Id));
        Assert.Equal([longBook.Id, shortBook.Id], descending.Select(book => book.Id));
    }

    [Fact]
    public async Task BookRepository_ShouldSortByNumericOwnerAndDateFields()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var otherOwnerId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        context.Users.Add(new Infrastructure.Identity.User { Id = otherOwnerId, UserName = "other-sort", NormalizedUserName = "OTHER-SORT" });
        var low = await TestData.AddBookAsync(context, database.UserId, "Low");
        low.Rating = 2;
        low.Priority = 1;
        low.CurrentChapterNumber = 5;
        var high = await TestData.AddBookAsync(context, database.UserId, "High");
        high.Rating = 9;
        high.Priority = 5;
        high.CurrentChapterNumber = 50;
        var unrated = await TestData.AddBookAsync(context, database.UserId, "Unrated");
        unrated.Rating = null;
        unrated.Priority = null;
        unrated.CurrentChapterNumber = null;
        var other = await TestData.AddBookAsync(context, otherOwnerId, "Other");
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE Books SET Created = {DateTimeOffset.Parse("2026-07-01T00:00:00+00:00")} WHERE Id = {low.Id}");
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE Books SET Created = {DateTimeOffset.Parse("2026-07-03T00:00:00+00:00")} WHERE Id = {high.Id}");
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE Books SET Created = {DateTimeOffset.Parse("2026-07-02T00:00:00+00:00")} WHERE Id = {unrated.Id}");
        context.ChangeTracker.Clear();
        var queryService = CreateReadQueryService(context);

        var ratingAsc = (await queryService.GetBooksAsync(database.UserId, BookSearchCriteria.Empty, 0, 10, "rating", "asc", CancellationToken.None)).ToList();
        var ratingDesc = (await queryService.GetBooksAsync(database.UserId, BookSearchCriteria.Empty, 0, 10, "rating", "desc", CancellationToken.None)).ToList();
        var priorityAsc = (await queryService.GetBooksAsync(database.UserId, BookSearchCriteria.Empty, 0, 10, "priority", "asc", CancellationToken.None)).ToList();
        var progressDesc = (await queryService.GetBooksAsync(database.UserId, BookSearchCriteria.Empty, 0, 10, "progress", "desc", CancellationToken.None)).ToList();
        var createdAsc = (await queryService.GetBooksAsync(database.UserId, BookSearchCriteria.Empty, 0, 10, "created", "asc", CancellationToken.None)).ToList();
        var ownerDesc = (await queryService.GetAdminBooksAsync(BookSearchCriteria.Empty, 0, 10, "owner", "desc", CancellationToken.None)).ToList();

        Assert.Equal([low.Id, high.Id, unrated.Id], ratingAsc.Select(book => book.Id));
        Assert.Equal([high.Id, low.Id, unrated.Id], ratingDesc.Select(book => book.Id));
        Assert.Equal([unrated.Id, low.Id, high.Id], priorityAsc.Select(book => book.Id));
        Assert.Equal([high.Id, low.Id, unrated.Id], progressDesc.Select(book => book.Id));
        Assert.Equal([low.Id, unrated.Id, high.Id], createdAsc.Select(book => book.Id));
        Assert.Equal(otherOwnerId, ownerDesc[0].OwnerId);
        Assert.Contains(ownerDesc, book => book.Id == other.Id);
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

        var queryService = CreateReadQueryService(context);
        const string novelTypeName = "Novel";
        const string mangaTypeName = "Manga";
        const string readingStatusName = "Reading";
        const string completedStatusName = "Completed";

        var typeAscending = (await queryService.GetBooksAsync(database.UserId, BookSearchCriteria.Empty, 0, 10, "type", novelTypeName, CancellationToken.None)).ToList();
        var typeRotated = (await queryService.GetBooksAsync(database.UserId, BookSearchCriteria.Empty, 0, 10, "type", mangaTypeName, CancellationToken.None)).ToList();
        var statusAscending = (await queryService.GetBooksAsync(database.UserId, BookSearchCriteria.Empty, 0, 10, "status", readingStatusName, CancellationToken.None)).ToList();
        var statusRotated = (await queryService.GetBooksAsync(database.UserId, BookSearchCriteria.Empty, 0, 10, "status", completedStatusName, CancellationToken.None)).ToList();

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

        var queryService = CreateReadQueryService(context);
        const string missingTypeName = "Manhwa";
        const string missingStatusName = "Plan To Read";

        var nextType = await queryService.GetNextCycleSortDirectionAsync(database.UserId, BookSearchCriteria.Empty, "type", missingTypeName, CancellationToken.None);
        var nextStatus = await queryService.GetNextCycleSortDirectionAsync(database.UserId, BookSearchCriteria.Empty, "status", missingStatusName, CancellationToken.None);

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

        var queryService = CreateSummaryQueryService(context);
        var criteria = new BookSearchCriteria(
            Array.Empty<string>(),
            Array.Empty<BookSearchFieldFilter>(),
            [new BookSearchNumberFilter(BookSearchNumberField.Rating, BookSearchOperator.GreaterThanOrEqual, 1)]);

        var summary = await queryService.GetSummaryAsync(database.UserId, criteria, CancellationToken.None);

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
        var queryService = CreateReadQueryService(context);
        var criteria = BookSearchQueryParser.Parse("title:Lord");

        var firstPage = (await queryService.GetAdminBooksAsync(criteria, 0, 1, "title", "asc", CancellationToken.None)).ToList();
        var secondPage = (await queryService.GetAdminBooksAsync(criteria, 1, 1, "title", "asc", CancellationToken.None)).ToList();
        var count = await queryService.GetAdminBookCountAsync(criteria, CancellationToken.None);

        Assert.Equal(2, count);
        Assert.Single(firstPage);
        Assert.Single(secondPage);
        Assert.Equal(firstMatch.Id, firstPage[0].Id);
        Assert.Equal(secondMatch.Id, secondPage[0].Id);
    }

    [Fact]
    public async Task BookRepository_ShouldProjectUserAndAdminListsWithSharedBookFields()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var book = await TestData.AddBookWithRelationsAsync(context, database.UserId);
        book.Description = new string('d', 100);
        book.Notes = new string('n', 100);
        book.Cover!.ThumbnailStoragePath = "11111111111111111111111111111111/example.thumb.jpg";
        book.Cover.ThumbnailMimeType = "image/jpeg";
        book.Cover.LastAttemptAt = DateTimeOffset.Parse("2026-07-13T10:15:30+00:00");
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var queryService = CreateReadQueryService(context);

        var userList = (await queryService.GetBooksAsync(database.UserId, BookSearchCriteria.Empty, 0, 10, "title", "asc", CancellationToken.None)).ToList();
        var adminList = (await queryService.GetAdminBooksAsync(BookSearchCriteria.Empty, 0, 10, "title", "asc", CancellationToken.None)).ToList();

        var userItem = Assert.Single(userList);
        var adminItem = Assert.Single(adminList);
        Assert.Equal(userItem.Id, adminItem.Id);
        Assert.Equal(userItem.PrimaryTitle, adminItem.PrimaryTitle);
        Assert.Equal(userItem.Description, adminItem.Description);
        Assert.Equal(userItem.Notes, adminItem.Notes);
        Assert.Equal(userItem.AlternativeTitles, adminItem.AlternativeTitles);
        Assert.Equal(userItem.Genres, adminItem.Genres);
        Assert.Equal(userItem.Tags, adminItem.Tags);
        Assert.Equal(userItem.Cover!.ImageUrl, adminItem.Cover!.ImageUrl);
        Assert.Equal(userItem.Cover.ThumbnailImageUrl, adminItem.Cover.ThumbnailImageUrl);
        Assert.Contains($"/api/v1/book/{book.Id}/cover/file?v=", userItem.Cover.ImageUrl);
        Assert.Contains($"/api/v1/book/{book.Id}/cover/thumbnail?v=", userItem.Cover.ThumbnailImageUrl);
        Assert.Equal(database.UserId, adminItem.OwnerId);
        Assert.False(string.IsNullOrWhiteSpace(adminItem.OwnerUsername));
    }

    [Fact]
    public async Task BookRepository_ShouldReturnNoAdminSearchResultsForMissingQuery()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        await TestData.AddBookAsync(context, database.UserId, "Lord of Mysteries");
        var queryService = CreateReadQueryService(context);
        var criteria = BookSearchQueryParser.Parse("title:Missing");

        var results = (await queryService.GetAdminBooksAsync(criteria, 0, 10, "title", "asc", CancellationToken.None)).ToList();
        var count = await queryService.GetAdminBookCountAsync(criteria, CancellationToken.None);

        Assert.Empty(results);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task BookExportQueryService_ShouldReturnFilteredSortedDetailedDtos()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var matching = await TestData.AddBookWithRelationsAsync(context, database.UserId);
        matching.Rating = 9;
        matching.CurrentChapterNumber = 42;
        matching.Notes = "Export notes";
        await TestData.AddBookAsync(context, database.UserId, "Unrelated Export");
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var queryService = CreateExportQueryService(context);
        var criteria = BookSearchQueryParser.Parse("tag:favorite rating:>=8");

        var result = await queryService.GetBooksForExportAsync(database.UserId, criteria, 0, 10, "title", "asc", CancellationToken.None);

        var book = Assert.Single(result.Data);
        Assert.Equal(1, result.Total);
        Assert.Equal(matching.PrimaryTitle, book.PrimaryTitle);
        Assert.Equal("Export notes", book.Notes);
        Assert.Equal(42, book.CurrentChapterNumber);
        Assert.Contains("favorite", book.Tags);
        Assert.NotNull(book.Cover);
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
