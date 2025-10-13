using Microsoft.AspNetCore.Builder;

namespace Infrastructure.Middleware;

public static class ErrorHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseErrorHandlingMiddleware(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ErrorHandlingMiddleware>();
    }
}
