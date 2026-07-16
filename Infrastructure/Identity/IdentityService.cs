namespace Infrastructure.Identity;

using Application.Common;
using Application.Common.DTOs.User;
using Application.Common.Models;
using Authentication;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Contexts;

public class IdentityService : IIdentityService
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ApplicationDbContext _context;

    public IdentityService(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IJwtTokenGenerator jwtTokenGenerator,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _context = context;
    }

    public async Task<TokenResponse> LoginUser(LoginDto login, CancellationToken cancellation)
    {
        string identifier = login.username ?? login.email ?? "No Identifier";
        User? user = await _userManager.FindByNameAsync(login.username ?? "") ??
                     await _userManager.FindByEmailAsync(login.email ?? "");
        if (user == null)
        {
            throw new EntityNotFoundException<User, string>(identifier);
        }

        SignInResult result = await _signInManager.CheckPasswordSignInAsync(user, login.password, false);

        if (!result.Succeeded)
        {
            throw new WrongPasswordException();
        }

        var authUser = new AuthUser
        {
            Username = user.UserName,
            Email = user.Email,
            Id = user.Id,
            CreatedAt = user.CreatedAt,
            IsAuthenticated = true,
            Roles = await _userManager.GetRolesAsync(user),
            Valid = true
        };

        TokenResponse? tokenResponse = _jwtTokenGenerator.GenerateToken(authUser);
        if (tokenResponse == null)
        {
            throw new TokenGeneratorFailedException();
        }

        (string Token, DateTimeOffset ExpiresAt) refreshToken = await IssueRefreshTokenAsync(user.Id, cancellation);
        return tokenResponse with { RefreshToken = refreshToken.Token, RefreshTokenExpiresAt = refreshToken.ExpiresAt };
    }

    public async Task<RegisterResponse> RegisterUser(RegisterDto register, CancellationToken cancellation)
    {
        User? exists = await _userManager.FindByNameAsync(register.username);
        if (exists != null)
        {
            throw new UsernameTakenException(register.username);
        }

        exists = await _userManager.FindByEmailAsync(register.email);
        if (exists != null)
        {
            throw new EmailInUseException(register.email);
        }

        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        var user = new User { UserName = register.username, Email = register.email, CreatedAt = createdAt };
        IdentityResult result = await _userManager.CreateAsync(user, register.password);
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

        string hashedToken = HashToken(refreshToken);
        RefreshToken? storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == hashedToken, cancellationToken);
        if (storedToken == null || !storedToken.IsActive)
        {
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");
        }

        User user = await _userManager.FindByIdAsync(storedToken.UserId.ToString())
                    ?? throw new UnauthorizedAccessException("Refresh token user no longer exists.");

        storedToken.RevokedAt = DateTimeOffset.UtcNow;
        storedToken.ReasonRevoked = "Rotated";

        (string Token, DateTimeOffset ExpiresAt) nextRefreshToken = CreateRefreshToken(user.Id);
        storedToken.ReplacedByTokenHash = HashToken(nextRefreshToken.Token);
        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id, TokenHash = storedToken.ReplacedByTokenHash, ExpiresAt = nextRefreshToken.ExpiresAt
        });

        var authUser = new AuthUser
        {
            Username = user.UserName,
            Email = user.Email,
            Id = user.Id,
            CreatedAt = user.CreatedAt,
            IsAuthenticated = true,
            Roles = await _userManager.GetRolesAsync(user),
            Valid = true
        };

        TokenResponse accessToken = _jwtTokenGenerator.GenerateToken(authUser)
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

        string hashedToken = HashToken(refreshToken);
        RefreshToken? storedToken = await _context.RefreshTokens
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
        (string Token, DateTimeOffset ExpiresAt) nextRefreshToken = CreateRefreshToken(userId);
        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId, TokenHash = HashToken(nextRefreshToken.Token), ExpiresAt = nextRefreshToken.ExpiresAt
        });
        await _context.SaveChangesAsync(cancellationToken);
        return nextRefreshToken;
    }

    private static (string Token, DateTimeOffset ExpiresAt) CreateRefreshToken(Guid userId)
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(64);
        string token = Convert.ToBase64String(bytes)
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
