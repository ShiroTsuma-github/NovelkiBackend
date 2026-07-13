namespace Infrastructure.Services;

internal readonly record struct BookImportKey(string NormalizedTitle, Guid ContentTypeId);

internal sealed class ImportSession
{
    public Guid SessionId { get; init; }
    public Guid OwnerId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public IReadOnlyCollection<string> AvailableContentTypes { get; set; } = Array.Empty<string>();
    public IReadOnlyCollection<string> AvailableStatuses { get; set; } = Array.Empty<string>();
    public List<ImportRow> Rows { get; } = [];
}

internal sealed class ImportRow
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
