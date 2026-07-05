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
        var otherBook = await repository.GetByNameAsync("Other", database.UserId, CancellationToken.None);

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
    public async Task BookRepository_ShouldReplaceCollectionsAfterDuplicateTitleLookup()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var book = await TestData.AddBookWithRelationsAsync(context, database.UserId);
        var genreId = book.BookGenres.First().GenreId;
        var tagId = book.BookTags.First().TagId;
        var repository = new BookRepository(context);

        var editableBook = await repository.GetForUpdateAsync(book.Id, database.UserId, CancellationToken.None);
        var duplicateCheck = await repository.GetByNameAsync(book.PrimaryTitle, database.UserId, CancellationToken.None);

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
