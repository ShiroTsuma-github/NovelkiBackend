namespace Domain.Entities;

public class Author : BaseAuditableEntity
{
    public required string PrimaryName { get; set; }
    public required string NormalizedPrimaryName { get; set; }
    public Guid? OwnerId { get; set; }
    public bool IsPublic { get; set; }
    public ICollection<AuthorName> Names { get; set; } = new HashSet<AuthorName>();
    public ICollection<Book> Books { get; set; } = new HashSet<Book>();
}
