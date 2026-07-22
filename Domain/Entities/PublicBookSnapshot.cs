namespace Domain.Entities;

public sealed class PublicBookSnapshot : BaseAuditableEntity
{
    public Guid SourceBookId { get; set; }
    public Book SourceBook { get; set; } = default!;
    public Guid OwnerId { get; set; }
    public required string PrimaryTitle { get; set; }
    public required string NormalizedPrimaryTitle { get; set; }
    public string? Description { get; set; }
    public required string AlternativeTitlesJson { get; set; }
    public string? AuthorName { get; set; }
    public required string AuthorOtherNamesJson { get; set; }
    public Guid? PublicAuthorId { get; set; }
    public required string ContentType { get; set; }
    public decimal? TotalChapters { get; set; }
    public required string GenresJson { get; set; }
    public required string TagsJson { get; set; }
    public required string PublicTagIdsJson { get; set; }
    public string? CoverStoragePath { get; set; }
    public string? CoverThumbnailStoragePath { get; set; }
    public string? CoverMimeType { get; set; }
    public DateTimeOffset SnapshotAt { get; set; }
}

public sealed class BookShareAuthorPromotion
{
    public Guid AuthorId { get; set; }
    public Author Author { get; set; } = default!;
}

public sealed class BookShareTagPromotion
{
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = default!;
}
