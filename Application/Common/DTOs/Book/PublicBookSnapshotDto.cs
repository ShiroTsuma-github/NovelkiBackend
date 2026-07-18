namespace Application.Common.DTOs.Book;

public sealed record PublicBookMetadataDto(string Name, string? Description);

public sealed record PublicBookSnapshotDto
{
    public Guid Id { get; init; }
    public Guid SourceBookId { get; init; }
    public required string PrimaryTitle { get; init; }
    public string? Description { get; init; }
    public IReadOnlyCollection<string> AlternativeTitles { get; init; } = [];
    public string? Author { get; init; }
    public IReadOnlyCollection<string> AuthorOtherNames { get; init; } = [];
    public required string ContentType { get; init; }
    public IReadOnlyCollection<PublicBookMetadataDto> Genres { get; init; } = [];
    public IReadOnlyCollection<PublicBookMetadataDto> Tags { get; init; } = [];
    public string? CoverUrl { get; init; }
    public DateTimeOffset SnapshotAt { get; init; }
    public bool IsOwner { get; init; }
}

public sealed record CopyPublicBookResult(Guid BookId);
