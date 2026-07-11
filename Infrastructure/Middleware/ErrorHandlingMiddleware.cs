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
                statusCode = HttpStatusCode.Unauthorized;
                title = "Authentication Failed";
                detail = exception.Message;
                _logger.LogWarning("Incorrect Password");
                break;

            case UsernameTakenException:
                statusCode = HttpStatusCode.Conflict;
                title = "Conflict";
                detail = exception.Message;
                _logger.LogWarning("Username already exists");
                break;

            case EmailInUseException:
                statusCode = HttpStatusCode.Conflict;
                title = "Conflict";
                detail = exception.Message;
                _logger.LogWarning("Email already exists");
                break;

            case IdentityOperationFailedException identityException:
                statusCode = HttpStatusCode.BadRequest;
                title = "Identity operation failed.";
                detail = exception.Message;
                errors = new Dictionary<string, IReadOnlyCollection<string>>
                {
                    ["Identity"] = identityException.Errors
                };
                _logger.LogWarning("Identity operation failed");
                break;

            case ValidationException validationException:
                var validationErrors = validationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray()
                    );

                statusCode = HttpStatusCode.BadRequest;
                title = "One or more validation errors occurred.";
                detail = validationErrors
                    .SelectMany(entry => entry.Value.Select(message => $"{entry.Key}: {message}"))
                    .FirstOrDefault() ?? "The request contains invalid data.";
                errors = validationErrors;
                _logger.LogWarning("User validation error. Errors: {@ValidationErrors}", validationErrors);
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

            case EntityAlreadyExistsException<ContentType, Guid>:
                statusCode = HttpStatusCode.Conflict;
                title = "Conflict";
                detail = exception.Message;
                _logger.LogWarning("Type already exists");
                break;

            case EntityNotFoundException<ContentType, Guid>:
                statusCode = HttpStatusCode.NotFound;
                title = "Not Found";
                detail = exception.Message;
                _logger.LogWarning("Type not found");
                break;

            case EntityNotFoundException<Book, Guid>:
                statusCode = HttpStatusCode.NotFound;
                title = "Not Found";
                detail = exception.Message;
                _logger.LogWarning("Book not found");
                break;

            case EntityNotFoundException<BookCover, Guid>:
                statusCode = HttpStatusCode.NotFound;
                title = "Not Found";
                detail = exception.Message;
                _logger.LogWarning("Book cover not found");
                break;

            case EntityAlreadyExistsException<Book, Guid> bookConflict:
                statusCode = HttpStatusCode.Conflict;
                title = "Conflict";
                detail = $"A book named '{bookConflict.Name}' already exists.";
                _logger.LogWarning("Book already exists. ExistingId={ExistingId}", bookConflict.ExistingId);
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
