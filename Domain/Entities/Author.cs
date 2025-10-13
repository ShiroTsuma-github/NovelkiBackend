namespace Domain.Entities;

public class Author : BaseAuditableEntity
{
    public required string Name { get; set; }
    public IEnumerable<string> OtherNames { get; set; } = new List<string>();
    public IEnumerable<Book> Books { get; set; } = new HashSet<Book>();
}
