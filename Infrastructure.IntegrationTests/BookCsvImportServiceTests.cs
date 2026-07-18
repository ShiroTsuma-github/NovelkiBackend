namespace Infrastructure.IntegrationTests;

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Application.Common;
using Application.Common.DTOs.Book;
using Application.Common.Interfaces;
using Contexts;
using Domain.Entities;
using Domain.Models;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Persistence;
using Services;
using TestSupport;

public class BookCsvImportServiceTests
{
    private static readonly IOptions<BookImportSecurityOptions> ImportSecurityOptions =
        Options.Create(new BookImportSecurityOptions());

    private static readonly BookImportSessionStore ImportSessionStore = new(ImportSecurityOptions);
    private static readonly BookImportConcurrencyGate ImportConcurrencyGate = new(ImportSecurityOptions);

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
    public async Task CreateSessionAsync_ShouldAcceptSemicolonDelimitedCsv()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var service = CreateService(context, database.UserId);

        using var stream = CreateCsv("""
                                     primaryTitle;contentType;status
                                     Semicolon Book;Manhua;Reading
                                     """);

        var session = await service.CreateSessionAsync(stream, "books.csv", CancellationToken.None);

        var row = Assert.Single(session.Rows);
        Assert.True(row.IsValid);
        Assert.Equal("Semicolon Book", row.PrimaryTitle);
        Assert.Equal("Manhua", row.ContentType);
        Assert.Equal("Reading", row.Status);
    }

    [Fact]
    public async Task CreateSessionAsync_ShouldRejectCsvAboveConfiguredRowLimit()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var options = Options.Create(new BookImportSecurityOptions { MaxCsvRows = 1 });
        using var store = new BookImportSessionStore(options);
        var service = CreateService(context, database.UserId, securityOptions: options, sessionStore: store,
            concurrencyGate: new BookImportConcurrencyGate(options));
        using var stream = CreateCsv(
            "primaryTitle,contentType,status\nFirst,Novel,Reading\nSecond,Novel,Reading\n");

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateSessionAsync(stream, "books.csv", CancellationToken.None));

        Assert.Contains("more than 1 rows", exception.Message);
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
        Assert.Equal(["Reading", "Completed", "Plan To Read", "On Hold", "Dropped", "Unknown"],
            session.AvailableStatuses);

        var existingRows = session.Rows.Where(row => row.PrimaryTitle == "Existing Book").ToList();
        Assert.Equal(2, existingRows.Count);
        Assert.All(existingRows,
            row => Assert.Contains("Duplicate title with the same content type inside this import session.",
                row.Errors));
        Assert.Contains(existingRows.SelectMany(row => row.Errors),
            error => error.Contains("already exists in your library."));
        Assert.Equal("one\ntwo", existingRows[0].Notes);

        var invalidRow = Assert.Single(session.Rows, row => row.PrimaryTitle == "New Book");
        Assert.Contains("Content type is required and must exist. Allowed values: Novel, Manga, Manhwa, Manhua, Other.",
            invalidRow.Errors);
        Assert.Contains(
            "Status is required and must exist. Allowed values: Reading, Completed, Plan To Read, On Hold, Dropped, Unknown.",
            invalidRow.Errors);
        Assert.Contains("TotalChapters must be a valid number.", invalidRow.Errors);
        Assert.Contains("Current chapter number cannot be negative.", invalidRow.Errors);
        Assert.Contains("Rating must be a valid integer.", invalidRow.Errors);
        Assert.Contains("Priority must be between 1 and 5.", invalidRow.Errors);
        Assert.Contains("Content type is required and must exist. Allowed values: Novel, Manga, Manhwa, Manhua, Other.",
            invalidRow.FieldErrors["contentType"]);
        Assert.Contains(
            "Status is required and must exist. Allowed values: Reading, Completed, Plan To Read, On Hold, Dropped, Unknown.",
            invalidRow.FieldErrors["status"]);
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
        Assert.Equal(["Reading", "Completed", "Plan To Read", "On Hold", "Dropped", "Unknown"],
            session.AvailableStatuses);
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
                null,
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
        context.Genres.Add(new Genre { Name = "Fantasy", NormalizedName = MappingExtensions.NormalizeName("Fantasy") });
        await context.SaveChangesAsync();

        using var stream = CreateCsv("""
                                     primaryTitle,authorName,contentType,status,genres,tags,totalChapters,currentChapterNumber,currentChapterLabel,rating,priority,description,notes,rawImportedLine
                                      The Novel , Toika , Novel , Reading , Fantasy , favorite; action; favorite , 200 , 49 , Progress: 49 , 8 , 2 , Desc , line1|line2 , source line
                                     """);

        var session = await service.CreateSessionAsync(stream, "books.csv", CancellationToken.None);

        var result = await service.FinalizeAsync(session.SessionId, CancellationToken.None);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Empty(result.Errors);
        var importedBook = Assert.Single(result.ImportedBooks);
        Assert.Equal("The Novel", importedBook.PrimaryTitle);
        Assert.Equal("Novel", importedBook.ContentType);
        Assert.Equal("Reading", importedBook.Status);
        Assert.Equal(49, importedBook.CurrentChapterNumber);
        Assert.Equal("Progress: 49", importedBook.CurrentChapterLabel);
        Assert.Equal(200, importedBook.TotalChapters);
        Assert.Equal(database.UserId, cacheInvalidator.InvalidatedOwnerId);
        Assert.Single(queue.BookIds);

        var savedBook = await context.Books
            .Include(b => b.Author)
            .Include(b => b.BookGenres).ThenInclude(bg => bg.Genre)
            .Include(b => b.BookTags).ThenInclude(bt => bt.Tag)
            .Include(b => b.ProgressHistory)
            .Include(b => b.Cover)
            .SingleAsync();

        Assert.Equal("The Novel", savedBook.PrimaryTitle);
        Assert.NotNull(savedBook.Author);
        Assert.Equal("Toika", savedBook.Author!.PrimaryName);
        Assert.NotNull(savedBook.AuthorId);
        Assert.Contains(savedBook.BookGenres, bg => bg.Genre.Name == "Fantasy");
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

        var analytics =
            await new BookAnalyticsQueryService(context, new BookSearchCriteriaApplier(context)).GetAnalyticsAsync(
                database.UserId,
                BookSearchQueryParser.Parse("title:\"The Novel\""),
                new BookAnalyticsScopeSnapshot(
                    "title:\"The Novel\"",
                    DateOnly.FromDateTime(savedBook.Created.UtcDateTime),
                    DateOnly.FromDateTime(savedBook.Created.UtcDateTime).AddDays(1),
                    "week"),
                CancellationToken.None);

        var authorCompleteness =
            Assert.Single(analytics.Quality.FieldCompleteness, item => item.Field == "author");
        Assert.Equal(1, authorCompleteness.BookCount);
        Assert.Equal(1d, authorCompleteness.ShareOfBooks, 6);
    }

    [Fact]
    public async Task FinalizeAsync_ShouldRoundTripExportedBookMetadata()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        context.Genres.Add(new Genre { Name = "Fantasy", NormalizedName = MappingExtensions.NormalizeName("Fantasy") });
        await context.SaveChangesAsync();
        var service = CreateService(context, database.UserId);
        var csv = new BookCsvExportService().Build([
            new BookDto
            {
                PrimaryTitle = "Round Trip Book",
                AlternativeTitles = ["Alternate title"],
                Author = "Author Name",
                ContentType = "Novel",
                Status = "Reading",
                CurrentChapterNumber = 12,
                CurrentChapterLabel = "12.5",
                TotalChapters = 100,
                Rating = 8,
                Priority = 2,
                Description = "Description",
                Notes = "Notes",
                RawImportedLine = "Original source line",
                Genres = ["Fantasy"],
                Tags = ["favorite"],
                Links =
                [
                    new BookLinkDto
                    {
                        Id = Guid.NewGuid(),
                        Url = "https://example.com/book",
                        Label = "Official",
                        SourceType = "Official",
                        IsPrimary = true
                    }
                ],
                ProgressHistory =
                [
                    new BookProgressHistoryDto
                    {
                        Id = Guid.NewGuid(),
                        ChangedAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
                        ChapterNumber = 12,
                        ChapterLabel = "12.5",
                        Comment = "Read today"
                    }
                ]
            }
        ]);
        using var stream = CreateCsv(csv);

        var session = await service.CreateSessionAsync(stream, "books.csv", CancellationToken.None);
        Assert.True(session.CanFinalize);
        await service.FinalizeAsync(session.SessionId, CancellationToken.None);

        var saved = await context.Books
            .Include(book => book.Author)
            .Include(book => book.Titles)
            .Include(book => book.BookGenres).ThenInclude(bookGenre => bookGenre.Genre)
            .Include(book => book.BookTags).ThenInclude(bookTag => bookTag.Tag)
            .Include(book => book.Links)
            .Include(book => book.ProgressHistory)
            .SingleAsync();

        Assert.Equal("Author Name", saved.Author!.PrimaryName);
        Assert.Equal("Description", saved.Description);
        Assert.Equal("Notes", saved.Notes);
        Assert.Equal("Original source line", saved.RawImportedLine);
        Assert.Contains(saved.Titles, title => title.Title == "Alternate title" && !title.IsPrimary);
        Assert.Contains(saved.BookGenres, genre => genre.Genre.Name == "Fantasy");
        Assert.Contains(saved.BookTags, tag => tag.Tag.Name == "favorite");
        Assert.Contains(saved.Links, link => link.Url == "https://example.com/book" && link.IsPrimary);
        var history = Assert.Single(saved.ProgressHistory);
        Assert.Equal("Read today", history.Comment);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), history.ChangedAt);
    }

    [Fact]
    public async Task FullImport_ShouldRestoreCoverWithoutQueuingAutomaticLookup()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var queue = new TrackingBookCoverQueue();
        var storage = new StubBookCoverStorage();
        var service = CreateService(context, database.UserId, queue, coverStorage: storage);
        using var archive = CreateFullBackup(
            "primaryTitle,contentType,status\nBackup Book,Novel,Reading\n",
            new BookFullBackupManifest
            {
                Books =
                [
                    new BookFullBackupManifestItem
                    {
                        PrimaryTitle = "Backup Book",
                        ContentType = "Novel",
                        OriginalCoverPath = "covers/000001/original.jpg",
                        OriginalCoverMimeType = "image/jpeg"
                    }
                ]
            },
            "covers/000001/original.jpg",
            [0xFF, 0xD8, 0xFF, 0x00]);

        var session = await service.CreateFullSessionAsync(archive, "backup.zip", CancellationToken.None);
        var result = await service.FinalizeAsync(session.SessionId, CancellationToken.None);

        Assert.Equal(1, result.ImportedCount);
        Assert.Empty(result.Errors);
        Assert.Empty(queue.BookIds);
        Assert.Equal(1, storage.SaveCount);
        Assert.Equal([0xFF, 0xD8, 0xFF, 0x00], storage.SavedContent);

        var book = await context.Books.Include(item => item.Cover).SingleAsync();
        Assert.Equal(BookCoverStatus.Uploaded, book.Cover!.Status);
        Assert.Equal(BookCoverSource.ManualUpload, book.Cover.Source);
        Assert.NotNull(book.Cover.StoragePath);
        Assert.NotNull(book.Cover.ThumbnailStoragePath);
    }

    [Fact]
    public async Task FullImport_ShouldRejectUnexpectedArchiveEntries()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var service = CreateService(context, database.UserId);
        using var archive = CreateValidFullBackup("Unexpected Entry Book", zip =>
        {
            var entry = zip.CreateEntry("payload.bin");
            using var stream = entry.Open();
            stream.Write([1, 2, 3]);
        });

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateFullSessionAsync(archive, "backup.zip", CancellationToken.None));

        Assert.Contains("unexpected entry", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FullImport_ShouldRejectUnsafeCoverPaths()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var service = CreateService(context, database.UserId);
        using var archive = CreateFullBackup(
            "primaryTitle,contentType,status\nUnsafe Book,Novel,Reading\n",
            new BookFullBackupManifest
            {
                Books =
                [
                    new BookFullBackupManifestItem
                    {
                        PrimaryTitle = "Unsafe Book",
                        ContentType = "Novel",
                        OriginalCoverPath = "covers/../escape.jpg",
                        OriginalCoverMimeType = "image/jpeg"
                    }
                ]
            },
            "covers/../escape.jpg",
            [0xFF, 0xD8, 0xFF, 0x00]);

        var exception = await Assert.ThrowsAsync<AccountTemporarilyBlockedException>(() =>
            service.CreateFullSessionAsync(archive, "backup.zip", CancellationToken.None));

        Assert.True(exception.BlockedUntilUtc > DateTimeOffset.UtcNow.AddHours(23));

        using var validArchive = CreateValidFullBackup("Blocked Attempt");
        await Assert.ThrowsAsync<AccountTemporarilyBlockedException>(() =>
            service.CreateFullSessionAsync(validArchive, "valid.zip", CancellationToken.None));
    }

    [Fact]
    public async Task FullImport_ShouldRejectSuspiciousCompressionRatio()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var options = Options.Create(new BookImportSecurityOptions { SuspiciousCompressionMinimumBytes = 128 * 1024 });
        var service = CreateService(context, database.UserId, securityOptions: options);
        var oversizedTitle = new string('A', 500_000);
        using var archive = CreateFullBackup(
            $"primaryTitle,contentType,status\n{oversizedTitle},Novel,Reading\n",
            new BookFullBackupManifest(),
            "covers/000001/original.jpg",
            [0xFF, 0xD8, 0xFF, 0x00]);

        var exception = await Assert.ThrowsAsync<AccountTemporarilyBlockedException>(() =>
            service.CreateFullSessionAsync(archive, "backup.zip", CancellationToken.None));

        Assert.True(exception.BlockedUntilUtc > DateTimeOffset.UtcNow.AddHours(23));
    }

    [Fact]
    public async Task FullImport_SmallHighlyCompressibleEntry_ShouldBeRejectedWithoutApplyingBlock()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var service = CreateService(context, database.UserId);
        var repetitiveTitle = new string('A', 500_000);
        using var archive = CreateFullBackup(
            $"primaryTitle,contentType,status\n{repetitiveTitle},Novel,Reading\n",
            new BookFullBackupManifest(),
            "covers/000001/original.jpg",
            [0xFF, 0xD8, 0xFF, 0x00]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateFullSessionAsync(archive, "compressed.zip", CancellationToken.None));
        Assert.Contains("compression ratio", exception.Message, StringComparison.OrdinalIgnoreCase);

        using var validArchive = CreateValidFullBackup("Allowed After Small Compressed Entry");
        var session = await service.CreateFullSessionAsync(validArchive, "valid.zip", CancellationToken.None);
        await service.CancelAsync(session.SessionId, CancellationToken.None);
    }

    [Fact]
    public async Task FullImport_SymbolicLinkEntry_ShouldApplyBlock()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var service = CreateService(context, database.UserId);
        using var archive = CreateValidFullBackup("Link Attempt", zip =>
        {
            var entry = zip.CreateEntry("covers/link");
            entry.ExternalAttributes = unchecked((int)0xA0000000);
            using var stream = entry.Open();
            stream.WriteByte(1);
        });

        await Assert.ThrowsAsync<AccountTemporarilyBlockedException>(() =>
            service.CreateFullSessionAsync(archive, "link.zip", CancellationToken.None));
    }

    [Fact]
    public async Task FullImport_MissingManifest_ShouldNotBlockLaterValidImport()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var service = CreateService(context, database.UserId);
        using var archive = new MemoryStream();
        using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, true))
        {
            var csvEntry = zip.CreateEntry("books.csv", CompressionLevel.NoCompression);
            using var writer = new StreamWriter(csvEntry.Open(), Encoding.UTF8);
            writer.Write("primaryTitle,contentType,status\nMissing Manifest,Novel,Reading\n");
        }

        archive.Position = 0;
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateFullSessionAsync(archive, "missing-manifest.zip", CancellationToken.None));
        Assert.Contains("manifest.json", exception.Message, StringComparison.OrdinalIgnoreCase);

        using var validArchive = CreateValidFullBackup("Allowed After Ordinary Error");
        var session = await service.CreateFullSessionAsync(validArchive, "valid.zip", CancellationToken.None);
        await service.CancelAsync(session.SessionId, CancellationToken.None);
    }

    [Fact]
    public async Task FullImport_Admin_ShouldRejectSuspiciousArchiveWithoutApplyingBlock()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var options = Options.Create(new BookImportSecurityOptions { SuspiciousCompressionMinimumBytes = 128 * 1024 });
        var service = CreateService(context, database.UserId, securityOptions: options,
            roles: [AuthorizationRoles.Admin]);
        var oversizedTitle = new string('A', 500_000);
        using var suspiciousArchive = CreateFullBackup(
            $"primaryTitle,contentType,status\n{oversizedTitle},Novel,Reading\n",
            new BookFullBackupManifest(),
            "covers/000001/original.jpg",
            [0xFF, 0xD8, 0xFF, 0x00]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateFullSessionAsync(suspiciousArchive, "suspicious.zip", CancellationToken.None));
        Assert.Contains("extreme compression ratio", exception.Message, StringComparison.OrdinalIgnoreCase);

        using var validArchive = CreateValidFullBackup("Admin Still Allowed");
        var session = await service.CreateFullSessionAsync(validArchive, "valid.zip", CancellationToken.None);
        await service.CancelAsync(session.SessionId, CancellationToken.None);
    }

    [Fact]
    public async Task FullImport_ShouldLimitActiveDraftsAndReleaseStagedDiskOnCancel()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var options = Options.Create(new BookImportSecurityOptions
        {
            MaxActiveFullSessionsGlobal = 2, MaxActiveFullSessionsPerUser = 1
        });
        using var store = new BookImportSessionStore(options);
        var gate = new BookImportConcurrencyGate(options);
        var service = CreateService(context, database.UserId, securityOptions: options, sessionStore: store,
            concurrencyGate: gate);
        using var firstArchive = CreateValidFullBackup("First Draft");
        var first = await service.CreateFullSessionAsync(firstArchive, "first.zip", CancellationToken.None);

        Assert.Equal(1, store.ActiveFullSessionCount);
        Assert.True(store.ReservedStagedBytes > 0);
        using var secondArchive = CreateValidFullBackup("Second Draft");
        var exception = await Assert.ThrowsAsync<FullImportCapacityExceededException>(() =>
            service.CreateFullSessionAsync(secondArchive, "second.zip", CancellationToken.None));
        Assert.Contains("active full import drafts", exception.Message, StringComparison.OrdinalIgnoreCase);

        await service.CancelAsync(first.SessionId, CancellationToken.None);
        Assert.Equal(0, store.ActiveFullSessionCount);
        Assert.Equal(0, store.ReservedStagedBytes);
    }

    [Fact]
    public void FullImportConcurrencyGate_ShouldRejectOperationsAboveGlobalLimit()
    {
        var options = Options.Create(new BookImportSecurityOptions { MaxConcurrentFullImportOperations = 1 });
        var gate = new BookImportConcurrencyGate(options);
        using var first = gate.TryAcquire();

        Assert.Throws<FullImportCapacityExceededException>(() => gate.TryAcquire());

        first.Dispose();
        using var next = gate.TryAcquire();
    }

    [Fact]
    public async Task FullImport_ShouldLimitActiveDraftsAcrossAllUsers()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var options = Options.Create(new BookImportSecurityOptions
        {
            MaxActiveFullSessionsGlobal = 1, MaxActiveFullSessionsPerUser = 1
        });
        using var store = new BookImportSessionStore(options);
        var gate = new BookImportConcurrencyGate(options);
        var firstService = CreateService(context, database.UserId, securityOptions: options, sessionStore: store,
            concurrencyGate: gate);
        var secondService = CreateService(context, Guid.NewGuid(), securityOptions: options, sessionStore: store,
            concurrencyGate: gate);
        using var firstArchive = CreateValidFullBackup("Global First Draft");
        var first = await firstService.CreateFullSessionAsync(firstArchive, "first.zip", CancellationToken.None);

        using var secondArchive = CreateValidFullBackup("Global Second Draft");
        var exception = await Assert.ThrowsAsync<FullImportCapacityExceededException>(() =>
            secondService.CreateFullSessionAsync(secondArchive, "second.zip", CancellationToken.None));

        Assert.Contains("server", exception.Message, StringComparison.OrdinalIgnoreCase);
        await firstService.CancelAsync(first.SessionId, CancellationToken.None);
    }

    [Fact]
    public async Task ImportSessionStore_ShouldExpireAbandonedDrafts()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var options = Options.Create(new BookImportSecurityOptions
        {
            SessionIdleTimeout = TimeSpan.FromMilliseconds(20),
            SessionAbsoluteLifetime = TimeSpan.FromMilliseconds(40),
            CleanupInterval = TimeSpan.FromMilliseconds(10)
        });
        using var store = new BookImportSessionStore(options);
        var service = CreateService(context, database.UserId, securityOptions: options, sessionStore: store,
            concurrencyGate: new BookImportConcurrencyGate(options));
        using var csv = CreateCsv("primaryTitle,contentType,status\nExpiring Book,Novel,Reading\n");
        var session = await service.CreateSessionAsync(csv, "books.csv", CancellationToken.None);

        await Task.Delay(100);
        store.CleanupExpired();

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.GetSessionAsync(session.SessionId, CancellationToken.None));
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

        var fixedSession =
            await service.DeleteRowAsync(session.SessionId, invalidRow.RowId, CancellationToken.None);
        var
            result = await service.FinalizeAsync(fixedSession.SessionId, CancellationToken.None);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(2, await context.Books.CountAsync());
    }

    [Fact]
    public async Task FinalizeAsync_ShouldRejectInvalidSessionWithoutPersistingOrQueuingCovers()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var queue = new TrackingBookCoverQueue();
        var cacheInvalidator = new TrackingCacheInvalidator();
        var service = CreateService(context, database.UserId, queue, cacheInvalidator);

        using var stream = CreateCsv("""
                                     primaryTitle,contentType,status,totalChapters,currentChapterNumber
                                     Invalid Book,Novel,Reading,10,11
                                     """);

        var session = await service.CreateSessionAsync(stream, "books.csv", CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.FinalizeAsync(session.SessionId, CancellationToken.None));
        Assert.Equal(0, await context.Books.CountAsync());
        Assert.Empty(queue.BookIds);
        Assert.Null(cacheInvalidator.InvalidatedOwnerId);
    }

    [Fact]
    public async Task FinalizeAsync_ShouldAllowOnlyOneConcurrentFinalizationPerSession()
    {
        using var database = new SqliteTestDatabase(Guid.NewGuid());
        await using var context = database.CreateContext();
        var service = CreateService(context, database.UserId);
        using var csv = CreateCsv("primaryTitle,contentType,status\nSingle Finalize,Novel,Reading\n");
        var session = await service.CreateSessionAsync(csv, "books.csv", CancellationToken.None);

        async Task<Exception?> TryFinalizeAsync()
        {
            try
            {
                await service.FinalizeAsync(session.SessionId, CancellationToken.None);
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        var outcomes = await Task.WhenAll(TryFinalizeAsync(), TryFinalizeAsync());

        Assert.Single(outcomes, outcome => outcome == null);
        Assert.Single(outcomes, outcome => outcome is ValidationException);
        Assert.Equal(1, await context.Books.CountAsync());
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

        var updatedSession =
            await service.DeleteInvalidRowsAsync(session.SessionId, CancellationToken.None);

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

        var updatedSession =
            await service.DeleteInvalidRowsAsync(session.SessionId, CancellationToken.None);

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
        ApplicationDbContext context,
        Guid ownerId,
        TrackingBookCoverQueue? queue = null,
        TrackingCacheInvalidator? cacheInvalidator = null,
        IBookCoverStorage? coverStorage = null,
        IOptions<BookImportSecurityOptions>? securityOptions = null,
        BookImportSessionStore? sessionStore = null,
        BookImportConcurrencyGate? concurrencyGate = null,
        AccountAbuseGuard? abuseGuard = null,
        IEnumerable<string>? roles = null)
    {
        var effectiveOptions = securityOptions ?? ImportSecurityOptions;
        var effectiveAbuseGuard = abuseGuard ?? new AccountAbuseGuard(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            effectiveOptions,
            NullLogger<AccountAbuseGuard>.Instance);
        return new BookCsvImportService(
            context,
            queue ?? new TrackingBookCoverQueue(),
            coverStorage ?? new StubBookCoverStorage(),
            cacheInvalidator ?? new TrackingCacheInvalidator(),
            new TestUser(ownerId, roles),
            sessionStore ?? ImportSessionStore,
            concurrencyGate ?? ImportConcurrencyGate,
            effectiveAbuseGuard,
            effectiveOptions);
    }

    private static MemoryStream CreateCsv(string content)
    {
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        return new MemoryStream(
            Encoding.UTF8.GetBytes(normalized.Replace("\n", Environment.NewLine, StringComparison.Ordinal)));
    }

    private static MemoryStream CreateFullBackup(string csv, BookFullBackupManifest manifest, string coverPath,
        byte[] coverBytes, Action<ZipArchive>? addEntries = null)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var csvEntry = archive.CreateEntry("books.csv");
            using (var writer = new StreamWriter(csvEntry.Open(), Encoding.UTF8))
            {
                writer.Write(csv);
            }

            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var manifestStream = manifestEntry.Open())
            {
                JsonSerializer.Serialize(manifestStream, manifest);
            }

            var coverEntry = archive.CreateEntry(coverPath);
            using (var coverStream = coverEntry.Open())
            {
                coverStream.Write(coverBytes);
            }

            addEntries?.Invoke(archive);
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateValidFullBackup(string title, Action<ZipArchive>? addEntries = null)
    {
        return CreateFullBackup(
            $"primaryTitle,contentType,status\n{title},Novel,Reading\n",
            new BookFullBackupManifest
            {
                Books =
                [
                    new BookFullBackupManifestItem
                    {
                        PrimaryTitle = title,
                        ContentType = "Novel",
                        OriginalCoverPath = "covers/000001/original.jpg",
                        OriginalCoverMimeType = "image/jpeg"
                    }
                ]
            },
            "covers/000001/original.jpg",
            [0xFF, 0xD8, 0xFF, 0x00],
            addEntries);
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

    private sealed class StubBookCoverStorage : IBookCoverStorage
    {
        public int SaveCount { get; private set; }
        public byte[] SavedContent { get; private set; } = [];

        public Task<BookCoverStoredFiles> SaveAsync(Guid ownerId, Guid bookId, Stream content, string fileName,
            string? contentType, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            content.CopyTo(buffer);
            SavedContent = buffer.ToArray();
            SaveCount++;
            var stored = new BookCoverStoredFiles(
                new BookCoverStoredVariant($"covers/{ownerId:N}/{bookId:N}/original.jpg", "image/jpeg",
                    SavedContent.Length,
                    600, 900),
                new BookCoverStoredVariant($"covers/{ownerId:N}/{bookId:N}/thumbnail.webp", "image/webp", 10,
                    200, 300));
            return Task.FromResult(stored);
        }

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken)
        {
            return Task.FromResult<Stream>(new MemoryStream());
        }

        public Task DeleteIfExistsAsync(string? storagePath, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestUser : IUser
    {
        public TestUser(Guid id, IEnumerable<string>? roles = null)
        {
            Id = id;
            Roles = roles ?? [];
        }

        public Guid? Id { get; }
        public Guid RequiredId => Id!.Value;
        public string? Email => "reader@example.com";
        public string? Username => "reader";
        public IEnumerable<string> Roles { get; }

        public bool IsAuthenticated => true;
        public bool Valid => true;
    }
}
