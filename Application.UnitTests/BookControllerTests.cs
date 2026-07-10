using Api.Controllers;
using Application.Common.DTOs.Book;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.BookFeatures.Commands;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text;

namespace Application.UnitTests;

public class BookControllerTests
{
    [Fact]
    public async Task CreateImportSession_ShouldRejectMissingFile()
    {
        var controller = CreateController();

        var result = await controller.CreateImportSession(null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("CSV file is required.", ReadError(badRequest.Value));
    }

    [Fact]
    public async Task CreateImportSession_ShouldRejectEmptyFile()
    {
        var controller = CreateController();
        var file = CreateFormFile(string.Empty, "books.csv");

        var result = await controller.CreateImportSession(file, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("CSV file is empty.", ReadError(badRequest.Value));
    }

    [Fact]
    public async Task CreateImportSession_ShouldReturnServiceResult()
    {
        var expected = new BookImportSessionDto
        {
            SessionId = Guid.NewGuid(),
            FileName = "books.csv",
            Rows = Array.Empty<BookImportRowDto>()
        };
        var importService = new Mock<IBookCsvImportService>();
        importService
            .Setup(service => service.CreateSessionAsync(It.IsAny<Stream>(), "books.csv", CancellationToken.None))
            .ReturnsAsync(expected);
        var controller = CreateController(importService: importService.Object);
        var file = CreateFormFile("primaryTitle,contentType,status\nBook,Novel,Reading", "books.csv");

        var result = await controller.CreateImportSession(file, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
    }

    [Fact]
    public async Task FinalizeImport_ShouldReturnFinalizeResult()
    {
        var sessionId = Guid.NewGuid();
        var expected = new BookImportFinalizeResultDto
        {
            ImportedCount = 2,
            SkippedCount = 1,
            Errors = ["Line 4 skipped"]
        };
        var importService = new Mock<IBookCsvImportService>();
        importService
            .Setup(service => service.FinalizeAsync(sessionId, CancellationToken.None))
            .ReturnsAsync(expected);
        var controller = CreateController(importService: importService.Object);

        var result = await controller.FinalizeImport(sessionId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
    }

    [Fact]
    public async Task UploadCover_ShouldRejectMissingFile()
    {
        var controller = CreateController();

        var result = await controller.UploadCover(Guid.NewGuid(), null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Cover file is required.", ReadError(badRequest.Value));
    }

    [Fact]
    public async Task UploadCover_ShouldRejectEmptyFile()
    {
        var controller = CreateController();
        var file = CreateFormFile(string.Empty, "cover.jpg", "image/jpeg");

        var result = await controller.UploadCover(Guid.NewGuid(), file);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Cover file is empty.", ReadError(badRequest.Value));
    }

    [Fact]
    public async Task GetCoverFile_ShouldReturnFileResultFromMediator()
    {
        var bookId = Guid.NewGuid();
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.Is<GetBookCoverFileQuery>(q => q.BookId == bookId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookCoverFileResult(
                new MemoryStream(Encoding.UTF8.GetBytes("abc")),
                "image/jpeg",
                "cover.jpg"));
        var controller = CreateController(mediator.Object);

        var result = await controller.GetCoverFile(bookId);

        var file = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("image/jpeg", file.ContentType);
        Assert.Equal("cover.jpg", file.FileDownloadName);
    }

    private static BookController CreateController(IMediator? mediator = null, IBookCsvImportService? importService = null)
    {
        return new BookController(
            mediator ?? Mock.Of<IMediator>(),
            importService ?? Mock.Of<IBookCsvImportService>(),
            NullLogger<BookController>.Instance);
    }

    private static IFormFile CreateFormFile(string content, string fileName, string contentType = "text/csv")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static string? ReadError(object? value)
    {
        return value?.GetType().GetProperty("error")?.GetValue(value) as string;
    }
}
