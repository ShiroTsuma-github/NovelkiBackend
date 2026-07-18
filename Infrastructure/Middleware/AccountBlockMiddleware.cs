namespace Infrastructure.Middleware;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Services;

public sealed class AccountBlockMiddleware
{
    private readonly RequestDelegate _next;

    public AccountBlockMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IUser user, AccountAbuseGuard abuseGuard)
    {
        if (user.IsAuthenticated)
        {
            await abuseGuard.ThrowIfBlockedAsync(user, context.RequestAborted);
        }

        await _next(context);
    }
}

public static class AccountBlockMiddlewareExtensions
{
    public static IApplicationBuilder UseAccountBlock(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AccountBlockMiddleware>();
    }
}
