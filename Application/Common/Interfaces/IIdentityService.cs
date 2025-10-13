namespace Application.Common.Interfaces;

using Application.Common.DTOs.User;

public interface IIdentityService
{
    Task<RegisterResponse> RegisterUser(RegisterDto registerForm, CancellationToken cancellation);
    Task<TokenResponse> LoginUser(LoginDto loginForm, CancellationToken cancellationToken);
}
