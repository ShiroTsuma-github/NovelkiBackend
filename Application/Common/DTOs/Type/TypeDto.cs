namespace Application.Common.DTOs.Type;

public class TypeDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
}
