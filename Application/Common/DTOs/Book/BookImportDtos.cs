namespace Application.Common.DTOs.Book;

public record BookImportSessionDto
{
    public Guid SessionId { get; set; }
    public required string FileName { get; set; }
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public bool CanFinalize { get; set; }
    public IReadOnlyCollection<string> AvailableContentTypes { get; set; } = Array.Empty<string>();
    public IReadOnlyCollection<string> AvailableStatuses { get; set; } = Array.Empty<string>();
    public IReadOnlyCollection<BookImportRowDto> Rows { get; set; } = Array.Empty<BookImportRowDto>();
}

public record BookImportRowDto
{
    public Guid RowId { get; set; }
    public int LineNumber { get; set; }
    public bool IsValid { get; set; }
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
    public IReadOnlyCollection<string> Errors { get; set; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> FieldErrors { get; set; } =
        new Dictionary<string, IReadOnlyCollection<string>>();
}

public record UpdateBookImportRowRequest(
    string? PrimaryTitle,
    string? AuthorName,
    string? ContentType,
    string? Status,
    string? Tags,
    string? TotalChapters,
    string? CurrentChapterNumber,
    string? CurrentChapterLabel,
    string? Rating,
    string? Priority,
    string? Description,
    string? Notes,
    string? RawImportedLine);

public record BookImportFinalizeResultDto
{
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public IReadOnlyCollection<BookImportFinalizedBookDto> ImportedBooks { get; set; } = Array.Empty<BookImportFinalizedBookDto>();
    public IReadOnlyCollection<string> Errors { get; set; } = Array.Empty<string>();
}

public record BookImportFinalizedBookDto
{
    public required string PrimaryTitle { get; set; }
    public required string ContentType { get; set; }
    public required string Status { get; set; }
    public decimal? CurrentChapterNumber { get; set; }
    public string? CurrentChapterLabel { get; set; }
    public decimal? TotalChapters { get; set; }
}
