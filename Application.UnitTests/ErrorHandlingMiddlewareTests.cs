using System.Text.Json;
using Domain.Entities;
using Domain.Exceptions;
using Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.UnitTests;

public class ErrorHandlingMiddlewareTests
{
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
        Assert.Contains("Account with username 'reader' already exists.", usernameErrors.EnumerateArray().Select(e => e.GetString()));
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
        Assert.Contains("The account with email reader@example.com already exists.", emailErrors.EnumerateArray().Select(e => e.GetString()));
    }
}
