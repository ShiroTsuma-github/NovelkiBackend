namespace Application.Common.Models;

public sealed record TokenResponse
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public string TokenType { get; init; } = AuthenticationSchemes.Bearer;
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset RefreshTokenExpiresAt { get; init; }
    public Guid UserId { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
