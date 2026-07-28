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
        var fuzzyBook = TestData.Book(ownerId, "Space Expedition");
        fuzzyBook.ContentTypeId = Guid.Parse("10000000-0000-0000-0000-000000000004");
        context.Books.Add(fuzzyBook);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT refresh_book_search_index({book.Id})");
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT refresh_book_search_index({fuzzyBook.Id})");

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
        Assert.Equal([ownerId], invalidator.OwnerIds);
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
