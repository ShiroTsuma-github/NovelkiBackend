namespace Infrastructure.IntegrationTests.PostgreSql;

using Application.Common;
using Application.Common.Interfaces;
using Domain.Associations;
using Domain.Entities;
using Infrastructure.BookSearch;
using Infrastructure.Contexts;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using TestSupport;

[Collection(PostgreSqlCollection.CollectionName)]
public sealed class BookSearchIndexPostgreSqlTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task GeneralSearch_ShouldMatchAcrossIndexedFieldsAndIgnoreDescription()
    {
        await fixture.ResetDatabaseAsync();
        var ownerId = Guid.NewGuid();
        await using var context = fixture.CreateContext(ownerId);
        await AddUserAsync(context, ownerId);

        var author = TestData.Author("Cultivator");
        author.Names.Add(new AuthorName
        {
            Name = "Meng Hao",
            NormalizedName = MappingExtensions.NormalizeName("Meng Hao"),
            Source = "Test"
        });
        var action = TestData.Genre("Action");
        var xianxia = TestData.Genre("Xianxia");
        var tag = TestData.Tag(ownerId, "Progression");
        var book = TestData.Book(ownerId, "A Will Eternal", author);
        book.Description = "description-only-secret";
        book.BookGenres.Add(new BookGenre { Book = book, Genre = action });
        book.BookGenres.Add(new BookGenre { Book = book, Genre = xianxia });
        book.BookTags.Add(new BookTag { Book = book, Tag = tag });
        context.Books.Add(book);
        var fuzzyAuthor = TestData.Author("Bei XIANG");
        var fuzzyBook = TestData.Book(ownerId, "Space Bloody Heaven", fuzzyAuthor);
        fuzzyBook.ContentTypeId = Guid.Parse("10000000-0000-0000-0000-000000000004");
        fuzzyBook.BookGenres.Add(new BookGenre { Book = fuzzyBook, Genre = TestData.Genre("Mystery") });
        fuzzyBook.BookGenres.Add(new BookGenre { Book = fuzzyBook, Genre = TestData.Genre("Drama") });
        fuzzyBook.BookTags.Add(new BookTag
        {
            Book = fuzzyBook,
            Tag = TestData.Tag(ownerId, "Manhua")
        });
        fuzzyBook.BookTags.Add(new BookTag
        {
            Book = fuzzyBook,
            Tag = TestData.Tag(ownerId, "Full Color")
        });
        context.Books.Add(fuzzyBook);
        var exactManga = TestData.Book(ownerId, "Ink Adventure");
        exactManga.ContentTypeId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        context.Books.Add(exactManga);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT refresh_book_search_index({book.Id})");
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT refresh_book_search_index({fuzzyBook.Id})");
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT refresh_book_search_index({exactManga.Id})");

        Assert.Equal(
            [book.Id],
            await SearchAsync(context, BookSearchQueryParser.Parse("will action xianxia meng")));
        Assert.Equal(
            [book.Id],
            await SearchAsync(context, BookSearchQueryParser.Parse("xiannxia")));
        Assert.Equal(
            [fuzzyBook.Id],
            await SearchAsync(context, BookSearchQueryParser.Parse("space manjua")));
        Assert.Equal(
            [exactManga.Id],
            await SearchAsync(context, BookSearchQueryParser.Parse("manga")));
        Assert.Equal(
            [fuzzyBook.Id],
            await SearchAsync(context, BookSearchQueryParser.Parse("blood")));
        Assert.Equal(
            [fuzzyBook.Id],
            await SearchAsync(context, BookSearchQueryParser.Parse("-tag:h-manhwa title:\"space")));
        Assert.Empty(await SearchAsync(context, BookSearchQueryParser.Parse("description-only-secret")));
        Assert.Equal(
            [book.Id],
            await SearchAsync(context, BookSearchQueryParser.Parse("description:description-only-secret")));
    }

    [Fact]
    public async Task QueueProcessor_ShouldCoalesceChangesRefreshDocumentAndInvalidateCache()
    {
        await fixture.ResetDatabaseAsync();
        var ownerId = Guid.NewGuid();
        await using var context = fixture.CreateContext(ownerId);
        await AddUserAsync(context, ownerId);

        var genre = TestData.Genre("Fantasy");
        var tag = TestData.Tag(ownerId, "Favorite");
        var book = TestData.Book(ownerId, "Queued Book");
        book.BookGenres.Add(new BookGenre { Book = book, Genre = genre });
        book.BookTags.Add(new BookTag { Book = book, Tag = tag });
        context.Books.Add(book);
        await context.SaveChangesAsync();

        Assert.Equal(1, await context.BookSearchIndexQueueItems.CountAsync());

        genre.Name = "Cultivation";
        tag.Name = "Recommended";
        await context.SaveChangesAsync();

        Assert.Equal(1, await context.BookSearchIndexQueueItems.CountAsync());

        var invalidator = new RecordingCacheInvalidator();
        var processor = new BookSearchIndexQueueProcessor(
            context,
            invalidator,
            TimeProvider.System,
            NullLogger<BookSearchIndexQueueProcessor>.Instance);

        Assert.Equal(1, await processor.ProcessBatchAsync(CancellationToken.None));
        Assert.Empty(await context.BookSearchIndexQueueItems.ToArrayAsync());

        var document = await context.Books
            .Where(candidate => candidate.Id == book.Id)
            .Select(candidate => candidate.SearchDocument)
            .SingleAsync();
        Assert.Contains("queued book", document, StringComparison.Ordinal);
        Assert.Contains("cultivation", document, StringComparison.Ordinal);
        Assert.Contains("recommended", document, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(document, "queued book"));
        Assert.Equal(1, document.Split(' ', StringSplitOptions.RemoveEmptyEntries).Count(word => word == "novel"));
        Assert.Equal([ownerId], invalidator.OwnerIds);
    }

    [Fact]
    public async Task CloseLexemeFunction_ShouldApplyLengthBasedDistanceLimits()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateContext(Guid.Empty);

        Assert.True(await CloseLexemeAsync(context, "manhua", "manjua"));
        Assert.False(await CloseLexemeAsync(context, "manhua", "manga"));
        Assert.True(await CloseLexemeAsync(context, "mysteries", "myst"));
        Assert.True(await CloseLexemeAsync(context, "abcdefghijkl", "abcxefghijkz"));
        Assert.False(await CloseLexemeAsync(context, "abcdefghijkl", "abxxefghijkz"));

        var longTerm = new string('a', 65);
        Assert.True(await CloseLexemeAsync(context, $"{longTerm}z", longTerm));
        Assert.False(await CloseLexemeAsync(context, longTerm, $"{longTerm[..64]}b"));
    }

    [Fact]
    public async Task FuzzyMigration_ShouldUpgradeBackfillAndRollbackFromPreviousSearchMigration()
    {
        const string previousMigration = "20260727221821_AddQueuedBookSearchIndex";
        await fixture.ResetDatabaseAsync(previousMigration);
        var ownerId = Guid.NewGuid();
        await using var context = fixture.CreateContext(ownerId);
        await AddUserAsync(context, ownerId);
        var book = TestData.Book(ownerId, "Upgrade Search Document");
        context.Books.Add(book);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT refresh_book_search_index({book.Id})");

        var beforeUpgrade = await GetSearchDocumentAsync(context, book.Id);
        Assert.Equal(2, CountOccurrences(beforeUpgrade, "upgrade search document"));

        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync();

        Assert.True(await HasCloseLexemeFunctionAsync(context));
        var afterUpgrade = await GetSearchDocumentAsync(context, book.Id);
        Assert.Equal(1, CountOccurrences(afterUpgrade, "upgrade search document"));
        Assert.Equal(
            [book.Id],
            await SearchAsync(context, BookSearchQueryParser.Parse("upgrde")));

        await migrator.MigrateAsync(previousMigration);

        Assert.False(await HasCloseLexemeFunctionAsync(context));
        var afterRollback = await GetSearchDocumentAsync(context, book.Id);
        Assert.Equal(2, CountOccurrences(afterRollback, "upgrade search document"));
    }

    [Fact]
    public async Task GeneralSearchQueryPlan_ShouldUseTrigramIndexBeforeLexemeValidation()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateContext(Guid.Empty);

        var plan = await ExplainFuzzySearchAsync(context, "manjua");

        Assert.Contains("IX_Books_SearchDocument_Trigram", plan, StringComparison.Ordinal);
        Assert.Contains("book_search_has_close_lexeme", plan, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RepositorySave_ShouldRefreshSingleBookImmediately()
    {
        await fixture.ResetDatabaseAsync();
        var ownerId = Guid.NewGuid();
        await using var context = fixture.CreateContext(ownerId);
        await AddUserAsync(context, ownerId);
        var updater = new BookSearchIndexUpdater(
            context,
            NullLogger<BookSearchIndexUpdater>.Instance);
        var repository = new BookRepository(context, updater);
        var book = TestData.Book(ownerId, "Immediate Search Result");

        await repository.AddAsync(book, CancellationToken.None);

        Assert.Empty(await context.BookSearchIndexQueueItems.ToArrayAsync());
        Assert.Equal(
            [book.Id],
            await SearchAsync(context, BookSearchQueryParser.Parse("immediate")));
    }

    private static Task<Guid[]> SearchAsync(
        ApplicationDbContext context,
        Domain.Repositories.BookSearchCriteria criteria)
    {
        return new BookSearchCriteriaApplier(context)
            .Apply(context.Books.AsNoTracking(), criteria)
            .Select(book => book.Id)
            .ToArrayAsync();
    }

    private static async Task AddUserAsync(ApplicationDbContext context, Guid ownerId)
    {
        context.Users.Add(new User
        {
            Id = ownerId,
            UserName = $"reader-{ownerId:N}",
            NormalizedUserName = $"READER-{ownerId:N}",
            Email = $"{ownerId:N}@example.com",
            NormalizedEmail = $"{ownerId:N}@EXAMPLE.COM"
        });
        await context.SaveChangesAsync();
    }

    private static Task<string> GetSearchDocumentAsync(ApplicationDbContext context, Guid bookId)
    {
        return context.Books
            .Where(book => book.Id == bookId)
            .Select(book => book.SearchDocument)
            .SingleAsync();
    }

    private static Task<bool> CloseLexemeAsync(
        ApplicationDbContext context,
        string document,
        string term)
    {
        return context.Database
            .SqlQueryRaw<bool>(
                """
                SELECT public.book_search_has_close_lexeme(
                    to_tsvector('simple', {0}),
                    {1}
                ) AS "Value"
                """,
                document,
                term)
            .SingleAsync();
    }

    private static Task<bool> HasCloseLexemeFunctionAsync(ApplicationDbContext context)
    {
        return context.Database
            .SqlQueryRaw<bool>(
                """
                SELECT (
                    to_regprocedure('public.book_search_has_close_lexeme(tsvector,text)') IS NOT NULL
                ) AS "Value"
                """)
            .SingleAsync();
    }

    private static async Task<string> ExplainFuzzySearchAsync(
        ApplicationDbContext context,
        string term)
    {
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            await using (var settings = connection.CreateCommand())
            {
                settings.CommandText = "SET enable_seqscan = off;";
                await settings.ExecuteNonQueryAsync();
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                EXPLAIN (COSTS OFF)
                SELECT "Id"
                FROM "Books"
                WHERE "SearchVector" @@ plainto_tsquery('simple', @term)
                   OR (
                        @term <% "SearchDocument"
                        AND public.book_search_has_close_lexeme("SearchVector", @term)
                   );
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "term";
            parameter.Value = term;
            command.Parameters.Add(parameter);

            var lines = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lines.Add(reader.GetString(0));
            }

            return string.Join(Environment.NewLine, lines);
        }
        finally
        {
            await using (var reset = connection.CreateCommand())
            {
                reset.CommandText = "RESET enable_seqscan;";
                await reset.ExecuteNonQueryAsync();
            }

            await connection.CloseAsync();
        }
    }

    private static int CountOccurrences(string value, string fragment)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(fragment, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += fragment.Length;
        }

        return count;
    }

    private sealed class RecordingCacheInvalidator : IBookListCacheInvalidator
    {
        public List<Guid> OwnerIds { get; } = [];

        public Task InvalidateBooksAsync(Guid ownerId, CancellationToken cancellationToken)
        {
            OwnerIds.Add(ownerId);
            return Task.CompletedTask;
        }
    }
}
