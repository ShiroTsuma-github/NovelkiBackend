namespace Application.Common.DTOs.Book;

public sealed class BookFullBackupManifest
{
    public int Version { get; init; } = 1;
    public List<BookFullBackupManifestItem> Books { get; init; } = [];
}

public sealed class BookFullBackupManifestItem
{
    public required string PrimaryTitle { get; init; }
    public required string ContentType { get; init; }
    public string? OriginalCoverPath { get; init; }
    public string? OriginalCoverMimeType { get; init; }
    public string? ThumbnailCoverPath { get; init; }
    public string? ThumbnailCoverMimeType { get; init; }
}
