using Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Application.UnitTests;

public class CurrentUserTests
{
    [Fact]
    public void Properties_ShouldReadClaimsFromHttpContext()
    {
        var userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, "reader@example.com"),
            new Claim(ClaimTypes.Name, "reader"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "Reader")
        ], "Test"));
        var currentUser = new CurrentUser(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        });

        Assert.Equal(userId, currentUser.Id);
        Assert.Equal(userId, currentUser.RequiredId);
        Assert.Equal("reader@example.com", currentUser.Email);
        Assert.Equal("reader", currentUser.Username);
        Assert.Equal(["Admin", "Reader"], currentUser.Roles);
        Assert.True(currentUser.IsAuthenticated);
        Assert.True(currentUser.Valid);
    }

    [Fact]
    public void Properties_ShouldHandleMissingOrInvalidClaims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "not-a-guid")
        ], "Test"));
        var currentUser = new CurrentUser(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        });

        Assert.Null(currentUser.Id);
        Assert.Null(currentUser.Email);
        Assert.Null(currentUser.Username);
        Assert.Empty(currentUser.Roles);
        Assert.False(currentUser.Valid);
    }

    [Fact]
    public void RequiredId_ShouldThrow_WhenUserIdIsUnavailable()
    {
        var currentUser = new CurrentUser(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        });

        Assert.Throws<UnauthorizedAccessException>(() => _ = currentUser.RequiredId);
    }
}
