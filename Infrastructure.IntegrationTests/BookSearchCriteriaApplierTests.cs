using Application.Common;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Repositories;
using Infrastructure.Contexts;
using Infrastructure.IntegrationTests.TestSupport;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests;

public sealed class BookSearchCriteriaApplierTests
{
    public static TheoryData<BookSearchField> FieldCases => new()
    {
        BookSearchField.Title,
        BookSearchField.Author,
        BookSearchField.Tag,
        BookSearchField.Genre,
        BookSearchField.Status,
        BookSearchField.Type
    };

    public static TheoryData<BookSearchNumberField, BookSearchOperator> NumberCases
    {
        get
        {
            var data = new TheoryData<BookSearchNumberField, BookSearchOperator>();
            foreach (var numberField in Enum.GetValues<BookSearchNumberField>())
            {
                foreach (var op in Enum.GetValues<BookSearchOperator>())
                {
                    data.Add(numberField, op);
                }
            }

            return data;
        }
    }

    public static TheoryData<BookSearchDateField, BookSearchOperator, bool> DateCases
    {
        get
        {
            var data = new TheoryData<BookSearchDateField, BookSearchOperator, bool>();
            foreach (var postgres in new[] { false, true })
            {
                foreach (var dateField in Enum.GetValues<BookSearchDateField>())
                {
                    foreach (var op in Enum.GetValues<BookSearchOperator>())
                    {
                        data.Add(dateField, op, postgres);
                    }
                }
            }

            return data;
        }
    }

    public static TheoryData<BookSearchOperator, string[]> OperatorExpectedTitles => new()
    {
        { BookSearchOperator.GreaterThan, ["High"] },
        { BookSearchOperator.GreaterThanOrEqual, ["Equal", "High"] },
        { BookSearchOperator.LessThan, ["Low"] },
        { BookSearchOperator.LessThanOrEqual, ["Equal", "Low"] },
        { BookSearchOperator.Equal, ["Equal"] }
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GeneralTextSearch_ShouldGenerateQueryForBothProviders(bool postgres)
    {
        using var context = CreateQueryContext(postgres);
        var criteria = Criteria(["  needle*with%_\\  "]);

        var sql = Apply(context, criteria).ToQueryString();

        Assert.Contains(postgres ? "ILIKE" : "LIKE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(postgres ? "BookTitles" : "NormalizedTitle", sql, StringComparison.Ordinal);
        Assert.Contains(postgres ? "AuthorNames" : "NormalizedName", sql, StringComparison.Ordinal);
        Assert.Equal(4, CountOccurrences(sql, postgres ? "ILIKE" : "LIKE"));
    }

    [Fact]
    public async Task GeneralTextSearch_ShouldMatchEverySupportedTextSource()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var primaryTitle = TestData.Book(database.UserId, "Primary Needle");
        var alternateTitle = TestData.Book(database.UserId, "Alternate Book");
        alternateTitle.Titles.Add(new BookTitle
        {
            Title = "Alternate Needle",
            NormalizedTitle = MappingExtensions.NormalizeName("Alternate Needle"),
            IsPrimary = false,
            Source = "Test"
        });
        var primaryAuthor = TestData.Book(database.UserId, "Primary Author Book", TestData.Author("Author Needle"));
        var aliasAuthor = TestData.Author("Alias Author");
        aliasAuthor.Names.Add(new AuthorName
        {
            Name = "Alias Needle",
            NormalizedName = MappingExtensions.NormalizeName("Alias Needle"),
            IsPrimary = false,
            Source = "Test"
        });
        var authorAlias = TestData.Book(database.UserId, "Alias Author Book", aliasAuthor);
        var noMatch = TestData.Book(database.UserId, "Unrelated Book", TestData.Author("Unrelated Author"));
        context.Books.AddRange(primaryTitle, alternateTitle, primaryAuthor, authorAlias, noMatch);
        await context.SaveChangesAsync();

        await AssertMatchesOnlyAsync("Primary Needle", primaryTitle.Id);
        await AssertMatchesOnlyAsync("Alternate Needle", alternateTitle.Id);
        await AssertMatchesOnlyAsync("Author Needle", primaryAuthor.Id);
        await AssertMatchesOnlyAsync("Alias Needle", authorAlias.Id);

        async Task AssertMatchesOnlyAsync(string term, Guid expectedId)
        {
            var ids = await Apply(context, Criteria([term]))
                .Select(book => book.Id)
                .ToArrayAsync();
            Assert.Equal([expectedId], ids);
        }
    }

    [Fact]
    public async Task GeneralTextSearch_ShouldTreatPercentAndUnderscoreAsLiteralsAndAsteriskAsWildcard()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var literal = TestData.Book(database.UserId, @"Literal 100%_Path\End");
        var wildcard = TestData.Book(database.UserId, "Wildcard Prefix Middle Suffix");
        var percentOnly = TestData.Book(database.UserId, @"Literal 100AXPath\End");
        context.Books.AddRange(literal, wildcard, percentOnly);
        await context.SaveChangesAsync();

        Assert.Equal([literal.Id], await SearchTermsAsync(context, @"100%_Path\End"));
        Assert.Equal([wildcard.Id], await SearchTermsAsync(context, "Wildcard Prefix*Suffix"));
    }

