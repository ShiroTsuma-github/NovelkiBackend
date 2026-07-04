namespace Domain.Associations;

using Domain.Entities;

public class BookGenre
{
    public Guid BookId { get; set; }
    public Book Book { get; set; } = default!;
    public Guid GenreId { get; set; }
    public Genre Genre { get; set; } = default!;
}
