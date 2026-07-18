using Application.Common;
using Application.Common.Interfaces;
using Infrastructure.Middleware;
using Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public class AccountAbuseGuardTests
{
    [Fact]
    public async Task BlockAsync_ShouldBeVisibleToAnotherGuardUsingTheSameDistributedCache()
    {
        var cache = CreateCache();
        var options = Options.Create(new BookImportSecurityOptions
        {
            SuspiciousAccountBlockDuration = TimeSpan.FromHours(24)
        });
        var user = new TestUser(Guid.NewGuid());
        var firstGuard = CreateGuard(cache, options);
        var secondGuard = CreateGuard(cache, options);

        var blockedUntil = await firstGuard.BlockAsync(user, "archive-path-traversal", CancellationToken.None);
        var exception = await Assert.ThrowsAsync<AccountTemporarilyBlockedException>(() =>
            secondGuard.ThrowIfBlockedAsync(user, CancellationToken.None));

        Assert.Equal(blockedUntil.ToUnixTimeSeconds(), exception.BlockedUntilUtc.ToUnixTimeSeconds());
        Assert.True(exception.BlockedUntilUtc > DateTimeOffset.UtcNow.AddHours(23));
    }

    [Fact]
    public async Task Admin_ShouldNeverBeBlockedByTheAbuseGuard()
    {
        var cache = CreateCache();
        var options = Options.Create(new BookImportSecurityOptions());
        var admin = new TestUser(Guid.NewGuid(), [AuthorizationRoles.Admin]);
        var guard = CreateGuard(cache, options);

        await guard.BlockAsync(admin, "extreme-compression-ratio", CancellationToken.None);
        await guard.ThrowIfBlockedAsync(admin, CancellationToken.None);
    }

    [Fact]
    public async Task AccountBlockMiddleware_ShouldRejectAllRequestsFromBlockedAuthenticatedUser()
    {
        var cache = CreateCache();
        var options = Options.Create(new BookImportSecurityOptions());
        var user = new TestUser(Guid.NewGuid());
        var guard = CreateGuard(cache, options);
        await guard.BlockAsync(user, "archive-path-traversal", CancellationToken.None);
        var nextCalled = false;
        var middleware = new AccountBlockMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<AccountTemporarilyBlockedException>(() =>
            middleware.InvokeAsync(new DefaultHttpContext(), user, guard));

        Assert.False(nextCalled);
    }

    private static IDistributedCache CreateCache()
    {
        return new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    }

    private static AccountAbuseGuard CreateGuard(IDistributedCache cache,
        IOptions<BookImportSecurityOptions> options)
    {
        return new AccountAbuseGuard(cache, options, NullLogger<AccountAbuseGuard>.Instance);
    }

    private sealed class TestUser : IUser
    {
        public TestUser(Guid id, IEnumerable<string>? roles = null)
        {
            Id = id;
            Roles = roles ?? [];
        }

        public Guid? Id { get; }
        public Guid RequiredId => Id!.Value;
        public string? Email => "reader@example.com";
        public string? Username => "reader";
        public IEnumerable<string> Roles { get; }

        public bool IsAuthenticated => true;
        public bool Valid => true;
    }
}
