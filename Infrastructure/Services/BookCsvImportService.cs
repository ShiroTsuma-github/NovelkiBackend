namespace Infrastructure.Services;

using Application.Common;
using Application.Common.DTOs.Book;
using Application.Common.Interfaces;
using Domain.Associations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using System.Collections.Concurrent;

public sealed class BookCsvImportService : IBookCsvImportService
{
    private static readonly string[] RequiredColumns = ["primaryTitle", "contentType", "status"];

    private static readonly string[] TemplateColumns =
    [
        "primaryTitle",
        "authorName",
        "contentType",
        "status",
        "tags",
        "totalChapters",
        "currentChapterNumber",
        "currentChapterLabel",
        "rating",
        "priority",
        "description",
        "notes",
        "rawImportedLine"
    ];

    private static readonly ConcurrentDictionary<Guid, ImportSession> Sessions = new();

    private readonly ApplicationDbContext _context;
    private readonly IBookCoverQueue _bookCoverQueue;
    private readonly IBookListCacheInvalidator _cacheInvalidator;
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
        Guid ownerId = _user.RequiredId;
        using var reader = new StreamReader(csvStream, leaveOpen: true);
        using var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        if (parser.EndOfData)
        {
            throw new ValidationException("CSV file is empty.");
        }

        string[]? headers = parser.ReadFields();
        if (headers == null)
        {
            throw new ValidationException("CSV header could not be read.");
        }

        var headerMap = headers
            .Select((name, index) => new { Name = name.Trim(), Index = index })
            .ToDictionary(item => item.Name, item => item.Index, StringComparer.OrdinalIgnoreCase);

