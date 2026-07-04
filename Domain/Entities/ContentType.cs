namespace Domain.Entities;

public class ContentType : BaseAuditableEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public ICollection<Book> Books { get; set; } = new HashSet<Book>();
}
