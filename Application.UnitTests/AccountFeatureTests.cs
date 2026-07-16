using Application.Common.DTOs.User;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.AccountFeatures.Commands;

namespace Application.UnitTests;

public class AccountFeatureTests
{
    [Fact]
    public async Task RefreshTokenHandler_ShouldDelegateToIdentityService()
    {
        var expected = new TokenResponse
        {
            AccessToken = "jwt",
            RefreshToken = "refresh",
            RefreshTokenExpiresAt = DateTimeOffset.Parse("2026-08-01T10:00:00Z")
        };
        var service = new FakeIdentityService { RefreshResult = expected };
        var handler = new RefreshTokenHandler(service);

        var result = await handler.Handle(new RefreshTokenCommand("refresh-token"), CancellationToken.None);

        Assert.Same(expected, result);
        Assert.Equal("refresh-token", service.LastRefreshToken);
    }

    [Fact]
    public async Task LogoutHandler_ShouldDelegateToIdentityService()
    {
        var service = new FakeIdentityService();
        var handler = new LogoutHandler(service);

        await handler.Handle(new LogoutCommand("refresh-token"), CancellationToken.None);

        Assert.Equal("refresh-token", service.LastRevokedToken);
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        public TokenResponse RefreshResult { get; set; } = new()
        {
            AccessToken = string.Empty, RefreshToken = string.Empty, RefreshTokenExpiresAt = DateTimeOffset.UtcNow
        };

        public string? LastRefreshToken { get; private set; }
        public string? LastRevokedToken { get; private set; }

        public Task<RegisterResponse> RegisterUser(RegisterDto registerForm, CancellationToken cancellation)
        {
            throw new NotSupportedException();
        }

        public Task<TokenResponse> LoginUser(LoginDto loginForm, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
        {
            LastRefreshToken = refreshToken;
            return Task.FromResult(RefreshResult);
        }

        public Task RevokeRefreshTokenAsync(string? refreshToken, CancellationToken cancellationToken)
        {
            LastRevokedToken = refreshToken;
            return Task.CompletedTask;
        }
    }
}
