namespace Domain.Entities;

public class BookCover : BaseAuditableEntity
{
    public Guid BookId { get; set; }
    public Book Book { get; set; } = default!;
    public BookCoverStatus Status { get; set; } = BookCoverStatus.Pending;
    public BookCoverSource? Source { get; set; }
    public string? StoragePath { get; set; }
    public string? ThumbnailStoragePath { get; set; }
    public string? OriginalImageUrl { get; set; }
    public string? MimeType { get; set; }
    public string? ThumbnailMimeType { get; set; }
    public long? SizeBytes { get; set; }
    public long? ThumbnailSizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? ThumbnailWidth { get; set; }
    public int? ThumbnailHeight { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
}

public enum BookCoverStatus
{
    Pending,
    Found,
    Uploaded,
    NotFound,
    Failed
}

public enum BookCoverSource
{
    ManualUpload,
    ManualUrl,
    BookLinkMetadata,
    Jikan,
    AniList,
    GoogleBooks,
    OpenLibrary,
    Wikidata,
    NovelUpdates
}
