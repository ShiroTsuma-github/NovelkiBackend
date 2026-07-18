using Application.Common;
using Application.Common.Interfaces;
using System.Text;
using Infrastructure.Middleware;
using Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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

    [Fact]
    public async Task SuspiciousRequestMiddleware_ShouldBlockRepeatedlyEncodedPathTraversal()
    {
        var user = new TestUser(Guid.NewGuid());
        var guard = CreateGuard(CreateCache(), Options.Create(new BookImportSecurityOptions()));
        var context = new DefaultHttpContext();
        context.Features.Get<IHttpRequestFeature>()!.RawTarget = "/api/books/%252e%252e%252fadmin";
        var nextCalled = false;
        var middleware = new SuspiciousRequestMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<AccountTemporarilyBlockedException>(() =>
            middleware.InvokeAsync(context, user, guard));

        Assert.False(nextCalled);
        await Assert.ThrowsAsync<AccountTemporarilyBlockedException>(() =>
            guard.ThrowIfBlockedAsync(user, CancellationToken.None));
    }

    [Fact]
    public async Task SuspiciousRequestMiddleware_ShouldBlockStrongSqlInjectionQuery()
    {
        var user = new TestUser(Guid.NewGuid());
        var guard = CreateGuard(CreateCache(), Options.Create(new BookImportSecurityOptions()));
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?search=%27%20OR%201%3D1%20--");
        var middleware = new SuspiciousRequestMiddleware(_ => Task.CompletedTask);

        await Assert.ThrowsAsync<AccountTemporarilyBlockedException>(() =>
            middleware.InvokeAsync(context, user, guard));
    }

    [Fact]
    public async Task SuspiciousRequestMiddleware_ShouldBlockScriptInjectionAndPreserveRequestBody()
    {
        var user = new TestUser(Guid.NewGuid());
        var guard = CreateGuard(CreateCache(), Options.Create(new BookImportSecurityOptions()));
        var body = """{"description":"<img src=x onerror=alert(1)>"}""";
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = Encoding.UTF8.GetByteCount(body);
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        var middleware = new SuspiciousRequestMiddleware(_ => Task.CompletedTask);

        await Assert.ThrowsAsync<AccountTemporarilyBlockedException>(() =>
            middleware.InvokeAsync(context, user, guard));

        Assert.Equal(0, context.Request.Body.Position);
    }

    [Fact]
    public async Task SuspiciousRequestMiddleware_ShouldAllowOrdinarySearchAndText()
    {
        var user = new TestUser(Guid.NewGuid());
        var guard = CreateGuard(CreateCache(), Options.Create(new BookImportSecurityOptions()));
        var body = """{"description":"A script about selecting books; ../ is shown once."}""";
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/book";
        context.Request.QueryString = new QueryString("?search=type%3Amanga&note=../");
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = Encoding.UTF8.GetByteCount(body);
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        var nextCalled = false;
        var middleware = new SuspiciousRequestMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, user, guard);

        Assert.True(nextCalled);
        Assert.Equal(0, context.Request.Body.Position);
    }

    [Fact]
    public async Task SuspiciousRequestMiddleware_ShouldNotInspectAdministratorRequests()
    {
        var admin = new TestUser(Guid.NewGuid(), [AuthorizationRoles.Admin]);
        var guard = CreateGuard(CreateCache(), Options.Create(new BookImportSecurityOptions()));
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?search=%27%20OR%201%3D1%20--");
        var nextCalled = false;
        var middleware = new SuspiciousRequestMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, admin, guard);

        Assert.True(nextCalled);
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
