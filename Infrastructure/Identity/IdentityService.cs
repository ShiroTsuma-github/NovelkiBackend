namespace Infrastructure.Identity;

using Application.Common;
using Application.Common.DTOs.User;
using Application.Common.Models;
using Infrastructure.Authentication;

public class IdentityService : IIdentityService
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public IdentityService(UserManager<User> userManager, SignInManager<User> signInManager, IJwtTokenGenerator jwtTokenGenerator)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<TokenResponse> LoginUser(LoginDto login, CancellationToken cancellation)
    {
        var user = await _userManager.FindByNameAsync(login.username ?? "") ?? await _userManager.FindByEmailAsync(login.email ?? "");
        Guard.ThrowIfNotFound<User, string>(user, login.username ?? login.email ?? "No Identifier");

        var result = await _signInManager.CheckPasswordSignInAsync(user, login.password, false);

        if (!result.Succeeded) 
        {
            throw new WrongPasswordException();
        }
        var authUser = new AuthUser
        {
            Username = user.UserName,
            Email = user.Email,
            Id = user.Id,
            IsAuthenticated = true,
            Roles = await _userManager.GetRolesAsync(user),
            Valid = true
        };

        var tokenResponse = _jwtTokenGenerator.GenerateToken(authUser);
        if (tokenResponse == null)
        {
            throw new TokenGeneratorFailedException();
        }
        return tokenResponse;
    }

    public async Task<RegisterResponse> RegisterUser(RegisterDto register, CancellationToken cancellation)
    {
        var exists = await _userManager.FindByNameAsync(register.username);
        if (exists != null) throw new UsernameTakenException(register.username);
        exists = await _userManager.FindByEmailAsync(register.email);
        if (exists != null) throw new EmailInUseException(register.email);

        var user = new User { UserName = register.username, Email = register.email };
        var result = await _userManager.CreateAsync(user, register.password);

        return new RegisterResponse
        {
            Id = user.Id,
            Name = register.username
        };
    }
}
