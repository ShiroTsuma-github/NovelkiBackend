namespace Infrastructure.Services;

using System.IO.Compression;
using System.Text.Json;
using Application.Common.DTOs.Book;
using FluentValidation;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic.FileIO;

public sealed class BookCsvImportService : IBookCsvImportService
{
    private const string ImportRowNotFoundMessage = "Import row not found.";
    private const int FullBackupVersion = 1;

    private static readonly string[] RequiredColumns =
    [
        BookCsvColumns.PrimaryTitle,
        BookCsvColumns.ContentType,
        BookCsvColumns.Status
    ];

    private static readonly string[] TemplateColumns =
    [
        BookCsvColumns.PrimaryTitle,
        BookCsvColumns.AlternativeTitles,
        BookCsvColumns.AuthorName,
        BookCsvColumns.ContentType,
        BookCsvColumns.Status,
        BookCsvColumns.Genres,
        BookCsvColumns.Tags,
        BookCsvColumns.TotalChapters,
        BookCsvColumns.CurrentChapterNumber,
        BookCsvColumns.CurrentChapterLabel,
        BookCsvColumns.Rating,
        BookCsvColumns.Priority,
        BookCsvColumns.Description,
        BookCsvColumns.Notes,
        BookCsvColumns.RawImportedLine,
        BookCsvColumns.Links,
        BookCsvColumns.ProgressHistory
    ];

    private readonly IBookCoverQueue _bookCoverQueue;
    private readonly IBookCoverStorage _bookCoverStorage;
    private readonly IBookListCacheInvalidator _cacheInvalidator;
    private readonly BookImportConcurrencyGate _concurrencyGate;

    private readonly ApplicationDbContext _context;
    private readonly BookImportSecurityOptions _securityOptions;
    private readonly BookImportSessionStore _sessionStore;
    private readonly IUser _user;

    public BookCsvImportService(
        ApplicationDbContext context,
        IBookCoverQueue bookCoverQueue,
        IBookCoverStorage bookCoverStorage,
        IBookListCacheInvalidator cacheInvalidator,
        IUser user,
        BookImportSessionStore sessionStore,
        BookImportConcurrencyGate concurrencyGate,
        IOptions<BookImportSecurityOptions> securityOptions)
    {
        _context = context;
        _bookCoverQueue = bookCoverQueue;
        _bookCoverStorage = bookCoverStorage;
        _cacheInvalidator = cacheInvalidator;
        _user = user;
        _sessionStore = sessionStore;
        _concurrencyGate = concurrencyGate;
        _securityOptions = securityOptions.Value;
    }

    public string CreateTemplate()
    {
        return string.Join(',', TemplateColumns) + Environment.NewLine;
    }

    public async Task<BookImportSessionDto> CreateSessionAsync(Stream csvStream, string fileName,
        CancellationToken cancellationToken)
    {
        var csvBytes = await ReadStreamToBytesAsync(csvStream, _securityOptions.MaxCsvBytes, "CSV file",
            cancellationToken);
        await using var boundedCsv = new MemoryStream(csvBytes, false);
        var session = await CreateCsvSessionAsync(boundedCsv, fileName, _user.RequiredId, cancellationToken);
        _sessionStore.Add(session);
        return BookCsvImportSessionMapper.ToDto(session);
    }

