namespace Application.Common.Models;

using System.Text.Json.Serialization;

public sealed record TokenResponse
{
    public required string AccessToken { get; init; }
    [JsonIgnore]
    public string RefreshToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = AuthenticationSchemes.Bearer;
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset RefreshTokenExpiresAt { get; init; }
    public Guid UserId { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
