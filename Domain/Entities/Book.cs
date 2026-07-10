namespace Domain.Entities;

using Domain.Associations;

public class Book : BaseAuditableEntity
{
    public required string PrimaryTitle { get; set; }
    public required string NormalizedPrimaryTitle { get; set; }
    public string? Description { get; set; }
    public Guid? AuthorId { get; set; }
    public Author? Author { get; set; }
    public Guid ContentTypeId { get; set; }
    public ContentType ContentType { get; set; } = default!;
    public Guid StatusId { get; set; }
    public Status Status { get; set; } = default!;
    public Guid OwnerId { get; set; }
    public BookCover? Cover { get; set; }
    public ICollection<BookTitle> Titles { get; set; } = new HashSet<BookTitle>();
    public ICollection<BookGenre> BookGenres { get; set; } = new HashSet<BookGenre>();
    public ICollection<BookTag> BookTags { get; set; } = new HashSet<BookTag>();
    public ICollection<BookLink> Links { get; set; } = new HashSet<BookLink>();
    public ICollection<BookProgressHistory> ProgressHistory { get; set; } = new HashSet<BookProgressHistory>();
    public decimal? TotalChapters { get; set; }
    public decimal? CurrentChapterNumber { get; set; }
    public string? CurrentChapterLabel { get; set; }
    public string? Notes { get; set; }
    public string? RawImportedLine { get; set; }
    public int? Priority { get; set; }
    public int? Rating { get; set; }
}
