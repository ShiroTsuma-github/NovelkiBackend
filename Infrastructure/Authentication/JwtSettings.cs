namespace Infrastructure.Authentication;

public sealed record JwtSettings
{
    public required string Key { get; init; }
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public int AccessTokenMinutes { get; init; } = 10;
}
