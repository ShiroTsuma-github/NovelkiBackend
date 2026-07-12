namespace Infrastructure.Services;

using Application.Common;
using Application.Common.DTOs.Book;
using Application.Common.Interfaces;
using Domain.Associations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using System.Collections.Concurrent;
using System.Globalization;

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

    public async Task<BookImportSessionDto> CreateSessionAsync(Stream csvStream, string fileName, CancellationToken cancellationToken)
    {
        var ownerId = _user.RequiredId;
        using var reader = new StreamReader(csvStream, leaveOpen: true);
        using var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        if (parser.EndOfData)
        {
            throw new ValidationException("CSV file is empty.");
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

                session.Rows.Add(new ImportRow
            {
                RowId = Guid.NewGuid(),
                LineNumber = NormalizeLineNumber((int?)parser.LineNumber),
                PrimaryTitle = CleanName(row, "primaryTitle"),
                AuthorName = CleanName(row, "authorName"),
                ContentType = CleanName(row, "contentType"),
                Status = CleanName(row, "status"),
                Tags = NormalizeTags(Clean(row, "tags")),
                TotalChapters = Clean(row, "totalChapters"),
                CurrentChapterNumber = Clean(row, "currentChapterNumber"),
                CurrentChapterLabel = Clean(row, "currentChapterLabel"),
                Rating = Clean(row, "rating"),
                Priority = Clean(row, "priority"),
                Description = Clean(row, "description"),
                Notes = NormalizeNotes(Clean(row, "notes")),
                RawImportedLine = Clean(row, "rawImportedLine")
            });
        }

        await RevalidateSessionAsync(session, cancellationToken);
        Sessions[session.SessionId] = session;
        return ToDto(session);
    }

    public async Task<BookImportSessionDto> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = GetOwnedSession(sessionId);
        await RevalidateSessionAsync(session, cancellationToken);
        return ToDto(session);
    }

    public async Task<BookImportSessionDto> UpdateRowAsync(Guid sessionId, Guid rowId, UpdateBookImportRowRequest request, CancellationToken cancellationToken)
    {
        var session = GetOwnedSession(sessionId);
        var row = session.Rows.FirstOrDefault(item => item.RowId == rowId)
            ?? throw new ValidationException("Import row not found.");

        row.PrimaryTitle = NormalizeNameToNull(request.PrimaryTitle);
        row.AuthorName = NormalizeNameToNull(request.AuthorName);
        row.ContentType = NormalizeNameToNull(request.ContentType);
        row.Status = NormalizeNameToNull(request.Status);
        row.Tags = NormalizeTags(request.Tags);
        row.TotalChapters = TrimToNull(request.TotalChapters);
        row.CurrentChapterNumber = TrimToNull(request.CurrentChapterNumber);
        row.CurrentChapterLabel = TrimToNull(request.CurrentChapterLabel);
        row.Rating = TrimToNull(request.Rating);
        row.Priority = TrimToNull(request.Priority);
        row.Description = TrimToNull(request.Description);
        row.Notes = NormalizeNotes(request.Notes);
        row.RawImportedLine = TrimToNull(request.RawImportedLine);

        await RevalidateSessionAsync(session, cancellationToken);
        return ToDto(session);
    }

    public async Task<BookImportSessionDto> DeleteRowAsync(Guid sessionId, Guid rowId, CancellationToken cancellationToken)
    {
        var session = GetOwnedSession(sessionId);
        var removed = session.Rows.RemoveAll(item => item.RowId == rowId);
        if (removed == 0)
        {
            throw new ValidationException("Import row not found.");
        }

        await RevalidateSessionAsync(session, cancellationToken);
        return ToDto(session);
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

        foreach (var row in session.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedTitle = MappingExtensions.NormalizeName(row.PrimaryTitle!);
            var contentTypeId = typeMap[MappingExtensions.NormalizeName(row.ContentType!)].Id;
            var importKey = new BookImportKey(normalizedTitle, contentTypeId);
            if (existingKeys.Contains(importKey))
            {
                skipped++;
                errors.Add($"Line {row.LineNumber}: title '{row.PrimaryTitle}' already exists for content type '{row.ContentType}'.");
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
                TotalChapters = ParseDecimal(row.TotalChapters),
                CurrentChapterNumber = ParseDecimal(row.CurrentChapterNumber),
                CurrentChapterLabel = row.CurrentChapterLabel,
                Rating = ParseInt(row.Rating),
                Priority = ParseInt(row.Priority),
                Notes = row.Notes,
                RawImportedLine = row.RawImportedLine,
                Cover = new BookCover()
            };

            book.Titles.Add(book.PrimaryTitle.ToPrimaryTitle());

            foreach (var tagName in SplitTags(row.Tags))
            {
                var normalizedTag = MappingExtensions.NormalizeName(tagName);
                if (!tagMap.TryGetValue(normalizedTag, out var tag))
                {
                    tag = new Tag
                    {
                        OwnerId = ownerId,
                        Name = tagName,
                        NormalizedName = normalizedTag
                    };
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
            ImportedCount = imported,
            SkippedCount = skipped,
            Errors = errors
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
            .ToListAsync(cancellationToken);
        var typeNames = types.Select(t => MappingExtensions.NormalizeName(t.Name)).ToList();
        var typeIdsByName = types.ToDictionary(t => MappingExtensions.NormalizeName(t.Name), t => t.Id);
        var statusNames = await _context.Statuses.AsNoTracking()
            .Select(s => s.Name)
            .ToListAsync(cancellationToken);
        session.AvailableContentTypes = types
            .Select(type => type.Name)
            .OrderBy(name => name)
            .ToArray();
        session.AvailableStatuses = statusNames
            .OrderBy(name => name)
            .ToArray();
        var validTypes = typeNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var validStatuses = statusNames.Select(MappingExtensions.NormalizeName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingKeys = await _context.Books.AsNoTracking()
            .Where(b => b.OwnerId == session.OwnerId)
            .Select(b => new BookImportKey(b.NormalizedPrimaryTitle, b.ContentTypeId))
            .ToHashSetAsync(cancellationToken);

        foreach (var row in session.Rows)
        {
            row.Errors.Clear();
            row.FieldErrors.Clear();

            row.PrimaryTitle = NormalizeNameToNull(row.PrimaryTitle);
            row.AuthorName = NormalizeNameToNull(row.AuthorName);
            row.ContentType = NormalizeNameToNull(row.ContentType);
            row.Status = NormalizeNameToNull(row.Status);
            row.Tags = NormalizeTags(row.Tags);
            row.TotalChapters = TrimToNull(row.TotalChapters);
            row.CurrentChapterNumber = TrimToNull(row.CurrentChapterNumber);
            row.CurrentChapterLabel = TrimToNull(row.CurrentChapterLabel);
            row.Rating = TrimToNull(row.Rating);
            row.Priority = TrimToNull(row.Priority);
            row.Description = TrimToNull(row.Description);
            row.Notes = NormalizeNotes(row.Notes);
            row.RawImportedLine = TrimToNull(row.RawImportedLine);

            if (string.IsNullOrWhiteSpace(row.PrimaryTitle))
            {
                AddFieldError(row, "primaryTitle", "Primary title is required.");
            }

            var normalizedContentType = string.IsNullOrWhiteSpace(row.ContentType) ? null : MappingExtensions.NormalizeName(row.ContentType);
            var normalizedStatus = string.IsNullOrWhiteSpace(row.Status) ? null : MappingExtensions.NormalizeName(row.Status);

            if (normalizedContentType == null || !validTypes.Contains(normalizedContentType))
            {
                AddFieldError(row, "contentType", $"Content type is required and must exist. Allowed values: {string.Join(", ", types.Select(type => type.Name))}.");
            }

            if (normalizedStatus == null || !validStatuses.Contains(normalizedStatus))
            {
                AddFieldError(row, "status", $"Status is required and must exist. Allowed values: {string.Join(", ", statusNames)}.");
            }

            var totalChapters = ParseDecimal(row, row.TotalChapters, "totalChapters", nameof(row.TotalChapters));
            var currentChapterNumber = ParseDecimal(row, row.CurrentChapterNumber, "currentChapterNumber", nameof(row.CurrentChapterNumber));
            _ = ParseInt(row, row.Rating, "rating", nameof(row.Rating), 1, 10);
            _ = ParseInt(row, row.Priority, "priority", nameof(row.Priority), 1, 5);

            if (currentChapterNumber.HasValue && currentChapterNumber < 0)
            {
                AddFieldError(row, "currentChapterNumber", "Current chapter number cannot be negative.");
            }

            if (totalChapters.HasValue && totalChapters < 0)
            {
                AddFieldError(row, "totalChapters", "Total chapters cannot be negative.");
            }

            if (totalChapters.HasValue && currentChapterNumber.HasValue && currentChapterNumber > totalChapters)
            {
                AddFieldError(row, "currentChapterNumber", "Current chapter number cannot exceed total chapters.");
            }

            if (!string.IsNullOrWhiteSpace(row.PrimaryTitle) &&
                normalizedContentType != null &&
                validTypes.Contains(normalizedContentType) &&
                existingKeys.Contains(new BookImportKey(
                    MappingExtensions.NormalizeName(row.PrimaryTitle),
                    typeIdsByName[normalizedContentType])))
            {
                AddFieldError(row, "primaryTitle", "A book with this title and content type already exists in your library.");
            }
        }

        var duplicateGroups = session.Rows
            .Where(row => !string.IsNullOrWhiteSpace(row.PrimaryTitle) && !string.IsNullOrWhiteSpace(row.ContentType) && validTypes.Contains(MappingExtensions.NormalizeName(row.ContentType)))
            .GroupBy(row => new BookImportKey(
                MappingExtensions.NormalizeName(row.PrimaryTitle!),
                typeIdsByName[MappingExtensions.NormalizeName(row.ContentType!)]))
            .Where(group => group.Count() > 1);

        foreach (var duplicateGroup in duplicateGroups)
        {
            foreach (var row in duplicateGroup)
            {
                AddFieldError(row, "primaryTitle", "Duplicate title with the same content type inside this import session.");
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

        author = new Author
        {
            PrimaryName = authorName,
            NormalizedPrimaryName = normalized
        };
        author.Names.Add(new AuthorName
        {
            Name = authorName,
            NormalizedName = normalized,
            IsPrimary = true,
            Source = "CSV import"
        });
        authorMap[normalized] = author;
        _context.Authors.Add(author);
        return author;
    }

    private static BookImportSessionDto ToDto(ImportSession session)
    {
        return new BookImportSessionDto
        {
            SessionId = session.SessionId,
            FileName = session.FileName,
            TotalRows = session.Rows.Count,
            ValidRows = session.Rows.Count(row => row.Errors.Count == 0),
            InvalidRows = session.Rows.Count(row => row.Errors.Count > 0),
            CanFinalize = session.Rows.Count > 0 && session.Rows.All(row => row.Errors.Count == 0),
            AvailableContentTypes = session.AvailableContentTypes,
            AvailableStatuses = session.AvailableStatuses,
            Rows = session.Rows
                .OrderBy(row => row.LineNumber)
                .Select(row => new BookImportRowDto
                {
                    RowId = row.RowId,
                    LineNumber = row.LineNumber,
                    IsValid = row.Errors.Count == 0,
                    PrimaryTitle = row.PrimaryTitle,
                    AuthorName = row.AuthorName,
                    ContentType = row.ContentType,
                    Status = row.Status,
                    Tags = row.Tags,
                    TotalChapters = row.TotalChapters,
                    CurrentChapterNumber = row.CurrentChapterNumber,
                    CurrentChapterLabel = row.CurrentChapterLabel,
                    Rating = row.Rating,
                    Priority = row.Priority,
                    Description = row.Description,
                    Notes = row.Notes,
                    RawImportedLine = row.RawImportedLine,
                    Errors = row.Errors.ToArray(),
                    FieldErrors = row.FieldErrors.ToDictionary(
                        pair => pair.Key,
                        pair => (IReadOnlyCollection<string>)pair.Value.ToArray())
                })
                .ToList()
        };
    }

    private static string? Clean(IReadOnlyDictionary<string, string> row, string key)
    {
        return row.TryGetValue(key, out var value) ? TrimToNull(value) : null;
    }

    private static string? CleanName(IReadOnlyDictionary<string, string> row, string key)
    {
        return row.TryGetValue(key, out var value) ? NormalizeNameToNull(value) : null;
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeNameToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : MappingExtensions.CollapseWhitespace(value);
    }

    private static string? NormalizeTags(string? value)
    {
        var tags = SplitTags(value).ToArray();
        return tags.Length == 0 ? null : string.Join("; ", tags);
    }

    private static string? NormalizeNotes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split(['|', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0);
        var result = string.Join('\n', normalized);
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private static IEnumerable<string> SplitTags(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(MappingExtensions.CollapseWhitespace)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static decimal? ParseDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static decimal? ParseDecimal(ImportRow row, string? value, string fieldKey, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            AddFieldError(row, fieldKey, $"{fieldName} must be a valid number.");
            return null;
        }

        return parsed;
    }

    private static int? ParseInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static int? ParseInt(ImportRow row, string? value, string fieldKey, string fieldName, int min, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            AddFieldError(row, fieldKey, $"{fieldName} must be a valid integer.");
            return null;
        }

        if (parsed < min || parsed > max)
        {
            AddFieldError(row, fieldKey, $"{fieldName} must be between {min} and {max}.");
        }

        return parsed;
    }

    private static void AddFieldError(ImportRow row, string fieldKey, string message)
    {
        row.Errors.Add(message);

        if (!row.FieldErrors.TryGetValue(fieldKey, out var errors))
        {
            errors = [];
            row.FieldErrors[fieldKey] = errors;
        }

        errors.Add(message);
    }

    private static int NormalizeLineNumber(int? lineNumber)
    {
        return lineNumber.HasValue && lineNumber.Value > 0 ? lineNumber.Value : 1;
    }

    private readonly record struct BookImportKey(string NormalizedTitle, Guid ContentTypeId);

    private sealed class ImportSession
    {
        public Guid SessionId { get; init; }
        public Guid OwnerId { get; init; }
        public string FileName { get; init; } = string.Empty;
        public IReadOnlyCollection<string> AvailableContentTypes { get; set; } = Array.Empty<string>();
        public IReadOnlyCollection<string> AvailableStatuses { get; set; } = Array.Empty<string>();
        public List<ImportRow> Rows { get; } = [];
    }

    private sealed class ImportRow
    {
        public Guid RowId { get; init; }
        public int LineNumber { get; init; }
        public string? PrimaryTitle { get; set; }
        public string? AuthorName { get; set; }
        public string? ContentType { get; set; }
        public string? Status { get; set; }
        public string? Tags { get; set; }
        public string? TotalChapters { get; set; }
        public string? CurrentChapterNumber { get; set; }
        public string? CurrentChapterLabel { get; set; }
        public string? Rating { get; set; }
        public string? Priority { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }
        public string? RawImportedLine { get; set; }
        public List<string> Errors { get; set; } = [];
        public Dictionary<string, List<string>> FieldErrors { get; } = [];
    }
}
