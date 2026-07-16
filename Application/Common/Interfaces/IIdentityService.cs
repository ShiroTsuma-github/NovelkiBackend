namespace Application.Common.Interfaces;

using DTOs.User;

public interface IIdentityService
{
    public Task<RegisterResponse> RegisterUser(RegisterDto registerForm, CancellationToken cancellation);
    public Task<TokenResponse> LoginUser(LoginDto loginForm, CancellationToken cancellationToken);
    public Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken);
    public Task RevokeRefreshTokenAsync(string? refreshToken, CancellationToken cancellationToken);
}
