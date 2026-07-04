namespace Domain.Entities;

public class BookLink : BaseAuditableEntity
{
    public Guid BookId { get; set; }
    public Book Book { get; set; } = default!;
    public required string Url { get; set; }
    public string? Label { get; set; }
    public required string SourceType { get; set; }
    public bool IsPrimary { get; set; }
    public bool LastReadHere { get; set; }
}
