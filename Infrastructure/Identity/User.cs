namespace Infrastructure.Identity;

public class User : IdentityUser<Guid>
{
    public ICollection<Book> Books { get; set; } = new HashSet<Book>();
    public ICollection<Tag> OwnedTags { get; set; } = new HashSet<Tag>();
    public ICollection<BookTagAssociation> TagAssociations { get; set; } = new HashSet<BookTagAssociation>();
}