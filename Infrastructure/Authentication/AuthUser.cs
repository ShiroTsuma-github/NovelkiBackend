
namespace Infrastructure.Authentication;

public class AuthUser : IUser
{
    public Guid? Id { get; set; }

    public string? Email {  get; set; }

    public string? Username {  get; set; }

    public IEnumerable<string> Roles { get; set; } = [];

    public DateTimeOffset? CreatedAt { get; set; }

    public bool IsAuthenticated {  get; set; }

    public bool Valid {  get; set; }

    public Guid RequiredId => Id ?? throw new UnauthorizedAccessException("Attempted to access required user ID when the user was not logged in or the claim was missing.");
}
