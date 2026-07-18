namespace Infrastructure.Identity;

using System.Security.Cryptography;
using System.Text;
using Application.Common.DTOs.User;
using Application.Common.Models;
using Authentication;
using Services;

public class IdentityService : IIdentityService
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
    private readonly AccountAbuseGuard _accountAbuseGuard;
    private readonly ApplicationDbContext _context;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly SignInManager<User> _signInManager;
    private readonly UserManager<User> _userManager;

    public IdentityService(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IJwtTokenGenerator jwtTokenGenerator,
        ApplicationDbContext context,
        AccountAbuseGuard accountAbuseGuard)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _context = context;
        _accountAbuseGuard = accountAbuseGuard;
    }

    public async Task<TokenResponse> LoginUser(LoginDto login, CancellationToken cancellation)
    {
        var identifier = login.username ?? login.email ?? "No Identifier";
        var user = await _userManager.FindByNameAsync(login.username ?? "") ??
                   await _userManager.FindByEmailAsync(login.email ?? "");
        if (user == null)
        {
            throw new EntityNotFoundException<User, string>(identifier);
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, login.password, false);

        if (!result.Succeeded)
        {
            throw new WrongPasswordException();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var authUser = new AuthUser
        {
            Username = user.UserName,
            Email = user.Email,
            Id = user.Id,
            CreatedAt = user.CreatedAt,
            IsAuthenticated = true,
            Roles = roles,
            Valid = true
        };
        await _accountAbuseGuard.ThrowIfBlockedAsync(authUser, cancellation);

        var tokenResponse = _jwtTokenGenerator.GenerateToken(authUser);
        if (tokenResponse == null)
        {
            throw new TokenGeneratorFailedException();
        }

        var refreshToken = await IssueRefreshTokenAsync(user.Id, cancellation);
        return tokenResponse with { RefreshToken = refreshToken.Token, RefreshTokenExpiresAt = refreshToken.ExpiresAt };
    }

    public async Task<RegisterResponse> RegisterUser(RegisterDto register, CancellationToken cancellation)
    {
        var exists = await _userManager.FindByNameAsync(register.username);
        if (exists != null)
        {
            throw new UsernameTakenException(register.username);
        }

        exists = await _userManager.FindByEmailAsync(register.email);
        if (exists != null)
        {
            throw new EmailInUseException(register.email);
        }

        var createdAt = DateTimeOffset.UtcNow;
        var user = new User { UserName = register.username, Email = register.email, CreatedAt = createdAt };
        var result = await _userManager.CreateAsync(user, register.password);
        if (!result.Succeeded)
        {
            throw new IdentityOperationFailedException(result.Errors.Select(e => e.Description));
        }

        return new RegisterResponse { Id = user.Id, Name = register.username, CreatedAt = createdAt };
    }

    public async Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedAccessException("Refresh token is required.");
        }

        var hashedToken = HashToken(refreshToken);
        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == hashedToken, cancellationToken);
        if (storedToken == null || !storedToken.IsActive)
        {
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");
        }

        var user = await _userManager.FindByIdAsync(storedToken.UserId.ToString())
                   ?? throw new UnauthorizedAccessException("Refresh token user no longer exists.");

        var roles = await _userManager.GetRolesAsync(user);
        var authUser = new AuthUser
        {
            Username = user.UserName,
            Email = user.Email,
            Id = user.Id,
            CreatedAt = user.CreatedAt,
            IsAuthenticated = true,
            Roles = roles,
            Valid = true
        };
        await _accountAbuseGuard.ThrowIfBlockedAsync(authUser, cancellationToken);

        storedToken.RevokedAt = DateTimeOffset.UtcNow;
        storedToken.ReasonRevoked = "Rotated";

        var nextRefreshToken = CreateRefreshToken(user.Id);
        storedToken.ReplacedByTokenHash = HashToken(nextRefreshToken.Token);
        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id, TokenHash = storedToken.ReplacedByTokenHash, ExpiresAt = nextRefreshToken.ExpiresAt
        });

        var accessToken = _jwtTokenGenerator.GenerateToken(authUser)
                          ?? throw new TokenGeneratorFailedException();

        await _context.SaveChangesAsync(cancellationToken);

        return accessToken with
        {
            RefreshToken = nextRefreshToken.Token, RefreshTokenExpiresAt = nextRefreshToken.ExpiresAt
        };
    }

    public async Task RevokeRefreshTokenAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hashedToken = HashToken(refreshToken);
        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == hashedToken, cancellationToken);
        if (storedToken == null || storedToken.RevokedAt != null)
        {
            return;
        }

        storedToken.RevokedAt = DateTimeOffset.UtcNow;
        storedToken.ReasonRevoked = "Logged out";
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<(string Token, DateTimeOffset ExpiresAt)> IssueRefreshTokenAsync(Guid userId,
        CancellationToken cancellationToken)
    {
        var nextRefreshToken = CreateRefreshToken(userId);
        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId, TokenHash = HashToken(nextRefreshToken.Token), ExpiresAt = nextRefreshToken.ExpiresAt
        });
        await _context.SaveChangesAsync(cancellationToken);
        return nextRefreshToken;
    }

    private static (string Token, DateTimeOffset ExpiresAt) CreateRefreshToken(Guid userId)
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return ($"{userId:N}.{token}", DateTimeOffset.UtcNow.Add(RefreshTokenLifetime));
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
