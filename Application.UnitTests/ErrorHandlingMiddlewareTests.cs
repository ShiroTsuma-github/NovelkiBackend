namespace Application.UnitTests;

using System.Text.Json;
using Domain.Entities;
using Domain.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using Infrastructure.Identity;
using Infrastructure.Middleware;
using Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

public class ErrorHandlingMiddlewareTests
{
    public static TheoryData<Exception, int, string> MappedExceptions => new()
    {
        {
            new EntityNotFoundException<User, string>("missing"), StatusCodes.Status404NotFound, "Authentication Failed"
        },
        { new WrongPasswordException(), StatusCodes.Status401Unauthorized, "Authentication Failed" },
        { new EntityNotFoundException<Infrastructure.Identity.User, Guid>(Guid.NewGuid()), StatusCodes.Status404NotFound, "Not Found" },
        { new CannotDeleteCurrentAccountException(), StatusCodes.Status409Conflict, "Conflict" },
        {
            new EntityAlreadyExistsException<Genre, Guid>("Fantasy", Guid.NewGuid()), StatusCodes.Status409Conflict,
            "Conflict"
        },
        { new EntityNotFoundException<Genre, Guid>(Guid.NewGuid()), StatusCodes.Status404NotFound, "Not Found" },
        {
            new EntityAlreadyExistsException<Status, Guid>("Reading", Guid.NewGuid()), StatusCodes.Status409Conflict,
            "Conflict"
        },
        { new EntityNotFoundException<Status, Guid>(Guid.NewGuid()), StatusCodes.Status404NotFound, "Not Found" },
        {
            new EntityAlreadyExistsException<ContentType, Guid>("Novel", Guid.NewGuid()), StatusCodes.Status409Conflict,
            "Conflict"
        },
        { new EntityNotFoundException<ContentType, Guid>(Guid.NewGuid()), StatusCodes.Status404NotFound, "Not Found" },
        { new EntityNotFoundException<Book, Guid>(Guid.NewGuid()), StatusCodes.Status404NotFound, "Not Found" },
        { new EntityNotFoundException<BookCover, Guid>(Guid.NewGuid()), StatusCodes.Status404NotFound, "Not Found" },
        { new EntityNotFoundException<Tag, Guid>(Guid.NewGuid()), StatusCodes.Status404NotFound, "Not Found" },
        { new EntityNotFoundException<Author, Guid>(Guid.NewGuid()), StatusCodes.Status404NotFound, "Not Found" },
        { new EntityInUseException<Tag>("favorite"), StatusCodes.Status409Conflict, "Conflict" },
        { new EntityInUseException<Author>("Er Gen"), StatusCodes.Status409Conflict, "Conflict" },
        { new EntityInUseException<Genre>("Fantasy"), StatusCodes.Status409Conflict, "Conflict" },
        { new EntityInUseException<Status>("Reading"), StatusCodes.Status409Conflict, "Conflict" },
        { new EntityInUseException<ContentType>("Novel"), StatusCodes.Status409Conflict, "Conflict" },
        {
            new FullImportCapacityExceededException("Full import capacity reached."),
            StatusCodes.Status429TooManyRequests, "Full Import Capacity Reached"
        },
        {
            new ImportCapacityExceededException("Import capacity reached."), StatusCodes.Status429TooManyRequests,
            "Import Capacity Reached"
        },
        {
            new BookImportProcessingTimeoutException("Full import timed out."), StatusCodes.Status408RequestTimeout,
            "Full Import Timed Out"
        },
        {
            new AccountTemporarilyBlockedException(DateTimeOffset.UtcNow.AddHours(24)),
            StatusCodes.Status429TooManyRequests, "Account Temporarily Blocked"
        },
        { new InvalidOperationException("boom"), StatusCodes.Status500InternalServerError, "Internal Server Error" }
    };

