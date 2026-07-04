namespace Infrastructure.Identity;

using Domain.Associations;

public class User : IdentityUser<Guid>
{
    public ICollection<Book> Books { get; set; } = new HashSet<Book>();
    public ICollection<Tag> OwnedTags { get; set; } = new HashSet<Tag>();
}
