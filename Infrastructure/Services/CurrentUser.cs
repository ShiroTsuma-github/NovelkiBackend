namespace Infrastructure.Services;

using Application.Common.Interfaces;
using Infrastructure.Authentication;
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
            var userIdClaim = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(userIdClaim, out var guid) ? guid : null;
        }
    }

    public Guid RequiredId => Id ?? throw new UnauthorizedAccessException(UserErrorMessages.RequiredIdUnavailable);

    public string? Email => User?.FindFirstValue(ClaimTypes.Email);

    public string? Username => User?.FindFirstValue(ClaimTypes.Name);

    public DateTimeOffset? CreatedAt
    {
        get
        {
            var createdAtClaim = User?.FindFirstValue(CustomClaimTypes.CreatedAt);
            return DateTimeOffset.TryParse(createdAtClaim, out var createdAt) ? createdAt : null;
        }
    }

    public IEnumerable<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? Enumerable.Empty<string>();

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool Valid => !(string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Email) || Id == null);
}
