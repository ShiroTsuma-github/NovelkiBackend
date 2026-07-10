namespace Application.Common.Interfaces;

using Application.Common.DTOs.User;

public interface IIdentityService
{
    Task<RegisterResponse> RegisterUser(RegisterDto registerForm, CancellationToken cancellation);
    Task<TokenResponse> LoginUser(LoginDto loginForm, CancellationToken cancellationToken);
    Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken);
    Task RevokeRefreshTokenAsync(string? refreshToken, CancellationToken cancellationToken);
}