    [Fact]
    public async Task InvokeAsync_ShouldCallNextWhenNoExceptionIsThrown()
    {
        var called = false;
        var context = new DefaultHttpContext();
        var middleware = new ErrorHandlingMiddleware(
            _ =>
            {
                called = true;
                return Task.CompletedTask;
            },
            Mock.Of<ILogger<ErrorHandlingMiddleware>>());

        await middleware.InvokeAsync(context);

        Assert.True(called);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(MappedExceptions))]
    public async Task InvokeAsync_ShouldMapKnownExceptions(Exception exception, int expectedStatus,
        string expectedTitle)
    {
        var context = await InvokeWithException(exception);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal(expectedTitle, document.RootElement.GetProperty("title").GetString());
        Assert.Equal(exception.GetType().Name, document.RootElement.GetProperty("type").GetString());
        Assert.Equal(expectedStatus, document.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_ShouldHideBookConflictInternalId()
    {
        var existingId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ErrorHandlingMiddleware(
            _ => throw new EntityAlreadyExistsException<Book, Guid>("Duplicated Book", existingId),
            Mock.Of<ILogger<ErrorHandlingMiddleware>>());

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var detail = document.RootElement.GetProperty("detail").GetString();

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal("A book named 'Duplicated Book' already exists.", detail);
        Assert.DoesNotContain(existingId.ToString(), detail);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnIdentityOperationErrors()
    {
        var context =
            await InvokeWithException(new IdentityOperationFailedException(["Password is too weak."]));

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var errors = document.RootElement.GetProperty("errors");

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.True(errors.TryGetProperty("Identity", out var identityErrors));
        Assert.Contains("Password is too weak.", identityErrors.EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnGroupedValidationErrors()
    {
        var exception = new ValidationException([
            new ValidationFailure("Title", "Title is required."),
            new ValidationFailure("Title", "Title is too long."),
            new ValidationFailure("Rating", "Rating must be valid.")
        ]);

        var context = await InvokeWithException(exception);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var errors = document.RootElement.GetProperty("errors");

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("Title: Title is required.", document.RootElement.GetProperty("detail").GetString());
        Assert.Equal(2, errors.GetProperty("Title").GetArrayLength());
        Assert.Single(errors.GetProperty("Rating").EnumerateArray());
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnAndLogMessageOnlyValidationError()
    {
        var logger = new Mock<ILogger<ErrorHandlingMiddleware>>();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/v1/book/import/full/sessions";
        context.Response.Body = new MemoryStream();
        var middleware = new ErrorHandlingMiddleware(
            _ => throw new ValidationException("Full backup is missing manifest.json."),
            logger.Object);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var errors = document.RootElement.GetProperty("errors");

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("Full backup is missing manifest.json.",
            document.RootElement.GetProperty("detail").GetString());
        Assert.Contains("Full backup is missing manifest.json.",
            errors.GetProperty("General").EnumerateArray().Select(item => item.GetString()));
        logger.Verify(
            candidate => candidate.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("Full backup is missing manifest.json.") &&
                    state.ToString()!.Contains("/api/v1/book/import/full/sessions")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnRemainingBlockInRetryAfterHeader()
    {
        var context = await InvokeWithException(
            new AccountTemporarilyBlockedException(DateTimeOffset.UtcNow.AddHours(24)));

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.True(long.TryParse(context.Response.Headers.RetryAfter, out var retryAfter));
        Assert.InRange(retryAfter, 23 * 60 * 60, 24 * 60 * 60);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnFieldErrorForUsernameConflict()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ErrorHandlingMiddleware(
            _ => throw new UsernameTakenException("reader"),
            Mock.Of<ILogger<ErrorHandlingMiddleware>>());

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var errors = document.RootElement.GetProperty("errors");

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.True(errors.TryGetProperty("Username", out var usernameErrors));
        Assert.Contains("Account with username 'reader' already exists.",
            usernameErrors.EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnFieldErrorForEmailConflict()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ErrorHandlingMiddleware(
            _ => throw new EmailInUseException("reader@example.com"),
            Mock.Of<ILogger<ErrorHandlingMiddleware>>());

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var errors = document.RootElement.GetProperty("errors");

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.True(errors.TryGetProperty("Email", out var emailErrors));
        Assert.Contains("The account with email reader@example.com already exists.",
            emailErrors.EnumerateArray().Select(e => e.GetString()));
    }

    private static async Task<DefaultHttpContext> InvokeWithException(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();
        var middleware = new ErrorHandlingMiddleware(
            _ => throw exception,
            Mock.Of<ILogger<ErrorHandlingMiddleware>>());

        await middleware.InvokeAsync(context);

        return context;
    }
}
