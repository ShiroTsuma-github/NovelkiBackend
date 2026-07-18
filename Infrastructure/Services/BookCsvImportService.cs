namespace Infrastructure.Services;

using System.Collections.Concurrent;
using Application.Common.DTOs.Book;
using FluentValidation;
using Microsoft.VisualBasic.FileIO;

public sealed class BookCsvImportService : IBookCsvImportService
{
    private const string ImportRowNotFoundMessage = "Import row not found.";

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

    private static readonly ConcurrentDictionary<Guid, ImportSession> Sessions = new();
    private readonly IBookCoverQueue _bookCoverQueue;
    private readonly IBookListCacheInvalidator _cacheInvalidator;

    private readonly ApplicationDbContext _context;
    private readonly IUser _user;

    public BookCsvImportService(
        ApplicationDbContext context,
        IBookCoverQueue bookCoverQueue,
        IBookListCacheInvalidator cacheInvalidator,
        IUser user)
    {
        _context = context;
        _bookCoverQueue = bookCoverQueue;
        _cacheInvalidator = cacheInvalidator;
        _user = user;
    }

    public string CreateTemplate()
    {
        return string.Join(',', TemplateColumns) + Environment.NewLine;
    }

    public async Task<BookImportSessionDto> CreateSessionAsync(Stream csvStream, string fileName,
        CancellationToken cancellationToken)
    {
        var ownerId = _user.RequiredId;
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

            var row = headerMap.ToDictionary(
                pair => pair.Key,
                pair => pair.Value < fields.Length ? fields[pair.Value] : string.Empty,
                StringComparer.OrdinalIgnoreCase);

            session.Rows.Add(BookCsvImportRowMapper.CreateRow(row, NormalizeLineNumber((int?)parser.LineNumber)));
        }

        await RevalidateSessionAsync(session, cancellationToken);
        Sessions[session.SessionId] = session;
        return BookCsvImportSessionMapper.ToDto(session);
    }

    public async Task<BookImportSessionDto> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = GetOwnedSession(sessionId);
        await RevalidateSessionAsync(session, cancellationToken);
        return BookCsvImportSessionMapper.ToDto(session);
    }

    public async Task<BookImportSessionDto> UpdateRowAsync(Guid sessionId, Guid rowId,
        UpdateBookImportRowRequest request, CancellationToken cancellationToken)
    {
        var session = GetOwnedSession(sessionId);
        var row = session.Rows.FirstOrDefault(item => item.RowId == rowId)
                  ?? throw new ValidationException(ImportRowNotFoundMessage);

        BookCsvImportRowMapper.ApplyRequest(row, request);

        await RevalidateSessionAsync(session, cancellationToken);
        return BookCsvImportSessionMapper.ToDto(session);
    }

    public async Task<BookImportSessionDto> DeleteRowAsync(Guid sessionId, Guid rowId,
        CancellationToken cancellationToken)
    {
        var session = GetOwnedSession(sessionId);
        var removed = session.Rows.RemoveAll(item => item.RowId == rowId);
        if (removed == 0)
        {
            throw new ValidationException(ImportRowNotFoundMessage);
        }

        await RevalidateSessionAsync(session, cancellationToken);
        return BookCsvImportSessionMapper.ToDto(session);
    }

    public async Task<BookImportSessionDto> DeleteInvalidRowsAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = GetOwnedSession(sessionId);
        session.Rows.RemoveAll(item => item.Errors.Count > 0);
        await RevalidateSessionAsync(session, cancellationToken);
        return BookCsvImportSessionMapper.ToDto(session);
    }

    public async Task<BookImportFinalizeResultDto> FinalizeAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = GetOwnedSession(sessionId);
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
        var createdBookIds = new List<Guid>();
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
            createdBookIds.Add(book.Id);
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
            await _context.SaveChangesAsync(cancellationToken);
            foreach (var bookId in createdBookIds)
            {
                await _bookCoverQueue.QueueAsync(bookId, cancellationToken);
            }

            await _cacheInvalidator.InvalidateBooksAsync(ownerId, cancellationToken);
        }

        Sessions.TryRemove(sessionId, out _);
        return new BookImportFinalizeResultDto
        {
            ImportedCount = imported, SkippedCount = skipped, ImportedBooks = importedBooks, Errors = errors
        };
    }

    public Task CancelAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = GetOwnedSession(sessionId);
        Sessions.TryRemove(session.SessionId, out _);
        return Task.CompletedTask;
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
        if (!Sessions.TryGetValue(sessionId, out var session) || session.OwnerId != _user.RequiredId)
        {
            throw new ValidationException("Import session not found or expired.");
        }

        return session;
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
}
