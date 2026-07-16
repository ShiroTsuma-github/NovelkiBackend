using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Application.Common.Interfaces;
using Domain.Entities;
using Infrastructure.Contexts;
using Infrastructure.Identity;
using Infrastructure.IntegrationTests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.IntegrationTests;

using Microsoft.Extensions.Primitives;

public class SecurityBaselineEndpointTests
{
    private static readonly Guid UserAId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserBId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task ProtectedBookEndpoint_ShouldReturnUnauthorizedWithoutToken()
    {
        await using var factory = new SecurityApiFactory();
        using var client =
            factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/v1/book");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_ShouldReturnForbiddenForNonAdminUser()
    {
        await using var factory = new SecurityApiFactory();
        using var client = factory.CreateAuthenticatedClient(UserAId);

        var response = await client.GetAsync("/api/v1/admin/books");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserBookEndpoints_ShouldNotAllowCrossUserReadOrDelete()
    {
        await using var factory = new SecurityApiFactory();
        var otherUserBookId = await factory.SeedBookAsync(UserBId, "Other User Book");
        using var client = factory.CreateAuthenticatedClient(UserAId);

        var readResponse = await client.GetAsync($"/api/v1/book/{otherUserBookId}");
        var deleteResponse = await client.DeleteAsync($"/api/v1/book/{otherUserBookId}");

        Assert.Equal(HttpStatusCode.NotFound, readResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
        Assert.True(await factory.BookExistsAsync(otherUserBookId));
    }

    [Fact]
    public async Task DestructiveAdminPurge_ShouldRequireAdminRole()
    {
        await using var factory = new SecurityApiFactory();
        using var client = factory.CreateAuthenticatedClient(UserAId);

        var response = await client.DeleteAsync($"/api/v1/admin/books/owner/{UserBId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ApiResponses_ShouldIncludeBaselineSecurityHeaders()
    {
        await using var factory = new SecurityApiFactory();
        using var client =
            factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/health/live");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {(int)response.StatusCode}: {body}");
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("camera=(), microphone=(), geolocation=()",
            response.Headers.GetValues("Permissions-Policy").Single());
        Assert.Equal("same-origin", response.Headers.GetValues("Cross-Origin-Opener-Policy").Single());
    }

    [Fact]
    public async Task Cors_ShouldNotAllowArbitraryOrigins_WhenNoOriginAllowlistIsConfiguredOutsideDevelopment()
    {
        await using var factory = new SecurityApiFactory();
        using var client =
            factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", "https://evil.example");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {(int)response.StatusCode}: {body}");
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task AccountLogin_ShouldRateLimitRepeatedRequestsByRemoteIp()
    {
        await using var factory = new SecurityApiFactory();
        using var client =
            factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var firstResponse = await client.PostAsJsonAsync("/api/v1/account/login", new { });
        var secondResponse = await client.PostAsJsonAsync("/api/v1/account/login", new { });

        Assert.NotEqual(HttpStatusCode.TooManyRequests, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondResponse.StatusCode);
    }

    [Fact]
    public async Task ExpensiveImportEndpoint_ShouldRateLimitNonAdminUsers()
    {
        await using var factory = new SecurityApiFactory();
        await factory.EnsureCreatedAsync();
        using var client = factory.CreateAuthenticatedClient(UserAId);

        var firstResponse =
            await client.PostAsync("/api/v1/book/import/sessions", new MultipartFormDataContent());
        var secondResponse =
            await client.PostAsync("/api/v1/book/import/sessions", new MultipartFormDataContent());

        Assert.NotEqual(HttpStatusCode.TooManyRequests, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondResponse.StatusCode);
    }

    [Fact]
    public async Task ExpensiveImportEndpoint_ShouldNotRateLimitAdminUsers()
    {
        await using var factory = new SecurityApiFactory();
        await factory.EnsureCreatedAsync();
        using var client = factory.CreateAuthenticatedClient(UserAId, "Admin");

        var firstResponse =
            await client.PostAsync("/api/v1/book/import/sessions", new MultipartFormDataContent());
        var secondResponse =
            await client.PostAsync("/api/v1/book/import/sessions", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
    }

    private sealed class SecurityApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("DataSource=:memory:");

        private readonly ServiceProvider _sqliteServices = new ServiceCollection()
            .AddEntityFrameworkSqlite()
            .BuildServiceProvider();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _connection.Open();
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DB"] = "Host=localhost;Database=test;Username=test;Password=test",
                    ["ConnectionStrings:Redis"] = " ",
                    ["Jwt:Key"] = "test-test-test-test-test-test-test-test-test-test-test-test",
                    ["Jwt:Issuer"] = "NovelkiBackend.Tests",
                    ["Jwt:Audience"] = "NovelkiBackend.Tests",
                    ["Database:AutoMigrate"] = "false",
                    ["RateLimiting:Account:PermitLimit"] = "1",
                    ["RateLimiting:Account:WindowSeconds"] = "60",
                    ["RateLimiting:Expensive:PermitLimit"] = "1",
                    ["RateLimiting:Expensive:WindowSeconds"] = "60",
                    ["BookCovers:S3:Endpoint"] = "",
                    ["BookCovers:S3:AccessKey"] = "",
                    ["BookCovers:S3:SecretKey"] = "",
                    ["BookCovers:S3:Bucket"] = ""
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IBookListCacheInvalidator>();
                services.RemoveAll<IDistributedCache>();
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseSqlite(_connection);
                    options.UseInternalServiceProvider(_sqliteServices);
                    options.EnableSensitiveDataLogging();
                });
                services.AddDistributedMemoryCache();
                services.AddScoped<IBookListCacheInvalidator, NoopBookListCacheInvalidator>();
                services
                    .AddAuthentication(TestAuthHandler.AuthenticationScheme)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.AuthenticationScheme,
                        _ => { });
                services.Configure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                    options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
                    options.DefaultScheme = TestAuthHandler.AuthenticationScheme;
                });
            });
        }

        public HttpClient CreateAuthenticatedClient(Guid userId, params string[] roles)
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(TestAuthHandler.AuthenticationScheme);
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
            if (roles.Length > 0)
            {
                client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(",", roles));
            }

            return client;
        }

        public async Task<Guid> SeedBookAsync(Guid ownerId, string title)
        {
            await EnsureCreatedAsync();
            await using var scope = Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var book = TestData.Book(ownerId, title);
            context.Books.Add(book);
            await context.SaveChangesAsync();
            return book.Id;
        }

        public async Task<bool> BookExistsAsync(Guid bookId)
        {
            await using var scope = Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.Books.AnyAsync(book => book.Id == bookId);
        }

        public async Task EnsureCreatedAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureCreatedAsync();

            foreach (var userId in new[] { UserAId, UserBId })
            {
                if (!await context.Users.AnyAsync(user => user.Id == userId))
                {
                    context.Users.Add(new User
                    {
                        Id = userId,
                        UserName = $"reader-{userId:N}",
                        NormalizedUserName = $"READER-{userId:N}".ToUpperInvariant(),
                        Email = $"{userId:N}@example.com",
                        NormalizedEmail = $"{userId:N}@example.com".ToUpperInvariant()
                    });
                }
            }

            await context.SaveChangesAsync();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _connection.Dispose();
                _sqliteServices.Dispose();
            }
        }
    }

    private sealed class NoopBookListCacheInvalidator : IBookListCacheInvalidator
    {
        public Task InvalidateBooksAsync(Guid ownerId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string AuthenticationScheme = "Test";
        public const string UserIdHeader = "X-Test-UserId";
        public const string RolesHeader = "X-Test-Roles";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var authorization) ||
                !authorization.ToString().StartsWith(AuthenticationScheme, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            if (!Guid.TryParse(Request.Headers[UserIdHeader].FirstOrDefault(), out var userId))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing test user id."));
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Name, $"reader-{userId:N}"),
                new(ClaimTypes.Email, $"{userId:N}@example.com")
            };
            var roles = Request.Headers[RolesHeader]
                .SelectMany(value =>
                    value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ??
                    Array.Empty<string>());
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var identity = new ClaimsIdentity(claims, AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, AuthenticationScheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
