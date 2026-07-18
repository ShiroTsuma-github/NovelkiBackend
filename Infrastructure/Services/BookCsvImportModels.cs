namespace Infrastructure.Services;

internal readonly record struct BookImportKey(string NormalizedTitle, Guid ContentTypeId);

internal readonly record struct BookFullBackupKey(string NormalizedTitle, string NormalizedContentType);

internal sealed record BookFullBackupCover(string StagedPath, string FileName, string? MimeType, long SizeBytes);

internal sealed class ImportSession
{
    public Guid SessionId { get; init; }
    public Guid OwnerId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public IReadOnlyCollection<string> AvailableContentTypes { get; set; } = Array.Empty<string>();
    public IReadOnlyCollection<string> AvailableStatuses { get; set; } = Array.Empty<string>();
    public List<ImportRow> Rows { get; } = [];
    public Dictionary<Guid, BookFullBackupCover> CoversByRowId { get; } = [];
    public bool IsFullImport { get; set; }
    public string? TempDirectory { get; set; }
    public long StagedBytes { get; set; }
    public long ReservedStagedBytes { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastAccessAt { get; set; } = DateTimeOffset.UtcNow;
    public SemaphoreSlim OperationLock { get; } = new(1, 1);
}

internal sealed class ImportRow
{
    public Guid RowId { get; init; }
    public int LineNumber { get; init; }
    public string? PrimaryTitle { get; set; }
    public string? AlternativeTitles { get; set; }
    public string? AuthorName { get; set; }
    public string? ContentType { get; set; }
    public string? Status { get; set; }
    public string? Genres { get; set; }
    public string? Tags { get; set; }
    public string? TotalChapters { get; set; }
    public string? CurrentChapterNumber { get; set; }
    public string? CurrentChapterLabel { get; set; }
    public string? Rating { get; set; }
    public string? Priority { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string? RawImportedLine { get; set; }
    public string? Links { get; set; }
    public string? ProgressHistory { get; set; }
    public List<string> Errors { get; set; } = [];
    public Dictionary<string, List<string>> FieldErrors { get; } = [];
}

internal sealed record BookProgressHistoryCsvItem(
    DateTimeOffset ChangedAt,
    decimal? ChapterNumber,
    string? ChapterLabel,
    string? Comment);
