namespace Application.Common.Models;

public sealed record TokenResponse
{
    public required string AccessToken { get; init; }
    public string TokenType { get; init; } = "Bearer";
    public DateTimeOffset ExpiresAt { get; init; }
    public Guid UserId { get; init; }

}
