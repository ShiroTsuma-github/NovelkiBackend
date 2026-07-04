namespace Domain.Entities;

public class BookTitle : BaseAuditableEntity
{
    public Guid BookId { get; set; }
    public Book Book { get; set; } = default!;
    public required string Title { get; set; }
    public required string NormalizedTitle { get; set; }
    public string? Language { get; set; }
    public bool IsPrimary { get; set; }
    public string? Source { get; set; }
}
