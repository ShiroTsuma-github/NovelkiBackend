using System.Text;
using Api.Controllers;
using Application.Common.DTOs.Book;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.BookFeatures.Commands;
using Application.Features.BookFeatures.Queries.GetBook;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

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
    public async Task DeleteInvalidImportRows_ShouldReturnUpdatedSession()
    {
        var sessionId = Guid.NewGuid();
        var expected = new BookImportSessionDto
        {
            SessionId = sessionId,
            FileName = "books.csv",
            TotalRows = 1,
            ValidRows = 1,
            InvalidRows = 0,
            CanFinalize = true,
            Rows = Array.Empty<BookImportRowDto>()
        };
        var importService = new Mock<IBookCsvImportService>();
        importService
            .Setup(service => service.DeleteInvalidRowsAsync(sessionId, CancellationToken.None))
            .ReturnsAsync(expected);
        var controller = CreateController(importService: importService.Object);

        var result = await controller.DeleteInvalidImportRows(sessionId, CancellationToken.None);

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
        Assert.Equal("private, max-age=2592000, immutable", controller.Response.Headers.CacheControl);
        Assert.Equal("Authorization", controller.Response.Headers.Vary);
    }

    [Fact]
    public async Task GetCoverThumbnail_ShouldReturnFileResultFromMediator()
    {
        var bookId = Guid.NewGuid();
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.Is<GetBookCoverThumbnailFileQuery>(q => q.BookId == bookId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookCoverFileResult(
                new MemoryStream(Encoding.UTF8.GetBytes("thumb")),
                "image/jpeg",
                "cover.thumb.jpg"));
        var controller = CreateController(mediator.Object);

        var result = await controller.GetCoverThumbnail(bookId);

        var file = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("image/jpeg", file.ContentType);
        Assert.Equal("cover.thumb.jpg", file.FileDownloadName);
        Assert.Equal("private, max-age=2592000, immutable", controller.Response.Headers.CacheControl);
        Assert.Equal("Authorization", controller.Response.Headers.Vary);
    }

    [Fact]
    public async Task ExportBooks_ShouldReturnCsvForCurrentQueryAndSort()
    {
        var mediator = new Mock<IMediator>();
        var importService = new Mock<IBookCsvImportService>();
        var exportService = new Mock<IBookCsvExportService>();
        var logger = new Mock<ILogger<BookController>>();
        var firstPage = new PaginatedResult<BookDto>
        {
            Skip = 0,
            Take = 1,
            Total = 2,
            Data = [Book("Placeholder")]
        };
        var fullPage = new PaginatedResult<BookDto>
        {
            Skip = 0,
            Take = 2,
            Total = 2,
            Data =
            [
                Book("Alpha, Book", author: "Toika", notes: "Line 1\nLine 2"),
                Book("Beta", tags: ["favorite", "mystery"]),
            ]
        };
        mediator
            .SetupSequence(mock => mock.Send(It.IsAny<GetAllBooksForExportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstPage)
            .ReturnsAsync(fullPage);
        exportService
            .Setup(service => service.Build(fullPage.Data))
            .Returns("exported,csv\n");
        var controller = new BookController(mediator.Object, importService.Object, exportService.Object, logger.Object);

        var result = await controller.ExportBooks("author:Toika", "title", "asc");

        var file = Assert.IsType<FileContentResult>(result);
        var csv = Encoding.UTF8.GetString(file.FileContents);

        Assert.Equal("books-export.csv", file.FileDownloadName);
        Assert.Equal("exported,csv\n", csv);
        mediator.Verify(mock => mock.Send(new GetAllBooksForExportQuery(0, 1, "author:Toika", "title", "asc"), It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(mock => mock.Send(new GetAllBooksForExportQuery(0, 2, "author:Toika", "title", "asc"), It.IsAny<CancellationToken>()), Times.Once);
        exportService.Verify(service => service.Build(fullPage.Data), Times.Once);
    }

    [Fact]
    public async Task GetSummary_ShouldReturnMediatorSummary()
    {
        var expected = new BookSummaryDto
        {
            TotalBooks = 3,
            RatedBooks = 2,
            UnratedBooks = 1,
            AverageRating = 8.5,
            CurrentChapters = 480,
            BooksWithKnownCurrentChapter = 2,
            BooksWithoutKnownCurrentChapter = 1,
            StatusCounts =
            [
                new BookSummaryStatusCountDto { Status = "Reading", Count = 2 },
                new BookSummaryStatusCountDto { Status = "Completed", Count = 1 },
            ],
            TypeCounts =
            [
                new BookSummaryTypeCountDto { Type = "Novel", BookCount = 3, CurrentChapters = 480 },
            ],
            GenreCounts =
            [
                new BookSummaryGenreCountDto { Genre = "Fantasy", BookCount = 2 },
            ],
            RatingCounts =
            [
                new BookSummaryRatingCountDto { Rating = 8, BookCount = 1 },
                new BookSummaryRatingCountDto { Rating = 9, BookCount = 1 },
            ],
        };
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(mock => mock.Send(new GetBookSummaryQuery("author:Toika"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = CreateController(mediator.Object);

        var result = await controller.GetSummary(new GetBookSummaryQuery("author:Toika"));

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
    }

    private static BookController CreateController(
        IMediator? mediator = null,
        IBookCsvImportService? importService = null,
        IBookCsvExportService? exportService = null)
    {
        return new BookController(
            mediator ?? Mock.Of<IMediator>(),
            importService ?? Mock.Of<IBookCsvImportService>(),
            exportService ?? Mock.Of<IBookCsvExportService>(),
            NullLogger<BookController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
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

    private static BookDto Book(
        string title,
        string? author = null,
        string contentType = "Novel",
        string status = "Reading",
        IEnumerable<string>? genres = null,
        IEnumerable<string>? tags = null,
        string? notes = null)
    {
        return new BookDto
        {
            Id = Guid.NewGuid(),
            Created = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            LastModified = DateTimeOffset.Parse("2026-01-02T00:00:00Z"),
            PrimaryTitle = title,
            AlternativeTitles = [],
            Author = author,
            ContentType = contentType,
            Status = status,
            ProgressHistory = [],
            Genres = genres?.ToList() ?? [],
            Tags = tags?.ToList() ?? [],
            Links = [],
            Notes = notes,
        };
    }

    private static string? ReadError(object? value)
    {
        return value?.GetType().GetProperty("error")?.GetValue(value) as string;
    }
}
