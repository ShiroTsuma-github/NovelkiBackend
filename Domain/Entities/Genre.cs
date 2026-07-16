namespace Domain.Entities;

using Associations;

public class Genre : BaseAuditableEntity
{
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public string? Description { get; set; }
    public ICollection<BookGenre> BookGenres { get; set; } = new HashSet<BookGenre>();
}
