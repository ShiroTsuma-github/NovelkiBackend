using Application.Common.Interfaces;
using Application.Common.DTOs.Book;
using Domain.Associations;
using Domain.Entities;
using FluentValidation;
using Infrastructure.IntegrationTests.TestSupport;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Infrastructure.IntegrationTests;

public class BookCsvImportServiceTests
{
    [Fact]
    public async Task CreateSessionAsync_ShouldRejectCsvWithoutRequiredColumns()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var service = CreateService(context, database.UserId);

        using var stream = CreateCsv("""
primaryTitle,contentType
Only Title,Novel
""");

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateSessionAsync(stream, "books.csv", CancellationToken.None));

        Assert.Contains("status", exception.Message);
    }

    [Fact]
    public async Task CreateSessionAsync_ShouldNormalizeFieldsAndFlagInvalidRows()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        await TestData.AddBookAsync(context, database.UserId, "Existing Book");
        var service = CreateService(context, database.UserId);

        using var stream = CreateCsv("""
primaryTitle,authorName,contentType,status,tags,totalChapters,currentChapterNumber,rating,priority,notes
 Existing Book , Toika , Novel , Reading , fantasy; favorite , 10 , 11 , 11 , 0 , one|two
 Existing Book , Toika , Novel , Reading , fantasy , 10 , 5 , 8 , 1 , keep
 New Book , , InvalidType , MissingStatus , , nope , -1 , x , 7 , 
""");

        var session = await service.CreateSessionAsync(stream, " import.csv ", CancellationToken.None);

        Assert.Equal("import.csv", session.FileName);
        Assert.Equal(3, session.TotalRows);
        Assert.Equal(0, session.ValidRows);
        Assert.False(session.CanFinalize);
        Assert.Equal(["Novel", "Manga", "Manhwa", "Manhua", "Other"], session.AvailableContentTypes);
        Assert.Equal(["Reading", "Completed", "Plan To Read", "On Hold", "Dropped", "Unknown"], session.AvailableStatuses);

        var existingRows = session.Rows.Where(row => row.PrimaryTitle == "Existing Book").ToList();
        Assert.Equal(2, existingRows.Count);
        Assert.All(existingRows, row => Assert.Contains("Duplicate title with the same content type inside this import session.", row.Errors));
        Assert.Contains(existingRows.SelectMany(row => row.Errors), error => error.Contains("already exists in your library."));
        Assert.Equal("one\ntwo", existingRows[0].Notes);

        var invalidRow = Assert.Single(session.Rows, row => row.PrimaryTitle == "New Book");
        Assert.Contains("Content type is required and must exist. Allowed values: Novel, Manga, Manhwa, Manhua, Other.", invalidRow.Errors);
        Assert.Contains("Status is required and must exist. Allowed values: Reading, Completed, Plan To Read, On Hold, Dropped, Unknown.", invalidRow.Errors);
        Assert.Contains("TotalChapters must be a valid number.", invalidRow.Errors);
        Assert.Contains("Current chapter number cannot be negative.", invalidRow.Errors);
        Assert.Contains("Rating must be a valid integer.", invalidRow.Errors);
        Assert.Contains("Priority must be between 1 and 5.", invalidRow.Errors);
        Assert.Contains("Content type is required and must exist. Allowed values: Novel, Manga, Manhwa, Manhua, Other.", invalidRow.FieldErrors["contentType"]);
        Assert.Contains("Status is required and must exist. Allowed values: Reading, Completed, Plan To Read, On Hold, Dropped, Unknown.", invalidRow.FieldErrors["status"]);
        Assert.Contains("TotalChapters must be a valid number.", invalidRow.FieldErrors["totalChapters"]);
        Assert.Contains("Current chapter number cannot be negative.", invalidRow.FieldErrors["currentChapterNumber"]);
        Assert.Contains("Rating must be a valid integer.", invalidRow.FieldErrors["rating"]);
        Assert.Contains("Priority must be between 1 and 5.", invalidRow.FieldErrors["priority"]);
    }

    [Fact]
    public async Task CreateSessionAsync_ShouldReturnFieldErrorsForMissingTitle()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var service = CreateService(context, database.UserId);

        using var stream = CreateCsv("""
primaryTitle,contentType,status
 ,Novel,Reading
""");

        var session = await service.CreateSessionAsync(stream, "books.csv", CancellationToken.None);
        var row = Assert.Single(session.Rows);

        Assert.Contains("Primary title is required.", row.Errors);
        Assert.Contains("Primary title is required.", row.FieldErrors["primaryTitle"]);
    }

    [Fact]
    public async Task CreateSessionAsync_ShouldExposeDomainOrderedTypeAndStatusSuggestions()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var service = CreateService(context, database.UserId);

        using var stream = CreateCsv("""
primaryTitle,contentType,status
Book,Invalid,Missing
""");

        var session = await service.CreateSessionAsync(stream, "books.csv", CancellationToken.None);

        Assert.Equal(["Novel", "Manga", "Manhwa", "Manhua", "Other"], session.AvailableContentTypes);
        Assert.Equal(["Reading", "Completed", "Plan To Read", "On Hold", "Dropped", "Unknown"], session.AvailableStatuses);
    }

    [Fact]
    public async Task UpdateRowAsync_ShouldRevalidateSessionAfterFixingRow()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var service = CreateService(context, database.UserId);

        using var stream = CreateCsv("""
primaryTitle,contentType,status,totalChapters,currentChapterNumber,rating,priority
Bad Book,Novel,Reading,10,11,11,0
""");

        var session = await service.CreateSessionAsync(stream, "books.csv", CancellationToken.None);
        var row = Assert.Single(session.Rows);

        var updatedSession = await service.UpdateRowAsync(
            session.SessionId,
            row.RowId,
            new UpdateBookImportRowRequest(
                "Better Book",
                "Toika",
                "Novel",
                "Reading",
                "favorite; favorite; action",
                "12",
                "10",
                "10",
                "9",
                "1",
                "desc",
                "alpha|beta",
                "raw"),
            CancellationToken.None);

        var updatedRow = Assert.Single(updatedSession.Rows);
        Assert.True(updatedRow.IsValid);
        Assert.Empty(updatedRow.Errors);
        Assert.Equal("alpha\nbeta", updatedRow.Notes);
        Assert.True(updatedSession.CanFinalize);
    }

    [Fact]
    public async Task FinalizeAsync_ShouldCreateBooksTagsAuthorsAndQueueCovers()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var queue = new TrackingBookCoverQueue();
        var cacheInvalidator = new TrackingCacheInvalidator();
        var service = CreateService(context, database.UserId, queue, cacheInvalidator);

        using var stream = CreateCsv("""
primaryTitle,authorName,contentType,status,tags,totalChapters,currentChapterNumber,currentChapterLabel,rating,priority,description,notes,rawImportedLine
 The Novel , Toika , Novel , Reading , favorite; action; favorite , 200 , 49 , Progress: 49 , 8 , 2 , Desc , line1|line2 , source line
""");

        var session = await service.CreateSessionAsync(stream, "books.csv", CancellationToken.None);

        var result = await service.FinalizeAsync(session.SessionId, CancellationToken.None);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Empty(result.Errors);
        Assert.Equal(database.UserId, cacheInvalidator.InvalidatedOwnerId);
        Assert.Single(queue.BookIds);

        var savedBook = await context.Books
            .Include(b => b.Author)
            .Include(b => b.BookTags).ThenInclude(bt => bt.Tag)
            .Include(b => b.ProgressHistory)
            .Include(b => b.Cover)
            .SingleAsync();

        Assert.Equal("The Novel", savedBook.PrimaryTitle);
        Assert.NotNull(savedBook.Author);
        Assert.Equal("Toika", savedBook.Author!.PrimaryName);
        Assert.Equal(2, savedBook.BookTags.Count);
        Assert.Contains(savedBook.BookTags, bt => bt.Tag.Name == "favorite");
        Assert.Contains(savedBook.BookTags, bt => bt.Tag.Name == "action");
        var progress = Assert.Single(savedBook.ProgressHistory);
        Assert.Equal(49, progress.ChapterNumber);
        Assert.Equal("Progress: 49", progress.ChapterLabel);
        Assert.Equal("Imported from CSV", progress.Comment);
        Assert.NotNull(savedBook.Cover);
        Assert.Equal(BookCoverStatus.Pending, savedBook.Cover!.Status);
        Assert.Equal(savedBook.Id, queue.BookIds[0]);
        Assert.Equal("line1\nline2", savedBook.Notes);
    }

    [Fact]
    public async Task FinalizeAsync_ShouldCollapseWhitespaceInIdentifyingFields()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var service = CreateService(context, database.UserId);

        using var stream = CreateCsv("""
primaryTitle,authorName,contentType,status,tags
 The   Novel , Er    Gen , Novel , Plan   To Read , favorite   tag; action    tag
""");

        var session = await service.CreateSessionAsync(stream, "books.csv", CancellationToken.None);
        var row = Assert.Single(session.Rows);

        Assert.True(row.IsValid);
        Assert.Equal("The Novel", row.PrimaryTitle);
        Assert.Equal("Er Gen", row.AuthorName);
        Assert.Equal("Novel", row.ContentType);
        Assert.Equal("Plan To Read", row.Status);
        Assert.Equal("favorite tag; action tag", row.Tags);

        await service.FinalizeAsync(session.SessionId, CancellationToken.None);

        var savedBook = await context.Books
            .Include(b => b.Author)
            .Include(b => b.BookTags).ThenInclude(bt => bt.Tag)
            .SingleAsync();

        Assert.Equal("The Novel", savedBook.PrimaryTitle);
        Assert.Equal("Er Gen", savedBook.Author!.PrimaryName);
        Assert.Contains(savedBook.BookTags, bt => bt.Tag.Name == "favorite tag");
        Assert.Contains(savedBook.BookTags, bt => bt.Tag.Name == "action tag");
    }

    [Fact]
    public async Task FinalizeAsync_ShouldSkipRowsThatConflictWithinExistingLibrary()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        await TestData.AddBookAsync(context, database.UserId, "Existing Book");
        var service = CreateService(context, database.UserId);

        using var stream = CreateCsv("""
primaryTitle,contentType,status
New Book,Novel,Reading
Existing Book,Novel,Reading
""");

        var session = await service.CreateSessionAsync(stream, "books.csv", CancellationToken.None);
        var invalidRow = Assert.Single(session.Rows, row => row.PrimaryTitle == "Existing Book");

        var fixedSession = await service.DeleteRowAsync(session.SessionId, invalidRow.RowId, CancellationToken.None);
        var result = await service.FinalizeAsync(fixedSession.SessionId, CancellationToken.None);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(2, await context.Books.CountAsync());
    }

    [Fact]
    public async Task DeleteInvalidRowsAsync_ShouldRemoveOnlyInvalidRows()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var service = CreateService(context, database.UserId);

        using var stream = CreateCsv("""
primaryTitle,contentType,status,totalChapters,currentChapterNumber
Valid Book,Novel,Reading,12,10
Invalid Book,Novel,Reading,10,11
""");

        var session = await service.CreateSessionAsync(stream, "books.csv", CancellationToken.None);

        var updatedSession = await service.DeleteInvalidRowsAsync(session.SessionId, CancellationToken.None);

        Assert.Equal(1, updatedSession.TotalRows);
        Assert.Equal(1, updatedSession.ValidRows);
        Assert.Equal(0, updatedSession.InvalidRows);
        Assert.True(updatedSession.CanFinalize);
        Assert.Equal("Valid Book", Assert.Single(updatedSession.Rows).PrimaryTitle);
    }

    [Fact]
    public async Task DeleteInvalidRowsAsync_ShouldKeepSessionUnchangedWhenThereAreNoInvalidRows()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var service = CreateService(context, database.UserId);

        using var stream = CreateCsv("""
primaryTitle,contentType,status,totalChapters,currentChapterNumber
Valid Book,Novel,Reading,12,10
""");

        var session = await service.CreateSessionAsync(stream, "books.csv", CancellationToken.None);

        var updatedSession = await service.DeleteInvalidRowsAsync(session.SessionId, CancellationToken.None);

        Assert.Equal(1, updatedSession.TotalRows);
        Assert.Equal(1, updatedSession.ValidRows);
        Assert.Equal(0, updatedSession.InvalidRows);
        Assert.True(updatedSession.CanFinalize);
    }

    [Fact]
    public async Task CancelAsync_ShouldExpireSession()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var service = CreateService(context, database.UserId);

        using var stream = CreateCsv("""
primaryTitle,contentType,status
Book,Novel,Reading
""");

        var session = await service.CreateSessionAsync(stream, "books.csv", CancellationToken.None);

        await service.CancelAsync(session.SessionId, CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.GetSessionAsync(session.SessionId, CancellationToken.None));
    }

    private static BookCsvImportService CreateService(
        Infrastructure.Contexts.ApplicationDbContext context,
        Guid ownerId,
        TrackingBookCoverQueue? queue = null,
        TrackingCacheInvalidator? cacheInvalidator = null)
    {
        return new BookCsvImportService(
            context,
            queue ?? new TrackingBookCoverQueue(),
            cacheInvalidator ?? new TrackingCacheInvalidator(),
            new TestUser(ownerId));
    }

    private static MemoryStream CreateCsv(string content)
    {
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        return new MemoryStream(Encoding.UTF8.GetBytes(normalized.Replace("\n", Environment.NewLine, StringComparison.Ordinal)));
    }

    private sealed class TrackingBookCoverQueue : IBookCoverQueue
    {
        public List<Guid> BookIds { get; } = [];

        public ValueTask QueueAsync(Guid bookId, CancellationToken cancellationToken)
        {
            BookIds.Add(bookId);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TrackingCacheInvalidator : IBookListCacheInvalidator
    {
        public Guid? InvalidatedOwnerId { get; private set; }

        public Task InvalidateBooksAsync(Guid ownerId, CancellationToken cancellationToken)
        {
            InvalidatedOwnerId = ownerId;
            return Task.CompletedTask;
        }
    }

    private sealed class TestUser : IUser
    {
        public TestUser(Guid id)
        {
            Id = id;
        }

        public Guid? Id { get; }
        public Guid RequiredId => Id!.Value;
        public string? Email => "reader@example.com";
        public string? Username => "reader";
        public IEnumerable<string> Roles => Array.Empty<string>();
        public bool IsAuthenticated => true;
        public bool Valid => true;
    }
}
