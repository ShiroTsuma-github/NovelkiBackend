namespace Infrastructure.Identity;

using System.Security.Cryptography;
using System.Text;
using Application.Common.DTOs.User;
using Application.Common.Models;
using Authentication;
using Microsoft.Extensions.Logging;
using Services;

public class IdentityService : IIdentityService
{
    private const string DummyPassword = "Dummy-password-used-only-for-timing-9!";
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
    private static readonly User DummyUser = new() { UserName = "timing-placeholder" };
    private static readonly string DummyPasswordHash =
        new PasswordHasher<User>().HashPassword(DummyUser, DummyPassword);

    private readonly AccountAbuseGuard _accountAbuseGuard;
    private readonly ApplicationDbContext _context;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ILogger<IdentityService> _logger;
    private readonly UserManager<User> _userManager;

    public IdentityService(
        UserManager<User> userManager,
        IJwtTokenGenerator jwtTokenGenerator,
        ApplicationDbContext context,
        AccountAbuseGuard accountAbuseGuard,
        ILogger<IdentityService> logger)
    {
        _userManager = userManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _context = context;
        _accountAbuseGuard = accountAbuseGuard;
        _logger = logger;
    }

    public async Task<TokenResponse> LoginUser(LoginDto login, CancellationToken cancellation)
    {
        var user = !string.IsNullOrWhiteSpace(login.username)
            ? await _userManager.FindByNameAsync(login.username)
            : await _userManager.FindByEmailAsync(login.email ?? string.Empty);
        if (user == null)
        {
            _userManager.PasswordHasher.VerifyHashedPassword(DummyUser, DummyPasswordHash, login.password);
            throw new AuthenticationFailedException();
        }

        if (!await _userManager.CheckPasswordAsync(user, login.password))
        {
            throw new AuthenticationFailedException();
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
        if (storedToken == null)
        {
            throw new UnauthorizedAccessException();
        }

        if (!storedToken.IsActive)
        {
            if (storedToken.ReplacedByTokenHash != null)
            {
                var now = DateTimeOffset.UtcNow;
                var candidateTokens = await _context.RefreshTokens
                    .Where(token => token.UserId == storedToken.UserId &&
                                    token.RevokedAt == null)
                    .ToListAsync(cancellationToken);
                var activeTokens = candidateTokens.Where(token => token.ExpiresAt > now);
                foreach (var activeToken in activeTokens)
                {
                    activeToken.RevokedAt = now;
                    activeToken.ReasonRevoked = "Refresh token reuse detected";
                }

                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(
                    "Refresh token reuse detected; active token family revoked. UserId={UserId}",
                    storedToken.UserId);
            }

            throw new UnauthorizedAccessException();
        }

        var user = await _userManager.FindByIdAsync(storedToken.UserId.ToString())
                   ?? throw new UnauthorizedAccessException();

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
