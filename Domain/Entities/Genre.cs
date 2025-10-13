namespace Domain.Entities;

public class Genre : BaseAuditableEntity
{
    public required string Name {  get; set; }
    public string? Description { get; set; }
    public ICollection<Book> Books { get; set; } = new HashSet<Book>();
}
