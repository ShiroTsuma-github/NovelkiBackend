namespace Domain.Associations;

using Entities;

public class BookTag
{
    public Guid BookId { get; set; }
    public Book Book { get; set; } = default!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = default!;
}
