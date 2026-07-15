namespace Infrastructure.Identity;

using Domain.Associations;

public class User : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<Book> Books { get; set; } = new HashSet<Book>();
    public ICollection<Tag> OwnedTags { get; set; } = new HashSet<Tag>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new HashSet<RefreshToken>();
}
