namespace Api.Controllers;

using Application.Features.AuthorFeatures.Commands;
using Application.Features.AuthorFeatures.Queries;

[ApiController]
[Route(ApiRoutes.Author)]
public class AuthorController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthorController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Search([FromQuery] SearchAuthorsQuery query)
    {
        var authors = await _mediator.Send(query);
        return Ok(authors);
    }

    [HttpPut(ApiRouteTemplates.Id)]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, UpdateAuthorCommand command)
    {
        command.Id = id;
        return Ok(await _mediator.Send(command));
    }

    [HttpDelete(ApiRouteTemplates.Id)]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteAuthorCommand(id));
        return NoContent();
    }
}
