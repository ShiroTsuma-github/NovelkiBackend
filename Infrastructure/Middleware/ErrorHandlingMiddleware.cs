using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using ValidationException = FluentValidation.ValidationException;

namespace Infrastructure.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(httpContext, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        var statusCode = HttpStatusCode.InternalServerError;
        var title = "An unexpected error occurred.";
        var detail = "Please try again later.";
        object? errors = null;

        switch(exception)
        {
            case EntityNotFoundException<User, string>:
                statusCode = HttpStatusCode.NotFound;
                title = "Authentication Failed";
                detail = exception.Message;
                _logger.LogWarning("User Not Found");
                break;

            case WrongPasswordException:
                statusCode = HttpStatusCode.NotFound;
                title = "Authentication Failed";
                detail = exception.Message;
                _logger.LogWarning("Incorrect Password");
                break;

            case ValidationException validationException:
                statusCode = HttpStatusCode.BadRequest;
                title = "One or more validation errors occurred.";
                detail = "The request contains invalid data.";
                errors = validationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray()
                    );
                _logger.LogWarning("User validation error");
                break;

            case EntityAlreadyExistsException<Genre, Guid>:
                statusCode = HttpStatusCode.Conflict;
                title = "Conflict";
                detail = exception.Message;
                _logger.LogWarning("Genre already exists");
                break;

            case EntityNotFoundException<Genre, Guid>:
                statusCode = HttpStatusCode.NotFound;
                title = "Not Found";
                detail = exception.Message;
                _logger.LogWarning("Genre not found");
                break;

            case EntityAlreadyExistsException<Status, Guid>:
                statusCode = HttpStatusCode.Conflict;
                title = "Conflict";
                detail = exception.Message;
                _logger.LogWarning("Status already exists");
                break;

            case EntityNotFoundException<Status, Guid>:
                statusCode = HttpStatusCode.NotFound;
                title = "Not Found";
                detail = exception.Message;
                _logger.LogWarning("Status not found");
                break;

            default:
                title = "Internal Server Error";
                detail = "An internal server error occurred. Please check the logs.";
                _logger.LogError(exception, "Server (500) unhandled error: {Message}", exception.Message);
                break;
        };

        context.Response.StatusCode = (int)statusCode;
        var errorResponse = new
        {
            type = exception.GetType().Name,
            title,
            status = context.Response.StatusCode,
            detail,
            instance = context.Request.Path,
            errors
        };

        var json = JsonSerializer.Serialize(errorResponse);
        await context.Response.WriteAsync(json);
    }
}