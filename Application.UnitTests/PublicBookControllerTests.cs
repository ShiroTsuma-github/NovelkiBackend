namespace Application.UnitTests;

using Api.Controllers;
using Application.Common.DTOs.Book;
using Application.Common.Interfaces;
using Application.Common.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;

public sealed class PublicBookControllerTests
{
    [Fact]
    public async Task Search_ShouldForwardArgumentsAndReturnOk()
    {
        using var source = new CancellationTokenSource();
        var expected = PaginatedResult<PublicBookSnapshotDto>.Create(7, 13, 1, [Snapshot()]);
        var service = new Mock<IPublicBookService>();
        service.Setup(item => item.SearchAsync("needle", 7, 13, true, source.Token)).ReturnsAsync(expected);

        var result = await new PublicBookController(service.Object)
            .Search("needle", 7, 13, true, source.Token);

        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
        service.VerifyAll();
    }

    [Fact]
    public async Task Publish_ShouldForwardArgumentsAndReturnOk()
    {
        var id = Guid.NewGuid();
        using var source = new CancellationTokenSource();
        var expected = Snapshot();
        var service = new Mock<IPublicBookService>();
        service.Setup(item => item.PublishAsync(id, source.Token)).ReturnsAsync(expected);

        var result = await new PublicBookController(service.Object).Publish(id, source.Token);

        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
        service.VerifyAll();
    }

    [Fact]
    public async Task Refresh_ShouldForwardArgumentsAndReturnOk()
    {
        var id = Guid.NewGuid();
        using var source = new CancellationTokenSource();
        var expected = Snapshot();
        var service = new Mock<IPublicBookService>();
        service.Setup(item => item.RefreshAsync(id, source.Token)).ReturnsAsync(expected);

        var result = await new PublicBookController(service.Object).Refresh(id, source.Token);

        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
        service.VerifyAll();
    }

    [Fact]
    public async Task Unlist_ShouldForwardArgumentsAndReturnNoContent()
    {
        var id = Guid.NewGuid();
        using var source = new CancellationTokenSource();
        var service = new Mock<IPublicBookService>();
        service.Setup(item => item.UnlistAsync(id, source.Token)).Returns(Task.CompletedTask);

        var result = await new PublicBookController(service.Object).Unlist(id, source.Token);

        Assert.IsType<NoContentResult>(result);
        service.VerifyAll();
    }

    [Fact]
    public async Task Copy_ShouldForwardArgumentsAndReturnOk()
    {
        var id = Guid.NewGuid();
        using var source = new CancellationTokenSource();
        var expected = new CopyPublicBookResult(Guid.NewGuid());
        var service = new Mock<IPublicBookService>();
        service.Setup(item => item.CopyAsync(id, source.Token)).ReturnsAsync(expected);

        var result = await new PublicBookController(service.Object).Copy(id, source.Token);

        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
        service.VerifyAll();
    }

    [Fact]
    public async Task Cover_ShouldForwardArgumentsAndReturnFile()
    {
        var id = Guid.NewGuid();
        using var source = new CancellationTokenSource();
        var stream = new MemoryStream([1, 2, 3]);
        var service = new Mock<IPublicBookService>();
        service.Setup(item => item.OpenCoverAsync(id, source.Token))
            .ReturnsAsync((stream, "image/webp"));

        var result = await new PublicBookController(service.Object).Cover(id, source.Token);

        var file = Assert.IsType<FileStreamResult>(result);
        Assert.Same(stream, file.FileStream);
        Assert.Equal("image/webp", file.ContentType);
        service.VerifyAll();
        await stream.DisposeAsync();
    }

    private static PublicBookSnapshotDto Snapshot() => new()
    {
        Id = Guid.NewGuid(),
        SourceBookId = Guid.NewGuid(),
        PrimaryTitle = "Snapshot",
        ContentType = "Novel",
        SnapshotAt = DateTimeOffset.UtcNow
    };
}
