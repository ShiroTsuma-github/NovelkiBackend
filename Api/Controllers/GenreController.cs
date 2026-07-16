namespace Api.Controllers;

using Application.Common.DTOs.Genre;
using Application.Common.Models;
using Application.Features.GenreFeatures.Commands;
using Application.Features.GenreFeatures.Queries.GetGenre;

[ApiController]
[Route(ApiRoutes.Genre)]
public class GenreController : ControllerBase
{
    private readonly IMediator _mediator;

    public GenreController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [Authorize(Roles = AuthorizationRoles.Admin)]
    public async Task<IActionResult> Create([FromBody] CreateGenreCommand command)
    {
        var genre = await _mediator.Send(command);

        return CreatedAtAction(nameof(GetById), new { id = genre.Id }, genre);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] GetAllGenresQuery getAllGenres)
    {
        var genres = await _mediator.Send(getAllGenres);

        return Ok(genres);
    }

    [HttpGet(ApiRouteTemplates.Id)]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var genreDto = await _mediator.Send(new GetGenreQuery(id));

        return Ok(genreDto);
    }

    [HttpGet(ApiRouteTemplates.IdDetails)]
    [Authorize]
    public async Task<IActionResult> GetByIdDetails(Guid id)
    {
        var genreDto = await _mediator.Send(new GetGenreDetailsQuery(id));

        return Ok(genreDto);
    }

    [HttpGet(ApiRouteTemplates.ByName)]
    [Authorize]
    public async Task<IActionResult> GetByName(string name)
    {
        var genreDto = await _mediator.Send(new GetGenreByNameQuery(name));

        return Ok(genreDto);
    }

    [HttpGet(ApiRouteTemplates.ByNameDetails)]
    [Authorize]
    public async Task<IActionResult> GetByNameDetails(string name)
    {
        var genreDto = await _mediator.Send(new GetGenreDetailsByNameQuery(name));

        return Ok(genreDto);
    }

    [HttpPut(ApiRouteTemplates.Id)]
    [Authorize(Roles = AuthorizationRoles.Admin)]
    public async Task<IActionResult> Update(Guid id, UpdateGenreCommand updateGenre)
    {
        updateGenre.Id = id;
        var genre = await _mediator.Send(updateGenre);

        return Ok(genre);
    }

    [HttpDelete(ApiRouteTemplates.Id)]
    [Authorize(Roles = AuthorizationRoles.Admin)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteGenreCommand(id));
        return NoContent();
    }
}
