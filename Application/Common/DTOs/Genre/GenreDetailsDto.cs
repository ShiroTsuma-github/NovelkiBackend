namespace Application.Common.DTOs.Genre;

using Application.Common.DTOs.Book;

public class GenreDetailsDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int BookCount => Books.Count;
    public ICollection<BookDto> Books { get; set; } = new HashSet<BookDto>();
}
