using ValidationException = FluentValidation.ValidationException;

namespace Infrastructure.Middleware;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Services;

public class ErrorHandlingMiddleware
{
    private const string ConflictTitle = "Conflict";
    private const string NotFoundTitle = "Not Found";
    private const string AuthenticationFailedTitle = "Authentication Failed";
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    private readonly RequestDelegate _next;

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

        switch (exception)
        {
            case EntityNotFoundException<User, string>:
                statusCode = HttpStatusCode.NotFound;
                title = AuthenticationFailedTitle;
                detail = exception.Message;
                _logger.LogWarning("User Not Found");
                break;

            case WrongPasswordException:
                statusCode = HttpStatusCode.Unauthorized;
                title = AuthenticationFailedTitle;
                detail = exception.Message;
                _logger.LogWarning("Incorrect Password");
                break;

            case UsernameTakenException:
                statusCode = HttpStatusCode.Conflict;
                title = ConflictTitle;
                detail = exception.Message;
                errors = new Dictionary<string, IReadOnlyCollection<string>>
                {
                    ["Username"] = new[] { exception.Message }
                };
                _logger.LogWarning("Username already exists");
                break;

            case EmailInUseException:
                statusCode = HttpStatusCode.Conflict;
                title = ConflictTitle;
                detail = exception.Message;
                errors = new Dictionary<string, IReadOnlyCollection<string>>
                {
                    ["Email"] = new[] { exception.Message }
                };
                _logger.LogWarning("Email already exists");
                break;

            case IdentityOperationFailedException identityException:
                statusCode = HttpStatusCode.BadRequest;
                title = IdentityOperationFailedException.DefaultMessage;
                detail = exception.Message;
                errors = new Dictionary<string, IReadOnlyCollection<string>>
                {
                    ["Identity"] = identityException.Errors
                };
                _logger.LogWarning("Identity operation failed");
                break;

            case ValidationException validationException:
                var validationErrors = validationException.Errors
                    .Where(error => !string.IsNullOrWhiteSpace(error.ErrorMessage))
                    .GroupBy(error => string.IsNullOrWhiteSpace(error.PropertyName) ? "General" : error.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).Distinct().ToArray()
                    );

                if (validationErrors.Count == 0)
                {
                    var message = string.IsNullOrWhiteSpace(validationException.Message)
                        ? "The request contains invalid data."
                        : validationException.Message;
                    validationErrors["General"] = [message];
                }

                statusCode = HttpStatusCode.BadRequest;
                title = "One or more validation errors occurred.";
                detail = validationErrors
                    .SelectMany(entry => entry.Value.Select(message =>
                        entry.Key == "General" ? message : $"{entry.Key}: {message}"))
                    .FirstOrDefault() ?? "The request contains invalid data.";
                errors = validationErrors;
                _logger.LogWarning(
                    "User validation error for {Method} {Path}. Detail={ValidationDetail} Errors={@ValidationErrors} TraceId={TraceId}",
                    context.Request.Method,
                    context.Request.Path,
                    detail,
                    validationErrors,
                    context.TraceIdentifier);
                break;

            case AccountTemporarilyBlockedException blockedException:
                statusCode = HttpStatusCode.TooManyRequests;
                title = "Account Temporarily Blocked";
                detail = blockedException.Message;
                errors = new Dictionary<string, IReadOnlyCollection<string>>
                {
                    ["General"] = new[] { blockedException.Message }
                };
                var remainingSeconds = Math.Max(1,
                    (long)Math.Ceiling((blockedException.BlockedUntilUtc - DateTimeOffset.UtcNow).TotalSeconds));
                context.Response.Headers.RetryAfter = remainingSeconds.ToString();
                _logger.LogWarning(
                    "Blocked account request rejected for {Method} {Path}. BlockedUntilUtc={BlockedUntilUtc} TraceId={TraceId}",
                    context.Request.Method, context.Request.Path, blockedException.BlockedUntilUtc,
                    context.TraceIdentifier);
                break;

            case FullImportCapacityExceededException:
                statusCode = HttpStatusCode.TooManyRequests;
                title = "Full Import Capacity Reached";
                detail = exception.Message;
                errors = new Dictionary<string, IReadOnlyCollection<string>>
                {
                    ["General"] = new[] { exception.Message }
                };
                context.Response.Headers.RetryAfter = "60";
                _logger.LogWarning(
                    "Full import capacity rejected {Method} {Path}. Detail={Detail} TraceId={TraceId}",
                    context.Request.Method, context.Request.Path, detail, context.TraceIdentifier);
                break;

