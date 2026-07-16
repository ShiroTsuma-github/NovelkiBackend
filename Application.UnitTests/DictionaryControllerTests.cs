using Api.Controllers;
using Application.Common.DTOs.Genre;
using Application.Common.DTOs.Status;
using Application.Common.DTOs.Type;
using Application.Common.Models;
using Application.Features.GenreFeatures.Commands;
using Application.Features.GenreFeatures.Queries.GetGenre;
using Application.Features.StatusFeatures.Commands;
using Application.Features.StatusFeatures.Queries.GetStatus;
using Application.Features.TypeFeatures.Commands;
using Application.Features.TypeFeatures.Queries.GetType;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Application.UnitTests;

public class DictionaryControllerTests
{
    [Fact]
    public async Task GenreController_ShouldForwardAllActionsToMediator()
    {
        var id = Guid.NewGuid();
        var dto = new GenreDto { Id = id, Name = "Fantasy" };
        var details = new GenreDetailsDto { Id = id, Name = "Fantasy" };
        var page = PaginatedResult<GenreDto>.Create(0, 10, 1, [dto]);
        var mediator = new Mock<IMediator>();
        mediator.Setup(mock => mock.Send(It.IsAny<CreateGenreCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        mediator.Setup(mock => mock.Send(It.IsAny<GetAllGenresQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);
        mediator.Setup(mock => mock.Send(It.IsAny<GetGenreQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        mediator.Setup(mock => mock.Send(It.IsAny<GetGenreByNameQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        mediator.Setup(mock => mock.Send(It.IsAny<GetGenreDetailsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);
        mediator.Setup(mock => mock.Send(It.IsAny<GetGenreDetailsByNameQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);
        mediator.Setup(mock => mock.Send(It.IsAny<UpdateGenreCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        var controller = new GenreController(mediator.Object);

        CreatedAtActionResult created =
            Assert.IsType<CreatedAtActionResult>(await controller.Create(new CreateGenreCommand("Fantasy", null)));
        Assert.Equal(nameof(GenreController.GetById), created.ActionName);
        Assert.Same(page, Assert.IsType<OkObjectResult>(await controller.GetAll(new GetAllGenresQuery(0, 10))).Value);
        Assert.Same(dto, Assert.IsType<OkObjectResult>(await controller.GetById(id)).Value);
        Assert.Same(details, Assert.IsType<OkObjectResult>(await controller.GetByIdDetails(id)).Value);
        Assert.Same(dto, Assert.IsType<OkObjectResult>(await controller.GetByName("Fantasy")).Value);
        Assert.Same(details, Assert.IsType<OkObjectResult>(await controller.GetByNameDetails("Fantasy")).Value);
        Assert.Same(dto,
            Assert.IsType<OkObjectResult>(await controller.Update(id, new UpdateGenreCommand { Name = "Drama" }))
                .Value);
        Assert.IsType<NoContentResult>(await controller.Delete(id));
        mediator.Verify(mock => mock.Send(It.IsAny<CreateGenreCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StatusController_ShouldForwardAllActionsToMediator()
    {
        var id = Guid.NewGuid();
        var dto = new StatusDto { Id = id, Name = "Reading" };
        var details = new StatusDetailsDto { Id = id, Name = "Reading" };
        var page = PaginatedResult<StatusDto>.Create(0, 10, 1, [dto]);
        var mediator = new Mock<IMediator>();
        mediator.Setup(mock => mock.Send(It.IsAny<CreateStatusCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        mediator.Setup(mock => mock.Send(It.IsAny<GetAllStatusesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);
        mediator.Setup(mock => mock.Send(It.IsAny<GetStatusQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        mediator.Setup(mock => mock.Send(It.IsAny<GetStatusByNameQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        mediator.Setup(mock => mock.Send(It.IsAny<GetStatusDetailsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);
        mediator.Setup(mock => mock.Send(It.IsAny<GetStatusDetailsByNameQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);
        mediator.Setup(mock => mock.Send(It.IsAny<UpdateStatusCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        var controller = new StatusController(mediator.Object);

        CreatedAtActionResult created =
            Assert.IsType<CreatedAtActionResult>(await controller.Create(new CreateStatusCommand("Reading", null)));
        Assert.Equal(nameof(StatusController.GetById), created.ActionName);
        Assert.Same(page, Assert.IsType<OkObjectResult>(await controller.GetAll(new GetAllStatusesQuery(0, 10))).Value);
        Assert.Same(dto, Assert.IsType<OkObjectResult>(await controller.GetById(id)).Value);
        Assert.Same(details, Assert.IsType<OkObjectResult>(await controller.GetByIdDetails(id)).Value);
        Assert.Same(dto, Assert.IsType<OkObjectResult>(await controller.GetByName("Reading")).Value);
        Assert.Same(details, Assert.IsType<OkObjectResult>(await controller.GetByNameDetails("Reading")).Value);
        Assert.Same(dto,
            Assert.IsType<OkObjectResult>(await controller.Update(id, new UpdateStatusCommand { Name = "Completed" }))
                .Value);
        Assert.IsType<NoContentResult>(await controller.Delete(id));
        mediator.Verify(mock => mock.Send(It.IsAny<CreateStatusCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TypeController_ShouldForwardAllActionsToMediator()
    {
        var id = Guid.NewGuid();
        var dto = new TypeDto { Id = id, Name = "Novel" };
        var details = new TypeDetailsDto { Id = id, Name = "Novel" };
        var page = PaginatedResult<TypeDto>.Create(0, 10, 1, [dto]);
        var mediator = new Mock<IMediator>();
        mediator.Setup(mock => mock.Send(It.IsAny<CreateTypeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        mediator.Setup(mock => mock.Send(It.IsAny<GetAllTypesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);
        mediator.Setup(mock => mock.Send(It.IsAny<GetTypeQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        mediator.Setup(mock => mock.Send(It.IsAny<GetTypeByNameQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        mediator.Setup(mock => mock.Send(It.IsAny<GetTypeDetailsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);
        mediator.Setup(mock => mock.Send(It.IsAny<GetTypeDetailsByNameQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);
        mediator.Setup(mock => mock.Send(It.IsAny<UpdateTypeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        var controller = new TypeController(mediator.Object);

        CreatedAtActionResult created =
            Assert.IsType<CreatedAtActionResult>(await controller.Create(new CreateTypeCommand("Novel", null)));
        Assert.Equal(nameof(TypeController.GetById), created.ActionName);
        Assert.Same(page, Assert.IsType<OkObjectResult>(await controller.GetAll(new GetAllTypesQuery(0, 10))).Value);
        Assert.Same(dto, Assert.IsType<OkObjectResult>(await controller.GetById(id)).Value);
        Assert.Same(details, Assert.IsType<OkObjectResult>(await controller.GetByIdDetails(id)).Value);
        Assert.Same(dto, Assert.IsType<OkObjectResult>(await controller.GetByName("Novel")).Value);
        Assert.Same(details, Assert.IsType<OkObjectResult>(await controller.GetByNameDetails("Novel")).Value);
        Assert.Same(dto,
            Assert.IsType<OkObjectResult>(await controller.Update(id, new UpdateTypeCommand { Name = "Manga" })).Value);
        Assert.IsType<NoContentResult>(await controller.Delete(id));
        mediator.Verify(mock => mock.Send(It.IsAny<CreateTypeCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
