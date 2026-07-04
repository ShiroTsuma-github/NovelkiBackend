namespace Domain.Entities;

public class AuthorName : BaseAuditableEntity
{
    public Guid AuthorId { get; set; }
    public Author Author { get; set; } = default!;
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public string? Language { get; set; }
    public bool IsPrimary { get; set; }
    public string? Source { get; set; }
}
