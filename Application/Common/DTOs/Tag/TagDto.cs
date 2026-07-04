namespace Application.Common.DTOs.Tag;

public record TagDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
}
