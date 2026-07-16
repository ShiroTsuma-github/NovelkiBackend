using Application.Common.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

namespace Infrastructure.Authentication;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _configuration;

    public JwtTokenGenerator(IOptions<JwtSettings> jwtSettings)
    {
        _configuration = jwtSettings.Value;
    }

    public TokenResponse? GenerateToken(IUser user)
    {
        if (!user.IsAuthenticated)
        {
            return null;
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.RequiredId.ToString()),
            new(ClaimTypes.Name, user.Username!),
            new(ClaimTypes.Email, user.Email!),
            new(JwtRegisteredClaimNames.Sub, user.RequiredId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!)
        };

        if (user.CreatedAt is { } createdAt)
        {
            claims.Add(new Claim("created_at", createdAt.ToString("O")));
        }

        foreach (string role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            _configuration.Issuer,
            _configuration.Audience,
            claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new TokenResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            RefreshToken = string.Empty,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            RefreshTokenExpiresAt = DateTimeOffset.UtcNow,
            UserId = user.RequiredId,
            CreatedAt = user.CreatedAt ?? DateTimeOffset.UtcNow
        };
    }
}
