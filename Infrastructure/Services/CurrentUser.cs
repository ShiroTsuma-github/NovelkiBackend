namespace Infrastructure.Services;

using Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

public class CurrentUser : IUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? Id
    {
        get
        {
            string? userIdClaim = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(userIdClaim, out Guid guid) ? guid : null;
        }
    }

    public Guid RequiredId => Id ?? throw new UnauthorizedAccessException(
        "Attempted to access required user ID when the user was not logged in or the claim was missing.");

    public string? Email => User?.FindFirstValue(ClaimTypes.Email);

    public string? Username => User?.FindFirstValue(ClaimTypes.Name);

    public DateTimeOffset? CreatedAt
    {
        get
        {
            string? createdAtClaim = User?.FindFirstValue("created_at");
            return DateTimeOffset.TryParse(createdAtClaim, out DateTimeOffset createdAt) ? createdAt : null;
        }
    }

    public IEnumerable<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? Enumerable.Empty<string>();

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool Valid => !(string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Email) || Id == null);
}
