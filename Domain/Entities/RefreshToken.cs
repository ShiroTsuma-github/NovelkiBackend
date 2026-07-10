namespace Domain.Entities;

public class RefreshToken : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? ReasonRevoked { get; set; }

    public bool IsActive => RevokedAt == null && ExpiresAt > DateTimeOffset.UtcNow;
}
