namespace Domain.Entities;

using Domain.Associations;

public class Tag : BaseAuditableEntity
{
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
    public Guid OwnerId { get; set; }
    public ICollection<BookTag> BookTags { get; set; } = new HashSet<BookTag>();
}
