namespace Application.Common.DTOs.Book;

public record BookDto
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required string Author { get; set; }
}