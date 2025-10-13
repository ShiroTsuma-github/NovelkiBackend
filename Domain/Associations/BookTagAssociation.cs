namespace Domain.Associations;

using Domain.Entities;

public class BookTagAssociation : BaseAuditableEntity
{
    public Guid BookId { get; set; }
    public Guid TagId { get; set; }
    public Guid OwnerId { get; set; }

    public Book Book { get; set; } = default!;
    public Tag Tag { get; set; } = default!;
}
