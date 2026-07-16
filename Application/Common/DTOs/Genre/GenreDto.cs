namespace Application.Common.DTOs.Genre;

public class GenreDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
}