            case ImportCapacityExceededException:
                statusCode = HttpStatusCode.TooManyRequests;
                title = "Import Capacity Reached";
                detail = exception.Message;
                errors = new Dictionary<string, IReadOnlyCollection<string>>
                {
                    ["General"] = new[] { exception.Message }
                };
                context.Response.Headers.RetryAfter = "60";
                _logger.LogWarning(
                    "Import capacity rejected {Method} {Path}. Detail={Detail} TraceId={TraceId}",
                    context.Request.Method, context.Request.Path, detail, context.TraceIdentifier);
                break;

            case BookImportProcessingTimeoutException:
                statusCode = HttpStatusCode.RequestTimeout;
                title = "Full Import Timed Out";
                detail = exception.Message;
                errors = new Dictionary<string, IReadOnlyCollection<string>>
                {
                    ["General"] = new[] { exception.Message }
                };
                _logger.LogWarning(
                    "Full import timed out for {Method} {Path}. Detail={Detail} TraceId={TraceId}",
                    context.Request.Method, context.Request.Path, detail, context.TraceIdentifier);
                break;

            case EntityAlreadyExistsException<Genre, Guid>:
                statusCode = HttpStatusCode.Conflict;
                title = ConflictTitle;
                detail = exception.Message;
                _logger.LogWarning("Genre already exists");
                break;

            case EntityNotFoundException<Genre, Guid>:
                statusCode = HttpStatusCode.NotFound;
                title = NotFoundTitle;
                detail = exception.Message;
                _logger.LogWarning("Genre not found");
                break;

            case EntityAlreadyExistsException<Status, Guid>:
                statusCode = HttpStatusCode.Conflict;
                title = ConflictTitle;
                detail = exception.Message;
                _logger.LogWarning("Status already exists");
                break;

            case EntityNotFoundException<Status, Guid>:
                statusCode = HttpStatusCode.NotFound;
                title = NotFoundTitle;
                detail = exception.Message;
                _logger.LogWarning("Status not found");
                break;

            case EntityAlreadyExistsException<ContentType, Guid>:
                statusCode = HttpStatusCode.Conflict;
                title = ConflictTitle;
                detail = exception.Message;
                _logger.LogWarning("Type already exists");
                break;

            case EntityNotFoundException<ContentType, Guid>:
                statusCode = HttpStatusCode.NotFound;
                title = NotFoundTitle;
                detail = exception.Message;
                _logger.LogWarning("Type not found");
                break;

            case EntityNotFoundException<Book, Guid>:
                statusCode = HttpStatusCode.NotFound;
                title = NotFoundTitle;
                detail = exception.Message;
                _logger.LogWarning("Book not found");
                break;

            case EntityNotFoundException<BookCover, Guid>:
                statusCode = HttpStatusCode.NotFound;
                title = NotFoundTitle;
                detail = exception.Message;
                _logger.LogWarning("Book cover not found");
                break;

            case EntityNotFoundException<Tag, Guid>:
            case EntityNotFoundException<Author, Guid>:
                statusCode = HttpStatusCode.NotFound;
                title = NotFoundTitle;
                detail = exception.Message;
                _logger.LogWarning("Managed library metadata was not found");
                break;

            case EntityAlreadyExistsException<Author, Guid> authorConflict:
                statusCode = HttpStatusCode.Conflict;
                title = ConflictTitle;
                detail = $"The author name '{authorConflict.Name}' is already assigned to another author.";
                _logger.LogWarning("Author alias already exists. ExistingId={ExistingId}", authorConflict.ExistingId);
                break;

            case EntityInUseException<Tag>:
            case EntityInUseException<Author>:
                statusCode = HttpStatusCode.Conflict;
                title = ConflictTitle;
                detail = exception.Message;
                _logger.LogWarning("Managed library metadata conflict: {Message}", exception.Message);
                break;

            case EntityAlreadyExistsException<Book, Guid> bookConflict:
                statusCode = HttpStatusCode.Conflict;
                title = ConflictTitle;
                detail = $"A book named '{bookConflict.Name}' already exists.";
                _logger.LogWarning("Book already exists. ExistingId={ExistingId}", bookConflict.ExistingId);
                break;

            default:
                title = "Internal Server Error";
                detail = "An internal server error occurred. Please check the logs.";
                _logger.LogError(exception, "Server (500) unhandled error: {Message}", exception.Message);
                break;
        }

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
