namespace Domain.Entities;

public class BookProgressHistory : BaseAuditableEntity
{
    public Guid BookId { get; set; }
    public Book Book { get; set; } = default!;
    public decimal? ChapterNumber { get; set; }
    public string? ChapterLabel { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Comment { get; set; }
}
