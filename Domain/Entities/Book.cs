namespace Domain.Entities;

using Domain.Associations;

public class Book : BaseAuditableEntity
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public Guid AuthorId { get; set; }
    public Author Author { get; set; } = default!;
    public Guid TypeId { get; set; }
    public Type Type { get; set; } = default!;
    public Guid StatusId { get; set; }
    public Status Status { get; set; } = default!;
    public Guid OwnerId { get; set; }
    public ICollection<Genre> Genres { get; set; } = new HashSet<Genre>();
    public ICollection<BookTagAssociation> BookTags { get; set; } = new HashSet<BookTagAssociation>();
    public int TotalChapters { get; set; }
    public int Chapter { get; set; }
    public string? Notes { get; set; }
    public int Priority { get; set; }
    public int Grade { get; set; }
    public IEnumerable<string> Links { get;set; } = Enumerable.Empty<string>();
}
