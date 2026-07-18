namespace Application.Common.DTOs.Author;

public record AuthorDto
{
    public Guid Id { get; set; }
    public required string PrimaryName { get; set; }
    public IReadOnlyCollection<string> OtherNames { get; set; } = Array.Empty<string>();
    public bool IsPublic { get; set; }
}
