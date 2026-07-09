namespace Application.Common.DTOs.Book;

public record BookDto
{
    public Guid Id { get; set; }
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset LastModified { get; set; }
    public required string PrimaryTitle { get; set; }
    public string? Description { get; set; }
    public IReadOnlyCollection<string> AlternativeTitles { get; set; } = Array.Empty<string>();
    public Guid? AuthorId { get; set; }
    public string? Author { get; set; }
    public required string ContentType { get; set; }
    public required string Status { get; set; }
    public decimal? CurrentChapterNumber { get; set; }
    public string? CurrentChapterLabel { get; set; }
    public decimal? TotalChapters { get; set; }
    public int? Rating { get; set; }
    public int? Priority { get; set; }
    public string? Comment { get; set; }
    public string? Notes { get; set; }
    public BookCoverDto? Cover { get; set; }
    public IReadOnlyCollection<string> Genres { get; set; } = Array.Empty<string>();
    public IReadOnlyCollection<string> Tags { get; set; } = Array.Empty<string>();
    public IReadOnlyCollection<BookLinkDto> Links { get; set; } = Array.Empty<BookLinkDto>();
}

public record AdminBookDto : BookDto
{
    public Guid OwnerId { get; set; }
}

public record BookLinkDto
{
    public Guid Id { get; set; }
    public required string Url { get; set; }
    public string? Label { get; set; }
    public required string SourceType { get; set; }
    public bool IsPrimary { get; set; }
    public bool LastReadHere { get; set; }
}

public record BookCoverDto
{
    public Guid Id { get; set; }
    public required string Status { get; set; }
    public string? Source { get; set; }
    public string? ImageUrl { get; set; }
    public string? OriginalImageUrl { get; set; }
    public string? MimeType { get; set; }
    public long? SizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
}

public record BookTitleInput(string Title, string? Language = null, string? Source = null);

public record BookLinkInput(
    string Url,
    string? Label = null,
    string SourceType = "Other",
    bool IsPrimary = false,
    bool LastReadHere = false);