    [Fact]
    public async Task MultipleGeneralTerms_ShouldUseAnd()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var both = TestData.Book(database.UserId, "Alpha Beta");
        var alphaOnly = TestData.Book(database.UserId, "Alpha");
        var betaOnly = TestData.Book(database.UserId, "Beta");
        context.Books.AddRange(both, alphaOnly, betaOnly);
        await context.SaveChangesAsync();

        var ids = await Apply(context, Criteria(["Alpha", "Beta"]))
            .Select(book => book.Id)
            .ToArrayAsync();

        Assert.Equal([both.Id], ids);
    }

    [Fact]
    public async Task TitleFilter_ShouldMatchPrimaryAndAlternativeTitlesAndRejectOtherFields()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var primary = TestData.Book(database.UserId, "Title Needle Primary");
        var alternative = TestData.Book(database.UserId, "Alternative Title Book");
        alternative.Titles.Add(new BookTitle
        {
            Title = "Title Needle Alternative",
            NormalizedTitle = MappingExtensions.NormalizeName("Title Needle Alternative"),
            IsPrimary = false,
            Source = "Test"
        });
        var authorOnly = TestData.Book(database.UserId, "Author Only Book", TestData.Author("Title Needle Author"));
        context.Books.AddRange(primary, alternative, authorOnly);
        await context.SaveChangesAsync();

        var ids = await SearchFieldAsync(context, BookSearchField.Title, "Title Needle*");

        Assert.Equal(new[] { alternative.Id, primary.Id }.Order(), ids.Order());
    }

    [Fact]
    public async Task AuthorFilter_ShouldMatchPrimaryAndAliasNamesAndRejectTitles()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var primary = TestData.Book(database.UserId, "Primary Author Book", TestData.Author("Author Needle Primary"));
        var aliasAuthor = TestData.Author("Unrelated Primary Author");
        aliasAuthor.Names.Add(new AuthorName
        {
            Name = "Author Needle Alias",
            NormalizedName = MappingExtensions.NormalizeName("Author Needle Alias"),
            IsPrimary = false,
            Source = "Test"
        });
        var alias = TestData.Book(database.UserId, "Alias Author Book", aliasAuthor);
        var titleOnly = TestData.Book(database.UserId, "Author Needle Title");
        context.Books.AddRange(primary, alias, titleOnly);
        await context.SaveChangesAsync();

        var ids = await SearchFieldAsync(context, BookSearchField.Author, "Author Needle*");

        Assert.Equal(new[] { alias.Id, primary.Id }.Order(), ids.Order());
    }

    [Fact]
    public async Task TagAndGenreFilters_ShouldNotBeInterchanged()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var tagged = TestData.Book(database.UserId, "Tagged");
        tagged.BookTags.Add(new Domain.Associations.BookTag
        {
            Book = tagged, Tag = TestData.Tag(database.UserId, "Relation Needle")
        });
        var genreBook = TestData.Book(database.UserId, "Genre");
        genreBook.BookGenres.Add(new Domain.Associations.BookGenre
        {
            Book = genreBook, Genre = TestData.Genre("Relation Needle")
        });
        context.Books.AddRange(tagged, genreBook);
        await context.SaveChangesAsync();

        Assert.Equal([tagged.Id], await SearchFieldAsync(context, BookSearchField.Tag, "Relation Needle"));
        Assert.Equal([genreBook.Id], await SearchFieldAsync(context, BookSearchField.Genre, "Relation Needle"));
    }

    [Fact]
    public async Task StatusAndTypeFilters_ShouldNotBeInterchanged()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var statusMatch = TestData.Book(database.UserId, "Status Match");
        statusMatch.StatusId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var typeMatch = TestData.Book(database.UserId, "Type Match");
        typeMatch.ContentTypeId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var defaultBook = TestData.Book(database.UserId, "Default");
        context.Books.AddRange(statusMatch, typeMatch, defaultBook);
        await context.SaveChangesAsync();

        Assert.Equal([statusMatch.Id], await SearchFieldAsync(context, BookSearchField.Status, "Completed"));
        Assert.Equal([typeMatch.Id], await SearchFieldAsync(context, BookSearchField.Type, "Manga"));
    }

    [Fact]
    public async Task FieldFilterMultipleValues_ShouldUseOrWithinOneFilter()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var first = TestData.Book(database.UserId, "First Choice");
        var second = TestData.Book(database.UserId, "Second Choice");
        var rejected = TestData.Book(database.UserId, "Rejected");
        context.Books.AddRange(first, second, rejected);
        await context.SaveChangesAsync();

        var ids = await Apply(
                context,
                Criteria(fields:
                [
                    new BookSearchFieldFilter(BookSearchField.Title, ["First*", "Second*"])
                ]))
            .Select(book => book.Id)
            .ToArrayAsync();

        Assert.Equal(new[] { first.Id, second.Id }.Order(), ids.Order());
    }

    [Theory]
    [MemberData(nameof(FieldCases))]
    public void FieldFilters_ShouldGeneratePostgresQueryForEveryField(BookSearchField field)
    {
        using var context = CreateQueryContext(true);
        var criteria = Criteria(fields: [new BookSearchFieldFilter(field, ["first", "second*"])]);

        var sql = Apply(context, criteria).ToQueryString();

        Assert.Contains("ILIKE", sql, StringComparison.Ordinal);
        Assert.Contains(" OR ", sql, StringComparison.OrdinalIgnoreCase);
        foreach (var fragment in ExpectedPostgresFieldFragments(field))
        {
            Assert.Contains(fragment, sql, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyAndUnknownFieldFilters_ShouldLeaveQueryUnchanged(bool postgres)
    {
        using var context = CreateQueryContext(postgres);
        var criteria = Criteria(fields:
        [
            new BookSearchFieldFilter(BookSearchField.Title, Array.Empty<string>()),
            new BookSearchFieldFilter((BookSearchField)int.MaxValue, ["ignored"])
        ]);

        var sql = Apply(context, criteria).ToQueryString();

        Assert.DoesNotContain("WHERE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(NumberCases))]
    public async Task NumberFilters_ShouldReturnExactResultsForEveryFieldAndOperator(
        BookSearchNumberField field,
        BookSearchOperator op)
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var low = TestData.Book(database.UserId, "Low");
        var equal = TestData.Book(database.UserId, "Equal");
        var high = TestData.Book(database.UserId, "High");
        var missing = TestData.Book(database.UserId, "Missing");
        SetNumber(low, field, 4);
        SetNumber(equal, field, 5);
        SetNumber(high, field, 6);
        context.Books.AddRange(low, equal, high, missing);
        await context.SaveChangesAsync();
        var criteria = Criteria(numbers: [new BookSearchNumberFilter(field, op, 5)]);

        var titles = await Apply(context, criteria)
            .OrderBy(book => book.PrimaryTitle)
            .Select(book => book.PrimaryTitle)
            .ToArrayAsync();

        Assert.Equal(ExpectedTitles(op).Order(), titles.Order());
    }

    [Theory]
    [MemberData(nameof(NumberCases))]
    public void NumberFilters_ShouldGeneratePostgresQueryForEveryFieldAndOperator(
        BookSearchNumberField field,
        BookSearchOperator op)
    {
        using var context = CreateQueryContext(true);
        var criteria = Criteria(numbers: [new BookSearchNumberFilter(field, op, 5)]);

        var sql = Apply(context, criteria).ToQueryString();

        Assert.Contains(ExpectedComparisonOperator(op), sql, StringComparison.Ordinal);
        Assert.Contains(ExpectedNumberColumn(field), sql, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownNumberFilter_ShouldLeaveQueryUnchanged()
    {
        using var database = new SqliteTestDatabase();
        using var context = database.CreateContext();
        var criteria = Criteria(numbers:
        [
            new BookSearchNumberFilter((BookSearchNumberField)int.MaxValue, BookSearchOperator.Equal, 5)
        ]);

        Assert.DoesNotContain("WHERE", Apply(context, criteria).ToQueryString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(DateCases))]
    public void DateFilters_ShouldGenerateQueryForEveryFieldOperatorAndProvider(
        BookSearchDateField field,
        BookSearchOperator op,
        bool postgres)
    {
        using var context = CreateQueryContext(postgres);
        var criteria = Criteria(dates: [new BookSearchDateFilter(field, op, new DateOnly(2026, 7, 15))]);

        var sql = Apply(context, criteria).ToQueryString();

        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        var column = field == BookSearchDateField.Created ? "Created" : "LastModified";
        Assert.Contains(column, sql, StringComparison.Ordinal);
        AssertDateOperatorShape(sql, op);
    }

    [Theory]
    [MemberData(nameof(OperatorExpectedTitles))]
    public async Task CreatedDateFilters_ShouldReturnExactResultsForEveryOperator(
        BookSearchOperator op,
        string[] expectedTitles)
    {
        await AssertDateResultsAsync(BookSearchDateField.Created, op, expectedTitles);
    }

    [Theory]
    [MemberData(nameof(OperatorExpectedTitles))]
    public async Task LastModifiedDateFilters_ShouldReturnExactResultsForEveryOperator(
        BookSearchOperator op,
        string[] expectedTitles)
    {
        await AssertDateResultsAsync(BookSearchDateField.LastModified, op, expectedTitles);
    }

    [Fact]
    public void UnknownDateFilter_ShouldLeaveQueryUnchanged()
    {
        using var database = new SqliteTestDatabase();
        using var context = database.CreateContext();
        var criteria = Criteria(dates:
        [
            new BookSearchDateFilter((BookSearchDateField)int.MaxValue, BookSearchOperator.Equal,
                new DateOnly(2026, 7, 15))
        ]);

        Assert.DoesNotContain("WHERE", Apply(context, criteria).ToQueryString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingFilters_ShouldSelectOnlyBooksMissingTheRequestedMetadata()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var complete = AddBookWithMetadata(context, database.UserId, "Complete");
        var expected = new Dictionary<BookSearchMissingField, Guid>();

        foreach (var field in Enum.GetValues<BookSearchMissingField>())
        {
            var book = AddBookWithMetadata(context, database.UserId, $"Missing {field}", field);
            expected[field] = book.Id;
        }

        await context.SaveChangesAsync();
        var applier = new BookSearchCriteriaApplier(context);

        foreach (var (field, expectedId) in expected)
        {
            var ids = await applier.Apply(
                    context.Books.AsNoTracking(),
                    Criteria(missing: [new BookSearchMissingFilter(field)]))
                .Select(book => book.Id)
                .ToArrayAsync();

            Assert.Equal([expectedId], ids);
            Assert.DoesNotContain(complete.Id, ids);
        }
    }

    [Fact]
    public async Task MissingCoverFilter_ShouldMatchEveryCoverWithoutAUsableStoredImage()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var usableFound = AddBookWithMetadata(context, database.UserId, "Usable Found");
        var usableUploaded = AddBookWithMetadata(context, database.UserId, "Usable Uploaded");
        usableUploaded.Cover!.Status = BookCoverStatus.Uploaded;
        usableUploaded.Cover.StoragePath = null;
        usableUploaded.Cover.ThumbnailStoragePath = $"{usableUploaded.Id:N}/thumbnail.jpg";

        var missingCover = AddBookWithMetadata(
            context,
            database.UserId,
            "No Cover",
            BookSearchMissingField.Cover);
        var pending = AddBookWithMetadata(context, database.UserId, "Pending Cover");
        pending.Cover!.Status = BookCoverStatus.Pending;
        var failed = AddBookWithMetadata(context, database.UserId, "Failed Cover");
        failed.Cover!.Status = BookCoverStatus.Failed;
        var notFound = AddBookWithMetadata(context, database.UserId, "Not Found Cover");
        notFound.Cover!.Status = BookCoverStatus.NotFound;
        var foundWithoutStorage = AddBookWithMetadata(context, database.UserId, "Found Without Storage");
        foundWithoutStorage.Cover!.StoragePath = null;

        await context.SaveChangesAsync();

        var ids = await Apply(
                context,
                Criteria(missing: [new BookSearchMissingFilter(BookSearchMissingField.Cover)]))
            .Select(book => book.Id)
            .ToArrayAsync();

        Assert.Equal(
            new[]
            {
                failed.Id,
                foundWithoutStorage.Id,
                missingCover.Id,
                notFound.Id,
                pending.Id
            }.Order(),
            ids.Order());
        Assert.DoesNotContain(usableFound.Id, ids);
        Assert.DoesNotContain(usableUploaded.Id, ids);
    }

    [Fact]
    public async Task MissingDescriptionFilter_ShouldMatchNullEmptyAndWhitespaceValues()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var complete = AddBookWithMetadata(context, database.UserId, "Complete Description");
        var missing = AddBookWithMetadata(
            context,
            database.UserId,
            "Null Description",
            BookSearchMissingField.Description);
        var empty = AddBookWithMetadata(context, database.UserId, "Empty Description");
        empty.Description = string.Empty;
        var whitespace = AddBookWithMetadata(context, database.UserId, "Whitespace Description");
        whitespace.Description = "   ";
        await context.SaveChangesAsync();

        var ids = await Apply(
                context,
                Criteria(missing: [new BookSearchMissingFilter(BookSearchMissingField.Description)]))
            .Select(book => book.Id)
            .ToArrayAsync();

        Assert.Equal(new[] { empty.Id, missing.Id, whitespace.Id }.Order(), ids.Order());
        Assert.DoesNotContain(complete.Id, ids);
    }

    [Fact]
    public async Task MissingAlternateTitleFilter_ShouldIgnorePrimaryAndWhitespaceOnlyTitles()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var complete = AddBookWithMetadata(context, database.UserId, "Complete Alternate Title");
        var missing = AddBookWithMetadata(
            context,
            database.UserId,
            "Primary Title Only",
            BookSearchMissingField.AlternateTitle);
        var whitespace = AddBookWithMetadata(
            context,
            database.UserId,
            "Whitespace Alternate",
            BookSearchMissingField.AlternateTitle);
        whitespace.Titles.Add(new BookTitle
        {
            Title = "   ",
            NormalizedTitle = string.Empty,
            IsPrimary = false,
            Source = "Test"
        });
        await context.SaveChangesAsync();

        var ids = await Apply(
                context,
                Criteria(missing: [new BookSearchMissingFilter(BookSearchMissingField.AlternateTitle)]))
            .Select(book => book.Id)
            .ToArrayAsync();

        Assert.Equal(new[] { missing.Id, whitespace.Id }.Order(), ids.Order());
        Assert.DoesNotContain(complete.Id, ids);
    }

    [Fact]
    public void UnknownMissingFilter_ShouldLeaveQueryUnchanged()
    {
        using var database = new SqliteTestDatabase();
        using var context = database.CreateContext();
        var criteria = Criteria(missing:
        [
            new BookSearchMissingFilter((BookSearchMissingField)int.MaxValue)
        ]);

        Assert.DoesNotContain("WHERE", Apply(context, criteria).ToQueryString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MultipleCriteriaGroups_ShouldBeCombinedWithAnd()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var match = AddBookWithMetadata(context, database.UserId, "Needle Match");
        match.Rating = 8;
        var wrongRating = AddBookWithMetadata(context, database.UserId, "Needle Wrong Rating");
        wrongRating.Rating = 4;
        var wrongTitle = AddBookWithMetadata(context, database.UserId, "Other Match");
        wrongTitle.Rating = 8;
        await context.SaveChangesAsync();

        var criteria = Criteria(
            ["Needle"],
            numbers:
            [
                new BookSearchNumberFilter(BookSearchNumberField.Rating, BookSearchOperator.GreaterThanOrEqual, 8)
            ],
            missing: [new BookSearchMissingFilter(BookSearchMissingField.Priority)]);
        match.Priority = null;
        wrongRating.Priority = null;
        wrongTitle.Priority = null;
        await context.SaveChangesAsync();

        var ids = await Apply(context, criteria).Select(book => book.Id).ToArrayAsync();

        Assert.Equal([match.Id], ids);
    }

    private static IQueryable<Book> Apply(ApplicationDbContext context, BookSearchCriteria criteria)
    {
        return new BookSearchCriteriaApplier(context).Apply(context.Books.AsNoTracking(), criteria);
    }

    private static async Task<Guid[]> SearchTermsAsync(ApplicationDbContext context, string term)
    {
        return await Apply(context, Criteria([term]))
            .Select(book => book.Id)
            .ToArrayAsync();
    }

    private static async Task<Guid[]> SearchFieldAsync(
        ApplicationDbContext context,
        BookSearchField field,
        params string[] values)
    {
        return await Apply(
                context,
                Criteria(fields: [new BookSearchFieldFilter(field, values)]))
            .Select(book => book.Id)
            .ToArrayAsync();
    }

    private static ApplicationDbContext CreateQueryContext(bool postgres)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
        if (postgres)
        {
            builder.UseNpgsql("Host=localhost;Database=query-generation-only;Username=test;Password=test");
        }
        else
        {
            builder.UseSqlite("Data Source=:memory:");
        }

        return new ApplicationDbContext(builder.Options, TestUser.Instance);
    }

    private static BookSearchCriteria Criteria(
        IReadOnlyCollection<string>? terms = null,
        IReadOnlyCollection<BookSearchFieldFilter>? fields = null,
        IReadOnlyCollection<BookSearchNumberFilter>? numbers = null,
        IReadOnlyCollection<BookSearchDateFilter>? dates = null,
        IReadOnlyCollection<BookSearchMissingFilter>? missing = null)
    {
        return new BookSearchCriteria(
            terms ?? Array.Empty<string>(),
            fields ?? Array.Empty<BookSearchFieldFilter>(),
            numbers ?? Array.Empty<BookSearchNumberFilter>(),
            dates ?? Array.Empty<BookSearchDateFilter>(),
            missing ?? Array.Empty<BookSearchMissingFilter>());
    }

    private static IReadOnlyCollection<string> ExpectedTitles(BookSearchOperator op)
    {
        return op switch
        {
            BookSearchOperator.GreaterThan => ["High"],
            BookSearchOperator.GreaterThanOrEqual => ["Equal", "High"],
            BookSearchOperator.LessThan => ["Low"],
            BookSearchOperator.LessThanOrEqual => ["Equal", "Low"],
            _ => ["Equal"]
        };
    }

    private static string ExpectedComparisonOperator(BookSearchOperator op)
    {
        return op switch
        {
            BookSearchOperator.GreaterThan => " > ",
            BookSearchOperator.GreaterThanOrEqual => " >= ",
            BookSearchOperator.LessThan => " < ",
            BookSearchOperator.LessThanOrEqual => " <= ",
            _ => " = "
        };
    }

    private static string ExpectedNumberColumn(BookSearchNumberField field)
    {
        return field switch
        {
            BookSearchNumberField.Rating => "\"Rating\"",
            BookSearchNumberField.Priority => "\"Priority\"",
            BookSearchNumberField.CurrentChapter => "\"CurrentChapterNumber\"",
            BookSearchNumberField.TotalChapters => "\"TotalChapters\"",
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
        };
    }

    private static IReadOnlyCollection<string> ExpectedPostgresFieldFragments(BookSearchField field)
    {
        return field switch
        {
            BookSearchField.Title => ["\"PrimaryTitle\"", "\"BookTitles\""],
            BookSearchField.Author => ["\"Authors\"", "\"AuthorNames\""],
            BookSearchField.Tag => ["\"BookTag\"", "\"Tags\""],
            BookSearchField.Genre => ["\"BookGenre\"", "\"Genres\""],
            BookSearchField.Status => ["\"Statuses\""],
            BookSearchField.Type => ["\"ContentTypes\""],
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
        };
    }

    private static void SetNumber(Book book, BookSearchNumberField field, decimal value)
    {
        switch (field)
        {
            case BookSearchNumberField.Rating:
                book.Rating = (int)value;
                break;
            case BookSearchNumberField.Priority:
                book.Priority = (int)value;
                break;
            case BookSearchNumberField.CurrentChapter:
                book.CurrentChapterNumber = value;
                break;
            case BookSearchNumberField.TotalChapters:
                book.TotalChapters = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(field), field, null);
        }
    }

    private static async Task AssertDateResultsAsync(
        BookSearchDateField field,
        BookSearchOperator op,
        IReadOnlyCollection<string> expectedTitles)
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var low = TestData.Book(database.UserId, "Low");
        var equal = TestData.Book(database.UserId, "Equal");
        var high = TestData.Book(database.UserId, "High");
        context.Books.AddRange(low, equal, high);
        await context.SaveChangesAsync();

        var unrelatedDate = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
        await SetAuditDatesAsync(context, low.Id, field, new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero),
            unrelatedDate);
        await SetAuditDatesAsync(context, equal.Id, field, new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero),
            unrelatedDate);
        await SetAuditDatesAsync(context, high.Id, field, new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero),
            unrelatedDate);
        context.ChangeTracker.Clear();

        var titles = await Apply(
                context,
                Criteria(dates:
                [
                    new BookSearchDateFilter(field, op, new DateOnly(2026, 7, 15))
                ]))
            .Select(book => book.PrimaryTitle)
            .ToArrayAsync();

        Assert.Equal(expectedTitles.Order(), titles.Order());
    }

    private static Task SetAuditDatesAsync(
        ApplicationDbContext context,
        Guid bookId,
        BookSearchDateField field,
        DateTimeOffset targetDate,
        DateTimeOffset unrelatedDate)
    {
        var created = field == BookSearchDateField.Created ? targetDate : unrelatedDate;
        var lastModified = field == BookSearchDateField.LastModified ? targetDate : unrelatedDate;
        return context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Books SET Created = {created}, LastModified = {lastModified} WHERE Id = {bookId}");
    }

    private static void AssertDateOperatorShape(string sql, BookSearchOperator op)
    {
        switch (op)
        {
            case BookSearchOperator.GreaterThan:
                Assert.Contains(">=", sql, StringComparison.Ordinal);
                Assert.Contains("2026-07-16", sql, StringComparison.Ordinal);
                break;
            case BookSearchOperator.GreaterThanOrEqual:
                Assert.Contains(">=", sql, StringComparison.Ordinal);
                Assert.Contains("2026-07-15", sql, StringComparison.Ordinal);
                break;
            case BookSearchOperator.LessThan:
                Assert.Contains(" < ", sql, StringComparison.Ordinal);
                Assert.Contains("2026-07-15", sql, StringComparison.Ordinal);
                break;
            case BookSearchOperator.LessThanOrEqual:
                Assert.Contains(" < ", sql, StringComparison.Ordinal);
                Assert.Contains("2026-07-16", sql, StringComparison.Ordinal);
                break;
            default:
                Assert.Contains(">=", sql, StringComparison.Ordinal);
                Assert.Contains(" < ", sql, StringComparison.Ordinal);
                Assert.Contains("2026-07-15", sql, StringComparison.Ordinal);
                Assert.Contains("2026-07-16", sql, StringComparison.Ordinal);
                break;
        }
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }

    private static Book AddBookWithMetadata(
        ApplicationDbContext context,
        Guid ownerId,
        string title,
        BookSearchMissingField? missing = null)
    {
        var author = missing == BookSearchMissingField.Author ? null : TestData.Author($"{title} Author");
        var book = TestData.Book(ownerId, title, author);
        book.Description = missing == BookSearchMissingField.Description ? null : $"{title} description";
        book.Rating = missing == BookSearchMissingField.Rating ? null : 8;
        book.Priority = missing == BookSearchMissingField.Priority ? null : 2;
        book.CurrentChapterNumber = missing == BookSearchMissingField.CurrentChapter ? null : 50;
        book.TotalChapters = missing == BookSearchMissingField.TotalChapters ? null : 100;

        if (missing != BookSearchMissingField.Genre)
        {
            book.BookGenres.Add(new Domain.Associations.BookGenre
            {
                Book = book, Genre = TestData.Genre($"{title} Genre")
            });
        }

        if (missing != BookSearchMissingField.Tag)
        {
            book.BookTags.Add(new Domain.Associations.BookTag
            {
                Book = book, Tag = TestData.Tag(ownerId, $"{title} Tag")
            });
        }

        if (missing != BookSearchMissingField.AlternateTitle)
        {
            book.Titles.Add(new BookTitle
            {
                Title = $"{title} Alternate",
                NormalizedTitle = MappingExtensions.NormalizeName($"{title} Alternate"),
                IsPrimary = false,
                Source = "Test"
            });
        }

        if (missing != BookSearchMissingField.Cover)
        {
            book.Cover = new BookCover
            {
                Status = BookCoverStatus.Found,
                StoragePath = $"{book.Id:N}/cover.jpg"
            };
        }

        if (missing != BookSearchMissingField.Link)
        {
            book.Links.Add(new BookLink { Url = $"https://example.com/{book.Id}", SourceType = "Test" });
        }

        context.Books.Add(book);
        return book;
    }

    private sealed class TestUser : IUser
    {
        public static TestUser Instance { get; } = new();
        public Guid? Id => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public Guid RequiredId => Id!.Value;
        public string? Email => "criteria@example.com";
        public string? Username => "criteria";
        public IEnumerable<string> Roles => Array.Empty<string>();
        public bool IsAuthenticated => true;
        public bool Valid => true;
    }
}
