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

    private static Task SetBookAuditDatesAsync(ApplicationDbContext context, Guid bookId, DateTimeOffset value)
    {
        return context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Books SET Created = {value}, LastModified = {value} WHERE Id = {bookId}");
    }

    [Fact]
    public async Task BookCsvDatasetSeeder_ShouldLoadLargeBalancedDataset()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();

        var snapshot = await BookCsvDatasetSeeder.SeedAsync(context, database.UserId);

        Assert.True(snapshot.BookCount >= 500);
        BookCsvDatasetSeeder.AssertBalancedTypeDistribution(snapshot);
        BookCsvDatasetSeeder.AssertBalancedStatusDistribution(snapshot);
        Assert.True(snapshot.BooksWithGenres >= snapshot.BookCount * 9 / 10);
        Assert.True(snapshot.PreservedTaggedBooks > 0);
        Assert.True(snapshot.PreservedRatedBooks > 0);
        Assert.True(snapshot.PreservedProgressBooks >= snapshot.BookCount * 9 / 10);
        Assert.Contains(snapshot.Samples, sample => sample.Tags.Count > 0);
        Assert.Contains(snapshot.Samples, sample => sample.Rating != null);
        Assert.Contains(snapshot.Samples, sample => sample.CurrentChapterNumber != null);
    }

    [Fact]
    public async Task BookRepository_ShouldScopeListAndGetByOwner()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var otherOwnerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        context.Users.Add(new Infrastructure.Identity.User { Id = otherOwnerId, UserName = "other", NormalizedUserName = "OTHER" });
        var snapshot = await BookCsvDatasetSeeder.SeedAsync(context, database.UserId);
        await TestData.AddBookAsync(context, otherOwnerId, "Other");
        var repository = new BookRepository(context);
        var queryService = CreateReadQueryService(context);

        var books = (await queryService.GetBooksAsync(database.UserId, BookSearchCriteria.Empty, 0, 25, null, null, CancellationToken.None)).ToList();
        var count = await queryService.GetBookCountAsync(database.UserId, BookSearchCriteria.Empty, CancellationToken.None);
        var otherBook = await repository.GetByNameAsync("Other", database.UserId, Guid.Parse("10000000-0000-0000-0000-000000000001"), CancellationToken.None);

        Assert.Equal(25, books.Count);
        Assert.Equal(snapshot.BookCount, count);
        Assert.DoesNotContain(books, book => book.PrimaryTitle == "Other");
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
        var snapshot = await BookCsvDatasetSeeder.SeedAsync(context, database.UserId);
        var sample = snapshot.WithTagAndRating;
        var queryService = CreateReadQueryService(context);
        var criteria = new BookSearchCriteria(
            Array.Empty<string>(),
            [new BookSearchFieldFilter(BookSearchField.Tag, sample.Tags[0])],
            [new BookSearchNumberFilter(BookSearchNumberField.Rating, BookSearchOperator.GreaterThanOrEqual, sample.Rating!.Value)]);

        var books = (await queryService.GetBooksAsync(database.UserId, criteria, 0, snapshot.BookCount, null, null, CancellationToken.None)).ToList();
        var count = await queryService.GetBookCountAsync(database.UserId, criteria, CancellationToken.None);

        Assert.NotEmpty(books);
        Assert.Equal(books.Count, count);
        Assert.Contains(books, book => book.Id == sample.Id);
        Assert.All(books, book =>
        {
            Assert.True(book.TagsCount > 0);
            Assert.True(book.Rating >= sample.Rating);
        });
    }

    [Fact]
    public async Task BookRepository_ShouldSearchByWildcardTitleCriteria()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var snapshot = await BookCsvDatasetSeeder.SeedAsync(context, database.UserId);
        var sample = snapshot.Any;
        var queryService = CreateReadQueryService(context);
        var prefix = sample.PrimaryTitle[..Math.Min(5, sample.PrimaryTitle.Length)];
        var criteria = new BookSearchCriteria(
            Array.Empty<string>(),
            [new BookSearchFieldFilter(BookSearchField.Title, $"{prefix}*")],
            Array.Empty<BookSearchNumberFilter>());

        var books = (await queryService.GetBooksAsync(database.UserId, criteria, 0, snapshot.BookCount, null, null, CancellationToken.None)).ToList();

        Assert.Contains(books, book => book.Id == sample.Id);
    }

    [Fact]
    public async Task BookRepository_ShouldSearchByProgressAndChaptersAliases()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var snapshot = await BookCsvDatasetSeeder.SeedAsync(context, database.UserId);
        var sample = snapshot.WithTotalChapters;
        var queryService = CreateReadQueryService(context);
        var criteria = BookSearchQueryParser.Parse($"progress:>={sample.CurrentChapterNumber} chapters:>={sample.TotalChapters}");

        var books = (await queryService.GetBooksAsync(database.UserId, criteria, 0, snapshot.BookCount, null, null, CancellationToken.None)).ToList();

        Assert.Contains(books, book => book.Id == sample.Id);
        Assert.All(books, book =>
        {
            Assert.True(book.CurrentChapterNumber >= sample.CurrentChapterNumber);
            Assert.True(book.TotalChapters >= sample.TotalChapters);
        });
    }

    [Fact]
    public async Task BookRepository_ShouldSearchByMissingValueFiltersWithOwnerIsolation()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var otherOwnerId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        context.Users.Add(new Infrastructure.Identity.User { Id = otherOwnerId, UserName = "other-missing", NormalizedUserName = "OTHER-MISSING" });
        var complete = await TestData.AddBookWithRelationsAsync(context, database.UserId);
        complete.Rating = 8;
        complete.Priority = 2;
        complete.TotalChapters = 100;
        var missing = TestData.Book(database.UserId, "Missing Fields");
        var otherOwnerMissing = TestData.Book(otherOwnerId, "Other Owner Missing Fields");
        context.Books.AddRange(missing, otherOwnerMissing);
        await context.SaveChangesAsync();
        var queryService = CreateReadQueryService(context);

        foreach (var query in new[]
        {
            "rating:none",
            "priority:NONE",
            "author:'none'",
            "genre:none",
            "tag:none",
            "total:none",
            "cover:none",
            "link:none",
            "genre:none tag:none"
        })
        {
            var books = (await queryService.GetBooksAsync(
                database.UserId,
                BookSearchQueryParser.Parse(query),
                0,
                10,
                "title",
                "asc",
                CancellationToken.None)).ToList();

            var book = Assert.Single(books);
            Assert.Equal(missing.Id, book.Id);
            Assert.DoesNotContain(books, item => item.Id == complete.Id);
            Assert.DoesNotContain(books, item => item.Id == otherOwnerMissing.Id);
        }
    }

    [Fact]
    public async Task BookRepository_ShouldSearchCreatedAndUpdatedDatesByDateOnly()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var oldBook = await TestData.AddBookAsync(context, database.UserId, "Old Date Match");
        var midBook = await TestData.AddBookAsync(context, database.UserId, "Middle Date Match");
        var lateBook = await TestData.AddBookAsync(context, database.UserId, "Late Date Match");
        await SetBookAuditDatesAsync(context, oldBook.Id, new DateTimeOffset(2026, 7, 14, 23, 59, 0, TimeSpan.Zero));
        await SetBookAuditDatesAsync(context, midBook.Id, new DateTimeOffset(2026, 7, 15, 12, 30, 0, TimeSpan.Zero));
        await SetBookAuditDatesAsync(context, lateBook.Id, new DateTimeOffset(2026, 7, 16, 0, 1, 0, TimeSpan.Zero));
        var queryService = CreateReadQueryService(context);

        var equal = await SearchIdsAsync("createDate:=15.07.2026");
        var greaterThan = await SearchIdsAsync("createDate:>2026-07-15");
        var lessThanOrEqual = await SearchIdsAsync("createDate:<=15/07/2026");
        var updatedBefore = await SearchIdsAsync("updateDate:<16.07.2026");

        Assert.Equal([midBook.Id], equal);
        Assert.Equal([lateBook.Id], greaterThan);
        Assert.Equal([oldBook.Id, midBook.Id], lessThanOrEqual);
        Assert.Equal([oldBook.Id, midBook.Id], updatedBefore);

        async Task<IReadOnlyCollection<Guid>> SearchIdsAsync(string query)
        {
            var books = await queryService.GetBooksAsync(
                database.UserId,
                BookSearchQueryParser.Parse(query),
                0,
                10,
                "created",
                "asc",
                CancellationToken.None);

            return books.Select(book => book.Id).ToArray();
        }
    }

    [Fact]
    public async Task BookRepository_ShouldSearchAcrossFieldValuesAndDictionaryFields()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var snapshot = await BookCsvDatasetSeeder.SeedAsync(context, database.UserId);
        var first = snapshot.Any;
        var tagged = snapshot.WithTag;
        var queryService = CreateReadQueryService(context);
        var titleCriteria = new BookSearchCriteria(
            Array.Empty<string>(),
            [new BookSearchFieldFilter(BookSearchField.Title, [first.PrimaryTitle, tagged.PrimaryTitle])],
            Array.Empty<BookSearchNumberFilter>());

        var titleOr = (await queryService.GetBooksAsync(
            database.UserId,
            titleCriteria,
            0,
            snapshot.BookCount,
            "title",
            "asc",
            CancellationToken.None)).ToList();
        var relationMatch = (await queryService.GetBooksAsync(
            database.UserId,
            new BookSearchCriteria(
                Array.Empty<string>(),
                [new BookSearchFieldFilter(BookSearchField.Genre, tagged.Genres[0]), new BookSearchFieldFilter(BookSearchField.Tag, tagged.Tags[0])],
                Array.Empty<BookSearchNumberFilter>()),
            0,
            snapshot.BookCount,
            null,
            null,
            CancellationToken.None)).ToList();
        var dictionaryMatch = (await queryService.GetBooksAsync(
            database.UserId,
            new BookSearchCriteria(
                Array.Empty<string>(),
                [new BookSearchFieldFilter(BookSearchField.Status, first.Status), new BookSearchFieldFilter(BookSearchField.Type, first.ContentType)],
                Array.Empty<BookSearchNumberFilter>()),
            0,
            snapshot.BookCount,
            null,
            null,
            CancellationToken.None)).ToList();

        Assert.Contains(titleOr, book => book.Id == first.Id);
        Assert.Contains(titleOr, book => book.Id == tagged.Id);
        Assert.Contains(relationMatch, book => book.Id == tagged.Id);
        Assert.All(relationMatch, book =>
        {
            Assert.True(book.GenresCount > 0);
            Assert.True(book.TagsCount > 0);
        });
        Assert.Contains(dictionaryMatch, book => book.Id == first.Id);
        Assert.All(dictionaryMatch, book =>
        {
            Assert.Equal(first.Status, book.Status);
            Assert.Equal(first.ContentType, book.ContentType);
        });
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
        var snapshot = await BookCsvDatasetSeeder.SeedAsync(context, database.UserId);
        var newer = snapshot.Samples[^1];
        var olderTimestamp = DateTimeOffset.Parse("2026-07-01T10:00:00+00:00");
        var newerTimestamp = DateTimeOffset.Parse("2026-07-02T10:00:00+00:00");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Books SET LastModified = {olderTimestamp} WHERE OwnerId = {database.UserId}");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Books SET LastModified = {newerTimestamp} WHERE Id = {newer.Id}");
        context.ChangeTracker.Clear();
        var queryService = CreateReadQueryService(context);

        var books = (await queryService.GetBooksAsync(database.UserId, BookSearchCriteria.Empty, 0, 10, null, null, CancellationToken.None)).ToList();

        Assert.Equal(newer.Id, books[0].Id);
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
        var snapshot = await BookCsvDatasetSeeder.SeedAsync(context, database.UserId);
        var queryService = CreateReadQueryService(context);
        var criteria = BookSearchQueryParser.Parse("chapters:>=0");

        var ascending = (await queryService.GetBooksAsync(database.UserId, criteria, 0, snapshot.BookCount, "chapters", "asc", CancellationToken.None)).ToList();
        var descending = (await queryService.GetBooksAsync(database.UserId, criteria, 0, snapshot.BookCount, "chapters", "desc", CancellationToken.None)).ToList();
        var expectedAscending = snapshot.Samples
            .Where(sample => sample.TotalChapters != null)
            .OrderBy(sample => sample.TotalChapters)
            .ThenBy(sample => sample.PrimaryTitle)
            .Select(sample => sample.Id)
            .ToArray();
        var expectedDescending = snapshot.Samples
            .Where(sample => sample.TotalChapters != null)
            .OrderByDescending(sample => sample.TotalChapters)
            .ThenBy(sample => sample.PrimaryTitle)
            .Select(sample => sample.Id)
            .ToArray();

        Assert.Equal(expectedAscending, ascending.Select(book => book.Id));
        Assert.Equal(expectedDescending, descending.Select(book => book.Id));
    }

    [Fact]
    public async Task BookRepository_ShouldSortByNumericOwnerAndDateFields()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var otherOwnerId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        context.Users.Add(new Infrastructure.Identity.User { Id = otherOwnerId, UserName = "other-sort", NormalizedUserName = "OTHER-SORT" });
        var snapshot = await BookCsvDatasetSeeder.SeedAsync(context, database.UserId);
        var other = await TestData.AddBookAsync(context, otherOwnerId, "Other");
        var oldest = snapshot.Samples[^1];
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE Books SET Created = {DateTimeOffset.Parse("2026-07-02T00:00:00+00:00")} WHERE OwnerId = {database.UserId}");
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE Books SET Created = {DateTimeOffset.Parse("2026-07-01T00:00:00+00:00")} WHERE Id = {oldest.Id}");
        context.ChangeTracker.Clear();
        var queryService = CreateReadQueryService(context);
        var ratingCriteria = BookSearchQueryParser.Parse("rating:>=0");

        var ratingAsc = (await queryService.GetBooksAsync(database.UserId, ratingCriteria, 0, 10, "rating", "asc", CancellationToken.None)).ToList();
        var ratingDesc = (await queryService.GetBooksAsync(database.UserId, ratingCriteria, 0, 10, "rating", "desc", CancellationToken.None)).ToList();
        var priorityAsc = (await queryService.GetBooksAsync(database.UserId, BookSearchCriteria.Empty, 0, 10, "priority", "asc", CancellationToken.None)).ToList();
        var progressDesc = (await queryService.GetBooksAsync(database.UserId, BookSearchCriteria.Empty, 0, 10, "progress", "desc", CancellationToken.None)).ToList();
        var createdAsc = (await queryService.GetBooksAsync(database.UserId, BookSearchCriteria.Empty, 0, 10, "created", "asc", CancellationToken.None)).ToList();
        var ownerDesc = (await queryService.GetAdminBooksAsync(BookSearchCriteria.Empty, 0, 10, "owner", "desc", CancellationToken.None)).ToList();

        Assert.Equal(snapshot.Samples.Where(sample => sample.Rating != null).Min(sample => sample.Rating), ratingAsc[0].Rating);
        Assert.Equal(snapshot.Samples.Where(sample => sample.Rating != null).Max(sample => sample.Rating), ratingDesc[0].Rating);
        Assert.Null(priorityAsc[0].Priority);
        Assert.Equal(snapshot.Samples.Max(sample => sample.CurrentChapterNumber), progressDesc[0].CurrentChapterNumber);
        Assert.Equal(oldest.Id, createdAsc[0].Id);
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
        await BookCsvDatasetSeeder.SeedAsync(context, database.UserId);

        var queryService = CreateSummaryQueryService(context);
        var criteria = new BookSearchCriteria(
            Array.Empty<string>(),
            Array.Empty<BookSearchFieldFilter>(),
            [new BookSearchNumberFilter(BookSearchNumberField.Rating, BookSearchOperator.GreaterThanOrEqual, 1)]);

        var summary = await queryService.GetSummaryAsync(database.UserId, criteria, CancellationToken.None);
        var expectedBooks = await context.Books
            .AsNoTracking()
            .Include(book => book.Status)
            .Include(book => book.ContentType)
            .Include(book => book.BookGenres)
                .ThenInclude(bookGenre => bookGenre.Genre)
            .Where(book => book.OwnerId == database.UserId && book.Rating >= 1)
            .ToListAsync();

        Assert.Equal(expectedBooks.Count, summary.TotalBooks);
        Assert.Equal(expectedBooks.Count(book => book.Rating != null), summary.RatedBooks);
        Assert.Equal(expectedBooks.Average(book => book.Rating), summary.AverageRating);
        Assert.Equal(expectedBooks.Sum(book => book.CurrentChapterNumber ?? 0), summary.CurrentChapters);
        Assert.Equal(expectedBooks.Count(book => book.CurrentChapterNumber != null), summary.BooksWithKnownCurrentChapter);
        Assert.Equal(
            expectedBooks.GroupBy(book => book.Status.Name).OrderByDescending(group => group.Count()).ThenBy(group => group.Key).Select(group => group.Key),
            summary.StatusCounts.Select(item => item.Status));
        Assert.Equal(
            expectedBooks.GroupBy(book => book.ContentType.Name).OrderByDescending(group => group.Count()).ThenBy(group => group.Key).Select(group => group.Key),
            summary.TypeCounts.Select(item => item.Type));
        Assert.NotEmpty(summary.GenreCounts);
        Assert.Equal(
            expectedBooks.Where(book => book.Rating != null).GroupBy(book => book.Rating!.Value).OrderBy(group => group.Key).Select(group => group.Key),
            summary.RatingCounts.Select(item => item.Rating));
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

        await BookCsvDatasetSeeder.SeedAsync(context, database.UserId);
        var firstMatch = await TestData.AddBookAsync(context, database.UserId, "AAAA Admin Dataset Match");
        var secondMatch = await TestData.AddBookAsync(context, otherOwnerId, "AAAB Admin Dataset Match");
        var queryService = CreateReadQueryService(context);
        var criteria = BookSearchQueryParser.Parse("title:\"Admin Dataset Match\"");

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
        var snapshot = await BookCsvDatasetSeeder.SeedAsync(context, database.UserId);
        var sample = snapshot.WithNotes;
        const string sentinelTitle = "CSV Projection Sentinel";
        var book = await context.Books
            .Include(book => book.Cover)
            .Include(book => book.Titles)
            .FirstAsync(book => book.Id == sample.Id);
        book.PrimaryTitle = sentinelTitle;
        book.NormalizedPrimaryTitle = MappingExtensions.NormalizeName(sentinelTitle);
        var primaryTitle = book.Titles.Single(title => title.IsPrimary);
        primaryTitle.Title = sentinelTitle;
        primaryTitle.NormalizedTitle = MappingExtensions.NormalizeName(sentinelTitle);
        book.Description = new string('d', 100);
        book.Notes = new string('n', 100);
        book.Cover!.ThumbnailStoragePath = "11111111111111111111111111111111/example.thumb.jpg";
        book.Cover.StoragePath = "11111111111111111111111111111111/example.jpg";
        book.Cover.Status = BookCoverStatus.Found;
        book.Cover.ThumbnailMimeType = "image/jpeg";
        book.Cover.LastAttemptAt = DateTimeOffset.Parse("2026-07-13T10:15:30+00:00");
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var queryService = CreateReadQueryService(context);
        var criteria = new BookSearchCriteria(
            Array.Empty<string>(),
            [new BookSearchFieldFilter(BookSearchField.Title, sentinelTitle)],
            Array.Empty<BookSearchNumberFilter>());

        var userList = (await queryService.GetBooksAsync(database.UserId, criteria, 0, 10, "title", "asc", CancellationToken.None)).ToList();
        var adminList = (await queryService.GetAdminBooksAsync(criteria, 0, 10, "title", "asc", CancellationToken.None)).ToList();

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
        await BookCsvDatasetSeeder.SeedAsync(context, database.UserId);
        var queryService = CreateReadQueryService(context);
        var criteria = BookSearchQueryParser.Parse("title:__missing_csv_dataset_query__");

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
        var snapshot = await BookCsvDatasetSeeder.SeedAsync(context, database.UserId);
        var sample = snapshot.WithTagAndRating;
        const string sentinelTitle = "CSV Export Sentinel";
        var matching = await context.Books
            .Include(book => book.Titles)
            .FirstAsync(book => book.Id == sample.Id);
        matching.PrimaryTitle = sentinelTitle;
        matching.NormalizedPrimaryTitle = MappingExtensions.NormalizeName(sentinelTitle);
        var primaryTitle = matching.Titles.Single(title => title.IsPrimary);
        primaryTitle.Title = sentinelTitle;
        primaryTitle.NormalizedTitle = MappingExtensions.NormalizeName(sentinelTitle);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var queryService = CreateExportQueryService(context);
        var criteria = new BookSearchCriteria(
            Array.Empty<string>(),
            [new BookSearchFieldFilter(BookSearchField.Title, sentinelTitle), new BookSearchFieldFilter(BookSearchField.Tag, sample.Tags[0])],
            [new BookSearchNumberFilter(BookSearchNumberField.Rating, BookSearchOperator.GreaterThanOrEqual, sample.Rating!.Value)]);

        var result = await queryService.GetBooksForExportAsync(database.UserId, criteria, 0, 10, "title", "asc", CancellationToken.None);

        var book = Assert.Single(result.Data);
        Assert.Equal(1, result.Total);
        Assert.Equal(sentinelTitle, book.PrimaryTitle);
        Assert.Equal(sample.Notes, book.Notes);
        Assert.Equal(sample.CurrentChapterNumber, book.CurrentChapterNumber);
        Assert.Contains(sample.Tags[0], book.Tags);
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
