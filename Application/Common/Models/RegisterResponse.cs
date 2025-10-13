namespace Application.Common.Models;

public sealed record RegisterResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