        string[] missingColumns = RequiredColumns.Where(column => !headerMap.ContainsKey(column)).ToArray();
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
        ImportSession session = GetOwnedSession(sessionId);
        await RevalidateSessionAsync(session, cancellationToken);
        return BookCsvImportSessionMapper.ToDto(session);
    }

    public async Task<BookImportSessionDto> UpdateRowAsync(Guid sessionId, Guid rowId,
        UpdateBookImportRowRequest request, CancellationToken cancellationToken)
    {
        ImportSession session = GetOwnedSession(sessionId);
        ImportRow row = session.Rows.FirstOrDefault(item => item.RowId == rowId)
                        ?? throw new ValidationException("Import row not found.");

        BookCsvImportRowMapper.ApplyRequest(row, request);

        await RevalidateSessionAsync(session, cancellationToken);
        return BookCsvImportSessionMapper.ToDto(session);
    }

    public async Task<BookImportSessionDto> DeleteRowAsync(Guid sessionId, Guid rowId,
        CancellationToken cancellationToken)
    {
        ImportSession session = GetOwnedSession(sessionId);
        int removed = session.Rows.RemoveAll(item => item.RowId == rowId);
        if (removed == 0)
        {
            throw new ValidationException("Import row not found.");
        }

        await RevalidateSessionAsync(session, cancellationToken);
        return BookCsvImportSessionMapper.ToDto(session);
    }

    public async Task<BookImportSessionDto> DeleteInvalidRowsAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        ImportSession session = GetOwnedSession(sessionId);
        session.Rows.RemoveAll(item => item.Errors.Count > 0);
        await RevalidateSessionAsync(session, cancellationToken);
        return BookCsvImportSessionMapper.ToDto(session);
    }

    public async Task<BookImportFinalizeResultDto> FinalizeAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        ImportSession session = GetOwnedSession(sessionId);
        await RevalidateSessionAsync(session, cancellationToken);
        if (session.Rows.Any(row => row.Errors.Count > 0))
        {
            throw new ValidationException("Fix invalid import rows before finalizing.");
        }

        Guid ownerId = session.OwnerId;
        Dictionary<string, ContentType> typeMap = await _context.ContentTypes.AsNoTracking()
            .ToDictionaryAsync(t => MappingExtensions.NormalizeName(t.Name), t => t, cancellationToken);
        Dictionary<string, Status> statusMap = await _context.Statuses.AsNoTracking()
            .ToDictionaryAsync(s => MappingExtensions.NormalizeName(s.Name), s => s, cancellationToken);
        Dictionary<string, Author> authorMap = await _context.Authors.Include(a => a.Names)
            .ToDictionaryAsync(a => a.NormalizedPrimaryName, cancellationToken);
        Dictionary<string, Tag> tagMap = await _context.Tags.Where(t => t.OwnerId == ownerId)
            .ToDictionaryAsync(t => t.NormalizedName, cancellationToken);
        HashSet<BookImportKey> existingKeys = await _context.Books.AsNoTracking()
            .Where(b => b.OwnerId == ownerId)
            .Select(b => new BookImportKey(b.NormalizedPrimaryTitle, b.ContentTypeId))
            .ToHashSetAsync(cancellationToken);

        int imported = 0;
        int skipped = 0;
        var errors = new List<string>();
        var createdBookIds = new List<Guid>();
        var importedBooks = new List<BookImportFinalizedBookDto>();

        foreach (ImportRow row in session.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string normalizedTitle = MappingExtensions.NormalizeName(row.PrimaryTitle!);
            Guid contentTypeId = typeMap[MappingExtensions.NormalizeName(row.ContentType!)].Id;
            var importKey = new BookImportKey(normalizedTitle, contentTypeId);
            if (existingKeys.Contains(importKey))
            {
                skipped++;
                errors.Add(
                    $"Line {row.LineNumber}: title '{row.PrimaryTitle}' already exists for content type '{row.ContentType}'.");
                continue;
            }

            Author? author = ResolveAuthor(row.AuthorName, authorMap);
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

            foreach (string tagName in BookCsvImportRowMapper.SplitTags(row.Tags))
            {
                string normalizedTag = MappingExtensions.NormalizeName(tagName);
                if (!tagMap.TryGetValue(normalizedTag, out Tag? tag))
                {
                    tag = new Tag { OwnerId = ownerId, Name = tagName, NormalizedName = normalizedTag };
                    tagMap[normalizedTag] = tag;
                    _context.Tags.Add(tag);
                }

                book.BookTags.Add(new BookTag { Book = book, Tag = tag });
            }

            if (book.CurrentChapterNumber.HasValue || !string.IsNullOrWhiteSpace(book.CurrentChapterLabel))
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
            foreach (Guid bookId in createdBookIds)
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
        ImportSession session = GetOwnedSession(sessionId);
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
        List<string> statusNames = await _context.Statuses.AsNoTracking()
            .OrderBy(s => s.Id.ToString())
            .Select(s => s.Name)
            .ToListAsync(cancellationToken);
        session.AvailableContentTypes = types
            .Select(type => type.Name)
            .ToArray();
        session.AvailableStatuses = statusNames
            .ToArray();
        var validTypes = typeNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var validStatuses = statusNames.Select(MappingExtensions.NormalizeName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<BookImportKey> existingKeys = await _context.Books.AsNoTracking()
            .Where(b => b.OwnerId == session.OwnerId)
            .Select(b => new BookImportKey(b.NormalizedPrimaryTitle, b.ContentTypeId))
            .ToHashSetAsync(cancellationToken);

        foreach (ImportRow row in session.Rows)
        {
            row.Errors.Clear();
            row.FieldErrors.Clear();

            BookCsvImportRowMapper.NormalizeRow(row);

            if (string.IsNullOrWhiteSpace(row.PrimaryTitle))
            {
                BookCsvImportRowMapper.AddFieldError(row, "primaryTitle", "Primary title is required.");
            }

            string? normalizedContentType = string.IsNullOrWhiteSpace(row.ContentType)
                ? null
                : MappingExtensions.NormalizeName(row.ContentType);
            string? normalizedStatus = string.IsNullOrWhiteSpace(row.Status)
                ? null
                : MappingExtensions.NormalizeName(row.Status);

            if (normalizedContentType == null || !validTypes.Contains(normalizedContentType))
            {
                BookCsvImportRowMapper.AddFieldError(row, "contentType",
                    $"Content type is required and must exist. Allowed values: {string.Join(", ", types.Select(type => type.Name))}.");
            }

            if (normalizedStatus == null || !validStatuses.Contains(normalizedStatus))
            {
                BookCsvImportRowMapper.AddFieldError(row, "status",
                    $"Status is required and must exist. Allowed values: {string.Join(", ", statusNames)}.");
            }

            decimal? totalChapters =
                BookCsvImportRowMapper.ParseDecimal(row, row.TotalChapters, "totalChapters", nameof(row.TotalChapters));
            decimal? currentChapterNumber = BookCsvImportRowMapper.ParseDecimal(row, row.CurrentChapterNumber,
                "currentChapterNumber", nameof(row.CurrentChapterNumber));
            _ = BookCsvImportRowMapper.ParseInt(row, row.Rating, "rating", nameof(row.Rating), 1, 10);
            _ = BookCsvImportRowMapper.ParseInt(row, row.Priority, "priority", nameof(row.Priority), 1, 5);

            if (currentChapterNumber.HasValue && currentChapterNumber < 0)
            {
                BookCsvImportRowMapper.AddFieldError(row, "currentChapterNumber",
                    "Current chapter number cannot be negative.");
            }

            if (totalChapters.HasValue && totalChapters < 0)
            {
                BookCsvImportRowMapper.AddFieldError(row, "totalChapters", "Total chapters cannot be negative.");
            }

            if (totalChapters.HasValue && currentChapterNumber.HasValue && currentChapterNumber > totalChapters)
            {
                BookCsvImportRowMapper.AddFieldError(row, "currentChapterNumber",
                    "Current chapter number cannot exceed total chapters.");
            }

            if (!string.IsNullOrWhiteSpace(row.PrimaryTitle) &&
                normalizedContentType != null &&
                validTypes.Contains(normalizedContentType) &&
                existingKeys.Contains(new BookImportKey(
                    MappingExtensions.NormalizeName(row.PrimaryTitle),
                    typeIdsByName[normalizedContentType])))
            {
                BookCsvImportRowMapper.AddFieldError(row, "primaryTitle",
                    "A book with this title and content type already exists in your library.");
            }
        }

        IEnumerable<IGrouping<BookImportKey, ImportRow>> duplicateGroups = session.Rows
            .Where(row => !string.IsNullOrWhiteSpace(row.PrimaryTitle) && !string.IsNullOrWhiteSpace(row.ContentType) &&
                          validTypes.Contains(MappingExtensions.NormalizeName(row.ContentType)))
            .GroupBy(row => new BookImportKey(
                MappingExtensions.NormalizeName(row.PrimaryTitle!),
                typeIdsByName[MappingExtensions.NormalizeName(row.ContentType!)]))
            .Where(group => group.Count() > 1);

        foreach (IGrouping<BookImportKey, ImportRow> duplicateGroup in duplicateGroups)
        {
            foreach (ImportRow row in duplicateGroup)
            {
                BookCsvImportRowMapper.AddFieldError(row, "primaryTitle",
                    "Duplicate title with the same content type inside this import session.");
            }
        }
    }

    private ImportSession GetOwnedSession(Guid sessionId)
    {
        if (!Sessions.TryGetValue(sessionId, out ImportSession? session) || session.OwnerId != _user.RequiredId)
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

        string normalized = MappingExtensions.NormalizeName(authorName);
        if (authorMap.TryGetValue(normalized, out Author? author))
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
}