    public async Task<BookImportSessionDto> CreateFullSessionAsync(Stream archiveStream, string fileName,
        CancellationToken cancellationToken)
    {
        using var operationLease = _concurrencyGate.TryAcquire();
        using var sessionReservation = _sessionStore.ReserveFullSession(_user.RequiredId);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_securityOptions.DraftProcessingTimeout);
        try
        {
            return await CreateFullSessionCoreAsync(archiveStream, fileName, sessionReservation, timeout.Token);
        }
        catch (InvalidDataException)
        {
            throw new ValidationException("Full backup ZIP file is invalid or corrupted.");
        }
        catch (JsonException)
        {
            throw new ValidationException("Full backup manifest is invalid.");
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested &&
                                                 !cancellationToken.IsCancellationRequested)
        {
            throw new BookImportProcessingTimeoutException(
                "Full backup processing exceeded the allowed time. Reduce the archive size and try again.");
        }
    }

    public async Task<BookImportSessionDto> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = GetOwnedSession(sessionId);
        await session.OperationLock.WaitAsync(cancellationToken);
        try
        {
            EnsureSessionIsCurrent(session);
            await RevalidateSessionAsync(session, cancellationToken);
            return BookCsvImportSessionMapper.ToDto(session);
        }
        finally
        {
            session.OperationLock.Release();
        }
    }

    public async Task<BookImportSessionDto> UpdateRowAsync(Guid sessionId, Guid rowId,
        UpdateBookImportRowRequest request, CancellationToken cancellationToken)
    {
        var session = GetOwnedSession(sessionId);
        await session.OperationLock.WaitAsync(cancellationToken);
        try
        {
            EnsureSessionIsCurrent(session);
            var row = session.Rows.FirstOrDefault(item => item.RowId == rowId)
                      ?? throw new ValidationException(ImportRowNotFoundMessage);
            BookCsvImportRowMapper.ApplyRequest(row, request);
            await RevalidateSessionAsync(session, cancellationToken);
            return BookCsvImportSessionMapper.ToDto(session);
        }
        finally
        {
            session.OperationLock.Release();
        }
    }

    public async Task<BookImportSessionDto> DeleteRowAsync(Guid sessionId, Guid rowId,
        CancellationToken cancellationToken)
    {
        var session = GetOwnedSession(sessionId);
        await session.OperationLock.WaitAsync(cancellationToken);
        try
        {
            EnsureSessionIsCurrent(session);
            var removed = session.Rows.RemoveAll(item => item.RowId == rowId);
            if (removed == 0)
            {
                throw new ValidationException(ImportRowNotFoundMessage);
            }

            await RevalidateSessionAsync(session, cancellationToken);
            return BookCsvImportSessionMapper.ToDto(session);
        }
        finally
        {
            session.OperationLock.Release();
        }
    }

    public async Task<BookImportSessionDto> DeleteInvalidRowsAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = GetOwnedSession(sessionId);
        await session.OperationLock.WaitAsync(cancellationToken);
        try
        {
            EnsureSessionIsCurrent(session);
            session.Rows.RemoveAll(item => item.Errors.Count > 0);
            await RevalidateSessionAsync(session, cancellationToken);
            return BookCsvImportSessionMapper.ToDto(session);
        }
        finally
        {
            session.OperationLock.Release();
        }
    }

    public async Task<BookImportFinalizeResultDto> FinalizeAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = GetOwnedSession(sessionId);
        await session.OperationLock.WaitAsync(cancellationToken);
        BookImportConcurrencyGate.Lease? operationLease = null;
        CancellationTokenSource? timeout = null;
        try
        {
            EnsureSessionIsCurrent(session);
            if (session.IsFullImport)
            {
                operationLease = _concurrencyGate.TryAcquire();
                timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(_securityOptions.FinalizeProcessingTimeout);
            }

            return await FinalizeSessionAsync(session, timeout?.Token ?? cancellationToken);
        }
        catch (OperationCanceledException) when (timeout?.IsCancellationRequested == true &&
                                                 !cancellationToken.IsCancellationRequested)
        {
            throw new BookImportProcessingTimeoutException(
                "Full import finalization exceeded the allowed time. Try again later.");
        }
        finally
        {
            timeout?.Dispose();
            operationLease?.Dispose();
            session.OperationLock.Release();
        }
    }

    public async Task CancelAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = GetOwnedSession(sessionId);
        await session.OperationLock.WaitAsync(cancellationToken);
        try
        {
            EnsureSessionIsCurrent(session);
            _sessionStore.Remove(session.SessionId);
        }
        finally
        {
            session.OperationLock.Release();
        }
    }

    private async Task<ImportSession> CreateCsvSessionAsync(Stream csvStream, string fileName, Guid ownerId,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(csvStream, leaveOpen: true);
        var csv = await reader.ReadToEndAsync(cancellationToken);
        using var parser = new TextFieldParser(new StringReader(csv))
        {
            TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false
        };
        parser.SetDelimiters(DetectDelimiter(csv));

        if (parser.EndOfData)
        {
            throw new ValidationException(BookCsvValidationMessages.EmptyFile);
        }

        var headers = parser.ReadFields();
        if (headers == null)
        {
            throw new ValidationException("CSV header could not be read.");
        }

        var headerMap = headers
            .Select((name, index) => new { Name = name.Trim(), Index = index })
            .ToDictionary(item => item.Name, item => item.Index, StringComparer.OrdinalIgnoreCase);

        var missingColumns = RequiredColumns.Where(column => !headerMap.ContainsKey(column)).ToArray();
        if (missingColumns.Length > 0)
        {
            throw new ValidationException($"CSV is missing required columns: {string.Join(", ", missingColumns)}");
        }

        var session = new ImportSession
        {
            SessionId = Guid.NewGuid(),
            OwnerId = ownerId,
            FileName = string.IsNullOrWhiteSpace(fileName) ? "import.csv" : fileName.Trim()
        };

        while (!parser.EndOfData)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string[]? fields;
            try
            {
                fields = parser.ReadFields();
            }
            catch (MalformedLineException ex)
            {
                EnsureImportRowCapacity(session);
                session.Rows.Add(new ImportRow
                {
                    RowId = Guid.NewGuid(),
                    LineNumber = NormalizeLineNumber((int?)ex.LineNumber),
                    Errors = ["Malformed CSV row."]
                });
                continue;
            }

            if (fields == null || fields.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            EnsureImportRowCapacity(session);
            var row = headerMap.ToDictionary(
                pair => pair.Key,
                pair => pair.Value < fields.Length ? fields[pair.Value] : string.Empty,
                StringComparer.OrdinalIgnoreCase);

            session.Rows.Add(BookCsvImportRowMapper.CreateRow(row, NormalizeLineNumber((int?)parser.LineNumber)));
        }

        await RevalidateSessionAsync(session, cancellationToken);
        return session;
    }

    private async Task<BookImportSessionDto> CreateFullSessionCoreAsync(Stream archiveStream, string fileName,
        BookImportSessionStore.FullSessionReservation sessionReservation, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, true);
        var entries = ValidateArchiveMetadata(archive);
        var csvEntry = GetRequiredEntry(entries, "books.csv");
        var manifestEntry = GetRequiredEntry(entries, "manifest.json");
        ValidateEntrySize(csvEntry, _securityOptions.MaxCsvBytes, "books.csv");
        ValidateEntrySize(manifestEntry, _securityOptions.MaxManifestBytes, "manifest.json");
        var readBudget = new ArchiveReadBudget(_securityOptions.MaxUncompressedArchiveBytes);

        var manifestBytes = await ReadEntryToBytesAsync(manifestEntry, _securityOptions.MaxManifestBytes,
            readBudget, cancellationToken);

        BookFullBackupManifest manifest;
        await using (var manifestStream = new MemoryStream(manifestBytes, false))
        {
            manifest = await JsonSerializer.DeserializeAsync<BookFullBackupManifest>(manifestStream,
                           new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken)
                       ?? throw new ValidationException("Full backup manifest is invalid.");
        }

        if (manifest.Version != FullBackupVersion)
        {
            throw new ValidationException($"Unsupported full backup version: {manifest.Version}.");
        }

        if (manifest.Books == null)
        {
            throw new ValidationException("Full backup manifest is missing its books list.");
        }

        if (manifest.Books.Count > _securityOptions.MaxManifestBooks)
        {
            throw new ValidationException("Full backup manifest contains too many books.");
        }

        var manifestByKey = new Dictionary<BookFullBackupKey, BookFullBackupManifestItem>();
        var referencedCoverPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allowedEntryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "books.csv", "manifest.json" };
        foreach (var item in manifest.Books)
        {
            if (string.IsNullOrWhiteSpace(item.PrimaryTitle) || string.IsNullOrWhiteSpace(item.ContentType) ||
                item.PrimaryTitle.Length > 1_000 || item.ContentType.Length > 100)
            {
                throw new ValidationException("Full backup manifest contains a book without a title or content type.");
            }

            var key = new BookFullBackupKey(
                MappingExtensions.NormalizeName(item.PrimaryTitle),
                MappingExtensions.NormalizeName(item.ContentType));
            if (!manifestByKey.TryAdd(key, item))
            {
                throw new ValidationException(
                    $"Full backup manifest contains duplicate book '{item.PrimaryTitle}' ({item.ContentType}).");
            }

            ValidateManifestCoverReference(item.OriginalCoverPath, item.OriginalCoverMimeType, entries,
                referencedCoverPaths, allowedEntryPaths);
            ValidateManifestCoverReference(item.ThumbnailCoverPath, item.ThumbnailCoverMimeType, entries,
                referencedCoverPaths, allowedEntryPaths);
        }

        var unexpectedEntry = entries.Keys.FirstOrDefault(path => !allowedEntryPaths.Contains(path));
        if (unexpectedEntry != null)
        {
            throw new ValidationException($"Full backup contains unexpected entry '{unexpectedEntry}'.");
        }

        var csvBytes = await ReadEntryToBytesAsync(csvEntry, _securityOptions.MaxCsvBytes, readBudget,
            cancellationToken);
        await using var csvStream = new MemoryStream(csvBytes, false);
        var session = await CreateCsvSessionAsync(csvStream, Path.GetFileNameWithoutExtension(fileName) + ".csv",
            _user.RequiredId, cancellationToken);
        session.IsFullImport = true;
        session.TempDirectory = _sessionStore.CreateSessionDirectory(session.SessionId);
        try
        {
            var processedCoverPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in session.Rows)
            {
                if (string.IsNullOrWhiteSpace(row.PrimaryTitle) || string.IsNullOrWhiteSpace(row.ContentType))
                {
                    continue;
                }

                var key = new BookFullBackupKey(
                    MappingExtensions.NormalizeName(row.PrimaryTitle),
                    MappingExtensions.NormalizeName(row.ContentType));
                if (!manifestByKey.TryGetValue(key, out var item) || string.IsNullOrWhiteSpace(item.OriginalCoverPath))
                {
                    continue;
                }

                var coverEntry = entries[item.OriginalCoverPath];
                var stagedPath = Path.Combine(session.TempDirectory, $"{row.RowId:N}.cover");
                var stagedCover = await CopyEntryToFileAsync(coverEntry, stagedPath, _securityOptions.MaxCoverBytes,
                    readBudget, cancellationToken);
                if (!string.IsNullOrWhiteSpace(item.OriginalCoverMimeType) &&
                    !string.Equals(item.OriginalCoverMimeType, stagedCover.MimeType,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new ValidationException(
                        $"Cover '{item.OriginalCoverPath}' does not match its declared media type.");
                }

                session.StagedBytes += stagedCover.SizeBytes;
                session.CoversByRowId[row.RowId] = new BookFullBackupCover(
                    stagedPath,
                    Path.GetFileName(coverEntry.FullName),
                    stagedCover.MimeType,
                    stagedCover.SizeBytes);
                processedCoverPaths.Add(item.OriginalCoverPath);
            }

            foreach (var coverPath in referencedCoverPaths.Where(path => !processedCoverPaths.Contains(path)))
            {
                await DrainEntryAsync(entries[coverPath], _securityOptions.MaxCoverBytes, readBudget,
                    cancellationToken);
            }

            var dto = BookCsvImportSessionMapper.ToDto(session);
            sessionReservation.Commit(session);
            return dto;
        }
        catch
        {
            _sessionStore.DeleteUncommittedDirectory(session.TempDirectory);
            throw;
        }
    }

    private async Task<BookImportFinalizeResultDto> FinalizeSessionAsync(ImportSession session,
        CancellationToken cancellationToken)
    {
        await RevalidateSessionAsync(session, cancellationToken);
        if (session.Rows.Any(row => row.Errors.Count > 0))
        {
            throw new ValidationException("Fix invalid import rows before finalizing.");
        }

        var ownerId = session.OwnerId;
        var typeMap = await _context.ContentTypes.AsNoTracking()
            .ToDictionaryAsync(t => MappingExtensions.NormalizeName(t.Name), t => t, cancellationToken);
        var statusMap = await _context.Statuses.AsNoTracking()
            .ToDictionaryAsync(s => MappingExtensions.NormalizeName(s.Name), s => s, cancellationToken);
        var authorMap = await _context.Authors.Include(a => a.Names)
            .ToDictionaryAsync(a => a.NormalizedPrimaryName, cancellationToken);
        var genreMap = await _context.Genres
            .ToDictionaryAsync(genre => genre.NormalizedName, cancellationToken);
        var tagMap = await _context.Tags.Where(t => t.OwnerId == ownerId)
            .ToDictionaryAsync(t => t.NormalizedName, cancellationToken);
        var existingKeys = await _context.Books.AsNoTracking()
            .Where(b => b.OwnerId == ownerId)
            .Select(b => new BookImportKey(b.NormalizedPrimaryTitle, b.ContentTypeId))
            .ToHashSetAsync(cancellationToken);

        var imported = 0;
        var skipped = 0;
        var errors = new List<string>();
        var createdBooks = new List<(Book Book, Guid RowId)>();
        var importedBooks = new List<BookImportFinalizedBookDto>();

        foreach (var row in session.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedTitle = MappingExtensions.NormalizeName(row.PrimaryTitle!);
            var contentTypeId = typeMap[MappingExtensions.NormalizeName(row.ContentType!)].Id;
            var importKey = new BookImportKey(normalizedTitle, contentTypeId);
            if (existingKeys.Contains(importKey))
            {
                skipped++;
                errors.Add(
                    $"Line {row.LineNumber}: title '{row.PrimaryTitle}' already exists for content type '{row.ContentType}'.");
                continue;
            }

            var author = ResolveAuthor(row.AuthorName, authorMap);
            var book = new Book
            {
                PrimaryTitle = row.PrimaryTitle!,
                NormalizedPrimaryTitle = normalizedTitle,
                Description = row.Description,
                AuthorId = author?.Id,
                Author = author,
                ContentTypeId = contentTypeId,
                ContentType = default!,
                StatusId = statusMap[MappingExtensions.NormalizeName(row.Status!)].Id,
                Status = default!,
                OwnerId = ownerId,
                TotalChapters = BookCsvImportRowMapper.ParseDecimal(row.TotalChapters),
                CurrentChapterNumber = BookCsvImportRowMapper.ParseDecimal(row.CurrentChapterNumber),
                CurrentChapterLabel = row.CurrentChapterLabel,
                Rating = BookCsvImportRowMapper.ParseInt(row.Rating),
                Priority = BookCsvImportRowMapper.ParseInt(row.Priority),
                Notes = row.Notes,
                RawImportedLine = row.RawImportedLine,
                Cover = new BookCover()
            };

            book.Titles.Add(book.PrimaryTitle.ToPrimaryTitle());

            foreach (var title in BookCsvImportRowMapper.DeserializeCollection<BookTitleInput>(row.AlternativeTitles))
            {
                var value = MappingExtensions.CollapseWhitespace(title.Title);
                if (value.Length == 0 || string.Equals(value, book.PrimaryTitle, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                book.Titles.Add(new BookTitle
                {
                    Title = value,
                    NormalizedTitle = MappingExtensions.NormalizeName(value),
                    Language = title.Language,
                    Source = title.Source,
                    IsPrimary = false
                });
            }

            foreach (var link in BookCsvImportRowMapper.DeserializeCollection<BookLinkInput>(row.Links))
            {
                book.Links.Add(new BookLink
                {
                    Url = link.Url,
                    Label = link.Label,
                    SourceType = link.SourceType,
                    IsPrimary = link.IsPrimary,
                    LastReadHere = link.LastReadHere
                });
            }

            foreach (var tagName in BookCsvImportRowMapper.SplitTags(row.Tags))
            {
                var normalizedTag = MappingExtensions.NormalizeName(tagName);
                if (!tagMap.TryGetValue(normalizedTag, out var tag))
                {
                    tag = new Tag { OwnerId = ownerId, Name = tagName, NormalizedName = normalizedTag };
                    tagMap[normalizedTag] = tag;
                    _context.Tags.Add(tag);
                }

                book.BookTags.Add(new BookTag { Book = book, Tag = tag });
            }

            foreach (var genreName in BookCsvImportRowMapper.SplitTags(row.Genres))
            {
                book.BookGenres.Add(new BookGenre
                {
                    Book = book, Genre = genreMap[MappingExtensions.NormalizeName(genreName)]
                });
            }

            if (row.ProgressHistory != null)
            {
                foreach (var history in
                         BookCsvImportRowMapper.DeserializeCollection<BookProgressHistoryCsvItem>(row.ProgressHistory))
                {
                    book.ProgressHistory.Add(new BookProgressHistory
                    {
                        ChangedAt = history.ChangedAt,
                        ChapterNumber = history.ChapterNumber,
                        ChapterLabel = history.ChapterLabel,
                        Comment = history.Comment
                    });
                }
            }
            else if (book.CurrentChapterNumber.HasValue || !string.IsNullOrWhiteSpace(book.CurrentChapterLabel))
            {
                book.ProgressHistory.Add(new BookProgressHistory
                {
                    ChapterNumber = book.CurrentChapterNumber,
                    ChapterLabel = book.CurrentChapterLabel,
                    Comment = "Imported from CSV"
                });
            }

            _context.Books.Add(book);
            existingKeys.Add(importKey);
            createdBooks.Add((book, row.RowId));
            importedBooks.Add(new BookImportFinalizedBookDto
            {
                PrimaryTitle = book.PrimaryTitle,
                ContentType = row.ContentType!,
                Status = row.Status!,
                CurrentChapterNumber = book.CurrentChapterNumber,
                CurrentChapterLabel = book.CurrentChapterLabel,
                TotalChapters = book.TotalChapters
            });
            imported++;
        }

        if (imported > 0)
        {
            var restoredCoverBookIds = new HashSet<Guid>();
            var persistedCoverFiles = new List<BookCoverStoredFiles>();
            if (session.IsFullImport)
            {
                foreach (var (book, rowId) in createdBooks)
                {
                    if (!session.CoversByRowId.TryGetValue(rowId, out var backupCover))
                    {
                        continue;
                    }

                    try
                    {
                        await using var coverStream = new FileStream(
                            backupCover.StagedPath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read,
                            64 * 1024,
                            FileOptions.Asynchronous | FileOptions.SequentialScan);
                        var stored = await _bookCoverStorage.SaveAsync(
                            ownerId,
                            book.Id,
                            coverStream,
                            backupCover.FileName,
                            backupCover.MimeType,
                            cancellationToken);
                        ApplyRestoredCover(book.Cover!, stored);
                        persistedCoverFiles.Add(stored);
                        restoredCoverBookIds.Add(book.Id);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        errors.Add(
                            $"Could not restore the cover for '{book.PrimaryTitle}'. Automatic cover search was queued instead.");
                    }
                }
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                foreach (var stored in persistedCoverFiles)
                {
                    await _bookCoverStorage.DeleteIfExistsAsync(stored.Original.StoragePath, CancellationToken.None);
                    await _bookCoverStorage.DeleteIfExistsAsync(stored.Thumbnail.StoragePath,
                        CancellationToken.None);
                }

                throw;
            }

            foreach (var (book, _) in createdBooks)
            {
                if (!restoredCoverBookIds.Contains(book.Id))
                {
                    await _bookCoverQueue.QueueAsync(book.Id, cancellationToken);
                }
            }

            await _cacheInvalidator.InvalidateBooksAsync(ownerId, cancellationToken);
        }

        _sessionStore.Remove(session.SessionId);
        return new BookImportFinalizeResultDto
        {
            ImportedCount = imported, SkippedCount = skipped, ImportedBooks = importedBooks, Errors = errors
        };
    }

    private async Task RevalidateSessionAsync(ImportSession session, CancellationToken cancellationToken)
    {
        var types = await _context.ContentTypes.AsNoTracking()
            .Select(t => new { t.Id, t.Name })
            .OrderBy(t => t.Id.ToString())
            .ToListAsync(cancellationToken);
        var typeNames = types.Select(t => MappingExtensions.NormalizeName(t.Name)).ToList();
        var typeIdsByName = types.ToDictionary(t => MappingExtensions.NormalizeName(t.Name), t => t.Id);
        var statusNames = await _context.Statuses.AsNoTracking()
            .OrderBy(s => s.Id.ToString())
            .Select(s => s.Name)
            .ToListAsync(cancellationToken);
        var genreNames = await _context.Genres.AsNoTracking()
            .Select(genre => genre.NormalizedName)
            .ToHashSetAsync(cancellationToken);
        session.AvailableContentTypes = types
            .Select(type => type.Name)
            .ToArray();
        session.AvailableStatuses = statusNames
            .ToArray();
        var validTypes = typeNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var validStatuses = statusNames.Select(MappingExtensions.NormalizeName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingKeys = await _context.Books.AsNoTracking()
            .Where(b => b.OwnerId == session.OwnerId)
            .Select(b => new BookImportKey(b.NormalizedPrimaryTitle, b.ContentTypeId))
            .ToHashSetAsync(cancellationToken);

        foreach (var row in session.Rows)
        {
            row.Errors.Clear();
            row.FieldErrors.Clear();

            BookCsvImportRowMapper.NormalizeRow(row);

            if (string.IsNullOrWhiteSpace(row.PrimaryTitle))
            {
                BookCsvImportRowMapper.AddFieldError(row, BookCsvColumns.PrimaryTitle,
                    "Primary title is required.");
            }

            var normalizedContentType = string.IsNullOrWhiteSpace(row.ContentType)
                ? null
                : MappingExtensions.NormalizeName(row.ContentType);
            var normalizedStatus = string.IsNullOrWhiteSpace(row.Status)
                ? null
                : MappingExtensions.NormalizeName(row.Status);

            if (normalizedContentType == null || !validTypes.Contains(normalizedContentType))
            {
                BookCsvImportRowMapper.AddFieldError(row, BookCsvColumns.ContentType,
                    $"Content type is required and must exist. Allowed values: {string.Join(", ", types.Select(type => type.Name))}.");
            }

            if (normalizedStatus == null || !validStatuses.Contains(normalizedStatus))
            {
                BookCsvImportRowMapper.AddFieldError(row, BookCsvColumns.Status,
                    $"Status is required and must exist. Allowed values: {string.Join(", ", statusNames)}.");
            }

            var totalChapters =
                BookCsvImportRowMapper.ParseDecimal(row, row.TotalChapters, BookCsvColumns.TotalChapters,
                    nameof(row.TotalChapters));
            var currentChapterNumber = BookCsvImportRowMapper.ParseDecimal(row, row.CurrentChapterNumber,
                BookCsvColumns.CurrentChapterNumber, nameof(row.CurrentChapterNumber));
            _ = BookCsvImportRowMapper.ParseInt(row, row.Rating, BookCsvColumns.Rating, nameof(row.Rating), 1, 10);
            _ = BookCsvImportRowMapper.ParseInt(row, row.Priority, BookCsvColumns.Priority, nameof(row.Priority), 1, 5);

            foreach (var genreName in BookCsvImportRowMapper.SplitTags(row.Genres))
            {
                if (!genreNames.Contains(MappingExtensions.NormalizeName(genreName)))
                {
                    BookCsvImportRowMapper.AddFieldError(row, BookCsvColumns.Genres,
                        $"Genre '{genreName}' does not exist.");
                }
            }

            ValidateJsonArray(row, row.AlternativeTitles, BookCsvColumns.AlternativeTitles, "Alternative titles");
            ValidateJsonArray(row, row.Links, BookCsvColumns.Links, "Links");
            ValidateJsonArray(row, row.ProgressHistory, BookCsvColumns.ProgressHistory, "Progress history");

            if (currentChapterNumber.HasValue && currentChapterNumber < 0)
            {
                BookCsvImportRowMapper.AddFieldError(row, BookCsvColumns.CurrentChapterNumber,
                    "Current chapter number cannot be negative.");
            }

            if (totalChapters.HasValue && totalChapters < 0)
            {
                BookCsvImportRowMapper.AddFieldError(row, BookCsvColumns.TotalChapters,
                    "Total chapters cannot be negative.");
            }

            if (totalChapters.HasValue && currentChapterNumber.HasValue && currentChapterNumber > totalChapters)
            {
                BookCsvImportRowMapper.AddFieldError(row, BookCsvColumns.CurrentChapterNumber,
                    "Current chapter number cannot exceed total chapters.");
            }

            if (!string.IsNullOrWhiteSpace(row.PrimaryTitle) &&
                normalizedContentType != null &&
                validTypes.Contains(normalizedContentType) &&
                existingKeys.Contains(new BookImportKey(
                    MappingExtensions.NormalizeName(row.PrimaryTitle),
                    typeIdsByName[normalizedContentType])))
            {
                BookCsvImportRowMapper.AddFieldError(row, BookCsvColumns.PrimaryTitle,
                    "A book with this title and content type already exists in your library.");
            }
        }

        var duplicateGroups = session.Rows
            .Where(row => !string.IsNullOrWhiteSpace(row.PrimaryTitle) && !string.IsNullOrWhiteSpace(row.ContentType) &&
                          validTypes.Contains(MappingExtensions.NormalizeName(row.ContentType)))
            .GroupBy(row => new BookImportKey(
                MappingExtensions.NormalizeName(row.PrimaryTitle!),
                typeIdsByName[MappingExtensions.NormalizeName(row.ContentType!)]))
            .Where(group => group.Count() > 1);

        foreach (var duplicateGroup in duplicateGroups)
        {
            foreach (var row in duplicateGroup)
            {
                BookCsvImportRowMapper.AddFieldError(row, BookCsvColumns.PrimaryTitle,
                    "Duplicate title with the same content type inside this import session.");
            }
        }
    }

    private ImportSession GetOwnedSession(Guid sessionId)
    {
        return _sessionStore.GetOwned(sessionId, _user.RequiredId);
    }

    private void EnsureSessionIsCurrent(ImportSession session)
    {
        var current = _sessionStore.GetOwned(session.SessionId, _user.RequiredId);
        if (!ReferenceEquals(current, session))
        {
            throw new ValidationException("Import session not found or expired.");
        }
    }

    private Author? ResolveAuthor(string? authorName, Dictionary<string, Author> authorMap)
    {
        if (string.IsNullOrWhiteSpace(authorName))
        {
            return null;
        }

        var normalized = MappingExtensions.NormalizeName(authorName);
        if (authorMap.TryGetValue(normalized, out var author))
        {
            return author;
        }

        author = new Author { PrimaryName = authorName, NormalizedPrimaryName = normalized };
        author.Names.Add(new AuthorName
        {
            Name = authorName, NormalizedName = normalized, IsPrimary = true, Source = "CSV import"
        });
        authorMap[normalized] = author;
        _context.Authors.Add(author);
        return author;
    }

    private static int NormalizeLineNumber(int? lineNumber)
    {
        return lineNumber.HasValue && lineNumber.Value > 0 ? lineNumber.Value : 1;
    }

    private void EnsureImportRowCapacity(ImportSession session)
    {
        if (session.Rows.Count >= _securityOptions.MaxCsvRows)
        {
            throw new ValidationException($"CSV cannot contain more than {_securityOptions.MaxCsvRows} rows.");
        }
    }

    private Dictionary<string, ZipArchiveEntry> ValidateArchiveMetadata(ZipArchive archive)
    {
        if (archive.Entries.Count == 0 || archive.Entries.Count > _securityOptions.MaxArchiveEntries)
        {
            throw new ValidationException("Full backup contains an invalid number of entries.");
        }

        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        long declaredTotal = 0;
        foreach (var entry in archive.Entries)
        {
            if (!IsSafeArchiveEntryName(entry.FullName) || !entries.TryAdd(entry.FullName, entry))
            {
                throw new ValidationException($"Full backup contains an unsafe or duplicate entry '{entry.FullName}'.");
            }

            if (entry.Length < 0 || entry.CompressedLength < 0 ||
                entry.Length > _securityOptions.MaxUncompressedArchiveBytes - declaredTotal)
            {
                throw new ValidationException("Full backup archive is too large.");
            }

            declaredTotal += entry.Length;
            if (entry.Length > 0)
            {
                if (entry.CompressedLength == 0 ||
                    entry.Length / (double)entry.CompressedLength > _securityOptions.MaxCompressionRatio)
                {
                    throw new ValidationException(
                        $"Full backup entry '{entry.FullName}' has a suspicious compression ratio.");
                }
            }
        }

        return entries;
    }

    private static ZipArchiveEntry GetRequiredEntry(IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        string entryName)
    {
        return entries.TryGetValue(entryName, out var entry)
            ? entry
            : throw new ValidationException($"Full backup is missing {entryName}.");
    }

    private void ValidateManifestCoverReference(
        string? path,
        string? mimeType,
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        ISet<string> referencedCoverPaths,
        ISet<string> allowedEntryPaths)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!IsSafeCoverEntryName(path) || !referencedCoverPaths.Add(path))
        {
            throw new ValidationException($"Full backup manifest contains an unsafe or duplicate cover path '{path}'.");
        }

        if (!entries.TryGetValue(path, out var entry))
        {
            throw new ValidationException($"Full backup is missing cover '{path}'.");
        }

        ValidateEntrySize(entry, _securityOptions.MaxCoverBytes, path);
        if (!string.IsNullOrWhiteSpace(mimeType) &&
            mimeType is not ("image/jpeg" or "image/png" or "image/webp"))
        {
            throw new ValidationException($"Cover '{path}' has an unsupported media type.");
        }

        allowedEntryPaths.Add(path);
    }

    private static void ValidateEntrySize(ZipArchiveEntry entry, long maximumBytes, string entryName)
    {
        if (entry.Length <= 0 || entry.Length > maximumBytes)
        {
            throw new ValidationException($"Full backup entry '{entryName}' has an invalid size.");
        }
    }

    private static bool IsSafeArchiveEntryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 240 || name.Contains('\\') || name.Contains(':') ||
            name.StartsWith('/') || name.EndsWith('/'))
        {
            return false;
        }

        var segments = name.Split('/');
        return segments.All(segment => segment.Length > 0 && segment is not "." and not "..");
    }

    private static bool IsSafeCoverEntryName(string name)
    {
        if (!IsSafeArchiveEntryName(name) || !name.StartsWith("covers/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Path.GetExtension(name).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp";
    }

    private static async Task<byte[]> ReadStreamToBytesAsync(Stream source, long maximumBytes, string sourceName,
        CancellationToken cancellationToken)
    {
        using var destination = new MemoryStream((int)Math.Min(maximumBytes, 1024 * 1024));
        var buffer = new byte[64 * 1024];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maximumBytes)
            {
                throw new ValidationException($"{sourceName} is too large.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        if (total == 0)
        {
            throw new ValidationException($"{sourceName} is empty.");
        }

        return destination.ToArray();
    }

    private static async Task<byte[]> ReadEntryToBytesAsync(ZipArchiveEntry entry, long maximumBytes,
        ArchiveReadBudget budget, CancellationToken cancellationToken)
    {
        await using var source = entry.Open();
        using var destination = new MemoryStream((int)Math.Min(entry.Length, maximumBytes));
        await CopyBoundedAsync(source, destination, maximumBytes, entry.FullName, budget, cancellationToken);
        return destination.ToArray();
    }

    private static async Task<StagedCoverResult> CopyEntryToFileAsync(ZipArchiveEntry entry, string destinationPath,
        long maximumBytes, ArchiveReadBudget budget, CancellationToken cancellationToken)
    {
        try
        {
            await using var source = entry.Open();
            await using (var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write,
                             FileShare.None, 64 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await CopyBoundedAsync(source, destination, maximumBytes, entry.FullName, budget, cancellationToken);
            }

            var mimeType = await DetectCoverMimeTypeAsync(destinationPath, cancellationToken)
                           ?? throw new ValidationException($"Cover '{entry.FullName}' is not a supported image.");
            return new StagedCoverResult(new FileInfo(destinationPath).Length, mimeType);
        }
        catch
        {
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            throw;
        }
    }

    private static async Task DrainEntryAsync(ZipArchiveEntry entry, long maximumBytes, ArchiveReadBudget budget,
        CancellationToken cancellationToken)
    {
        await using var source = entry.Open();
        await CopyBoundedAsync(source, Stream.Null, maximumBytes, entry.FullName, budget, cancellationToken);
    }

    private static async Task CopyBoundedAsync(Stream source, Stream destination, long maximumBytes, string entryName,
        ArchiveReadBudget budget, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        long entryBytes = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            entryBytes += read;
            if (entryBytes > maximumBytes)
            {
                throw new ValidationException($"Full backup entry '{entryName}' is too large after decompression.");
            }

            budget.Add(read);
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        if (entryBytes == 0)
        {
            throw new ValidationException($"Full backup entry '{entryName}' is empty.");
        }
    }

    private static async Task<string?> DetectCoverMimeTypeAsync(string path, CancellationToken cancellationToken)
    {
        var header = new byte[12];
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var read = await stream.ReadAsync(header, cancellationToken);
        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (read >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            return "image/png";
        }

        if (read >= 12 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
        {
            return "image/webp";
        }

        return null;
    }

    private static void ApplyRestoredCover(BookCover cover, BookCoverStoredFiles stored)
    {
        cover.Status = BookCoverStatus.Uploaded;
        cover.Source = BookCoverSource.ManualUpload;
        cover.StoragePath = stored.Original.StoragePath;
        cover.ThumbnailStoragePath = stored.Thumbnail.StoragePath;
        cover.OriginalImageUrl = null;
        cover.MimeType = stored.Original.MimeType;
        cover.ThumbnailMimeType = stored.Thumbnail.MimeType;
        cover.SizeBytes = stored.Original.SizeBytes;
        cover.ThumbnailSizeBytes = stored.Thumbnail.SizeBytes;
        cover.Width = stored.Original.Width;
        cover.Height = stored.Original.Height;
        cover.ThumbnailWidth = stored.Thumbnail.Width;
        cover.ThumbnailHeight = stored.Thumbnail.Height;
        cover.FailureReason = null;
        cover.LastAttemptAt = DateTimeOffset.UtcNow;
    }

    private static void ValidateJsonArray(ImportRow row, string? value, string fieldKey, string fieldName)
    {
        if (!BookCsvImportRowMapper.IsJsonArray(value))
        {
            BookCsvImportRowMapper.AddFieldError(row, fieldKey, $"{fieldName} must be a JSON array.");
        }
    }

    private static string DetectDelimiter(string csv)
    {
        using var reader = new StringReader(csv);
        string? header;
        do
        {
            header = reader.ReadLine();
        } while (header != null && string.IsNullOrWhiteSpace(header));

        if (string.IsNullOrEmpty(header))
        {
            return ",";
        }

        return CountUnquotedDelimiters(header, ';') > CountUnquotedDelimiters(header, ',') ? ";" : ",";
    }

    private static int CountUnquotedDelimiters(string value, char delimiter)
    {
        var count = 0;
        var insideQuotes = false;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '"')
            {
                if (insideQuotes && index + 1 < value.Length && value[index + 1] == '"')
                {
                    index++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }
            }
            else if (!insideQuotes && value[index] == delimiter)
            {
                count++;
            }
        }

        return count;
    }

    private sealed class ArchiveReadBudget
    {
        private readonly long _maximumBytes;
        private long _readBytes;

        public ArchiveReadBudget(long maximumBytes)
        {
            _maximumBytes = maximumBytes;
        }

        public void Add(int bytes)
        {
            if (bytes > _maximumBytes - _readBytes)
            {
                throw new ValidationException("Full backup exceeds the decompressed data limit.");
            }

            _readBytes += bytes;
        }
    }

    private sealed record StagedCoverResult(long SizeBytes, string MimeType);
}
