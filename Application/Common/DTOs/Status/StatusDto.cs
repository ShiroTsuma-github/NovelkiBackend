namespace Application.Common.DTOs.Status;
public class StatusDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
}
