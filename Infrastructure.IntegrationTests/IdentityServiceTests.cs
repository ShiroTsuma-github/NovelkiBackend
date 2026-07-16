using Application.Common;
using Application.Common.DTOs.User;
using Application.Common.Interfaces;
using Application.Common.Models;
using Infrastructure.Authentication;
using Infrastructure.Contexts;
using Infrastructure.Identity;
using Domain.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.IntegrationTests;

using Domain.Entities;

public class IdentityServiceTests
{
    [Fact]
    public async Task RegisterUser_ShouldCreateUserAndRejectDuplicateUsernameAndEmail()
    {
        using var host = new IdentityTestHost();
        IdentityService service = host.GetRequiredService<IdentityService>();

        RegisterResponse created =
            await service.RegisterUser(new RegisterDto("new-reader", "new-reader@example.com", "Strong1!"),
                CancellationToken.None);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("new-reader", created.Name);
        await Assert.ThrowsAsync<UsernameTakenException>(() =>
            service.RegisterUser(new RegisterDto("new-reader", "other@example.com", "Strong1!"),
                CancellationToken.None));
        await Assert.ThrowsAsync<EmailInUseException>(() =>
            service.RegisterUser(new RegisterDto("other-reader", "new-reader@example.com", "Strong1!"),
                CancellationToken.None));
    }

    [Fact]
    public async Task RegisterUser_ShouldSurfaceIdentityErrors()
    {
        using var host = new IdentityTestHost();
        IdentityService service = host.GetRequiredService<IdentityService>();

        IdentityOperationFailedException ex = await Assert.ThrowsAsync<IdentityOperationFailedException>(() =>
            service.RegisterUser(new RegisterDto("weak-reader", "weak@example.com", "x"), CancellationToken.None));

        Assert.NotEmpty(ex.Errors);
    }

    [Fact]
    public async Task LoginUser_ShouldIssueAccessAndRefreshToken()
    {
        using var host = new IdentityTestHost();
        IdentityService service = host.GetRequiredService<IdentityService>();
        await service.RegisterUser(new RegisterDto("login-reader", "login@example.com", "Strong1!"),
            CancellationToken.None);

        TokenResponse response =
            await service.LoginUser(new LoginDto("login-reader", null, "Strong1!"), CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
        Assert.True(response.RefreshTokenExpiresAt > DateTimeOffset.UtcNow);
        await using ApplicationDbContext context = host.CreateContext();
        Assert.Single(context.RefreshTokens.Where(token => token.UserId == response.UserId));
    }

    [Fact]
    public async Task LoginUser_ShouldRejectMissingUserAndWrongPassword()
    {
        using var host = new IdentityTestHost();
        IdentityService service = host.GetRequiredService<IdentityService>();
        await service.RegisterUser(new RegisterDto("password-reader", "password@example.com", "Strong1!"),
            CancellationToken.None);

        await Assert.ThrowsAsync<EntityNotFoundException<User, string>>(() =>
            service.LoginUser(new LoginDto("missing-reader", null, "Strong1!"), CancellationToken.None));
        await Assert.ThrowsAsync<WrongPasswordException>(() =>
            service.LoginUser(new LoginDto("password-reader", null, "Wrong1!"), CancellationToken.None));
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldRotateRefreshToken()
    {
        using var host = new IdentityTestHost();
        IdentityService service = host.GetRequiredService<IdentityService>();
        await service.RegisterUser(new RegisterDto("refresh-reader", "refresh@example.com", "Strong1!"),
            CancellationToken.None);
        TokenResponse login = await service.LoginUser(new LoginDto(null, "refresh@example.com", "Strong1!"),
            CancellationToken.None);

        TokenResponse refreshed = await service.RefreshTokenAsync(login.RefreshToken, CancellationToken.None);

        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
        await using ApplicationDbContext context = host.CreateContext();
        var tokens = (await context.RefreshTokens
                .Where(token => token.UserId == login.UserId)
                .ToListAsync())
            .OrderBy(token => token.Created)
            .ToList();
        Assert.Equal(2, tokens.Count);
        Assert.Equal("Rotated", tokens[0].ReasonRevoked);
        Assert.NotNull(tokens[0].ReplacedByTokenHash);
        Assert.Null(tokens[1].RevokedAt);
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldRejectMissingInvalidAndExpiredTokens()
    {
        using var host = new IdentityTestHost();
        IdentityService service = host.GetRequiredService<IdentityService>();
        await service.RegisterUser(new RegisterDto("expired-reader", "expired@example.com", "Strong1!"),
            CancellationToken.None);
        TokenResponse login =
            await service.LoginUser(new LoginDto("expired-reader", null, "Strong1!"), CancellationToken.None);
        await using (ApplicationDbContext context = host.CreateContext())
        {
            RefreshToken stored = await context.RefreshTokens.SingleAsync(token => token.UserId == login.UserId);
            stored.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await context.SaveChangesAsync();
        }

        service = host.GetRequiredService<IdentityService>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RefreshTokenAsync("", CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RefreshTokenAsync("not-a-real-token", CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RefreshTokenAsync(login.RefreshToken, CancellationToken.None));
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_ShouldIgnoreBlankAndRevokeActiveToken()
    {
        using var host = new IdentityTestHost();
        IdentityService service = host.GetRequiredService<IdentityService>();
        await service.RegisterUser(new RegisterDto("logout-reader", "logout@example.com", "Strong1!"),
            CancellationToken.None);
        TokenResponse login =
            await service.LoginUser(new LoginDto("logout-reader", null, "Strong1!"), CancellationToken.None);

        await service.RevokeRefreshTokenAsync(null, CancellationToken.None);
        await service.RevokeRefreshTokenAsync(login.RefreshToken, CancellationToken.None);
        await service.RevokeRefreshTokenAsync(login.RefreshToken, CancellationToken.None);

        await using ApplicationDbContext context = host.CreateContext();
        RefreshToken stored = await context.RefreshTokens.SingleAsync(token => token.UserId == login.UserId);
        Assert.Equal("Logged out", stored.ReasonRevoked);
        Assert.NotNull(stored.RevokedAt);
    }

    private sealed class IdentityTestHost : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;

        public IdentityTestHost()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpContextAccessor();
            services.AddAuthentication();
            services.AddSingleton<IUser>(new TestUser());
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
            services
                .AddIdentityCore<User>(options =>
                {
                    options.User.RequireUniqueEmail = true;
                    options.Password.RequiredLength = 6;
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = true;
                })
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddSignInManager();

            services.AddSingleton<IOptions<JwtSettings>>(Options.Create(new JwtSettings
            {
                Key = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                Issuer = "NovelkiTests",
                Audience = "NovelkiTests"
            }));
            services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
            services.AddScoped<IdentityService>();

            _provider = services.BuildServiceProvider();
            using IServiceScope scope = _provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        }

        public T GetRequiredService<T>() where T : notnull
        {
            return _provider.CreateScope().ServiceProvider.GetRequiredService<T>();
        }

        public ApplicationDbContext CreateContext()
        {
            return _provider.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();
        }

        public void Dispose()
        {
            _provider.Dispose();
            _connection.Dispose();
        }
    }

    private sealed class TestUser : IUser
    {
        public Guid? Id => Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public Guid RequiredId => Id!.Value;
        public string? Email => "tests@example.com";
        public string? Username => "tests";
        public IEnumerable<string> Roles => [];
        public bool IsAuthenticated => true;
        public bool Valid => true;
    }
}
