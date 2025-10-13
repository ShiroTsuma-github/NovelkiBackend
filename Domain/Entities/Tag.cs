namespace Domain.Entities;

using Domain.Associations;

public class Tag : BaseAuditableEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required bool Public { get; set; } = false;
    public Guid OwnerId { get; set; }
    public ICollection<BookTagAssociation> BookAssociations { get; set; } = new HashSet<BookTagAssociation>();
}
