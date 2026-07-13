using Api.Controllers;
using Application.Common.DTOs.Book;
using Application.Common.DTOs.Genre;
using Application.Common.Models;
using Application.Common.DTOs.Status;
using Application.Common.DTOs.Type;
using Application.Features.BookFeatures.Commands;
using Application.Features.BookFeatures.Queries.GetBook;
using Application.Features.GenreFeatures.Commands;
using Application.Features.StatusFeatures.Commands;
using Application.Features.TypeFeatures.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Application.UnitTests;

public class AdminControllerTests
{
    [Fact]
    public async Task GetBooks_ShouldForwardQueryStringParametersToMediator()
    {
        var expected = new PaginatedResult<AdminBookListItemDto>
        {
            Skip = 40,
            Take = 20,
            Total = 1,
            Data = [new AdminBookListItemDto { Id = Guid.NewGuid(), PrimaryTitle = "Completed", ContentType = "Novel", Status = "Completed", OwnerId = Guid.NewGuid() }]
        };
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(mock => mock.Send(
                new GetAllAdminBooksQuery(40, 20, "status:comple", "lastModified", "desc"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = CreateController(mediator.Object);

        var result = await controller.GetBooks(40, 20, "status:comple", "lastModified", "desc", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
        mediator.Verify(mock => mock.Send(
            new GetAllAdminBooksQuery(40, 20, "status:comple", "lastModified", "desc"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetBooks_ShouldUseDefaultListParametersWhenQueryStringIsEmpty()
    {
        var expected = new PaginatedResult<AdminBookListItemDto>
        {
            Skip = 0,
            Take = 100,
            Total = 0,
            Data = []
        };
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(mock => mock.Send(
                new GetAllAdminBooksQuery(0, 100, null, "lastModified", "desc"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = CreateController(mediator.Object);

        var result = await controller.GetBooks();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
    }

    [Fact]
    public async Task UpdateBook_ShouldSendAdminScopedCommand()
    {
        var mediator = new Mock<IMediator>();
        var controller = CreateController(mediator.Object);
        var bookId = Guid.NewGuid();
        var command = new UpdateBookCommand(
            bookId,
            "Title",
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            [],
            [],
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            []);

        var result = await controller.UpdateBook(bookId, command);

        Assert.IsType<NoContentResult>(result);
        mediator.Verify(mock => mock.Send(
            It.Is<UpdateBookCommand>(sent => sent.Id == bookId && sent.AdminScope),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateDictionaryItems_ShouldReturnCreatedResults()
    {
        var mediator = new Mock<IMediator>();
        var status = new StatusDto { Id = Guid.NewGuid(), Name = "Paused" };
        var type = new TypeDto { Id = Guid.NewGuid(), Name = "Audio" };
        var genre = new GenreDto { Id = Guid.NewGuid(), Name = "Drama" };
        mediator.Setup(mock => mock.Send(It.IsAny<CreateStatusCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(status);
        mediator.Setup(mock => mock.Send(It.IsAny<CreateTypeCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(type);
        mediator.Setup(mock => mock.Send(It.IsAny<CreateGenreCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(genre);
        var controller = CreateController(mediator.Object);

        var statusResult = Assert.IsType<CreatedResult>(await controller.CreateStatus(new CreateStatusCommand("Paused", null)));
        var typeResult = Assert.IsType<CreatedResult>(await controller.CreateType(new CreateTypeCommand("Audio", null)));
        var genreResult = Assert.IsType<CreatedResult>(await controller.CreateGenre(new CreateGenreCommand("Drama", null)));

        Assert.Same(status, statusResult.Value);
        Assert.Same(type, typeResult.Value);
        Assert.Same(genre, genreResult.Value);
    }

    private static AdminController CreateController(IMediator mediator)
    {
        return new AdminController(mediator, NullLogger<AdminController>.Instance);
    }
}
