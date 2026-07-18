namespace Api.Controllers;

using Application.Features.TagFeatures.Commands;
using Application.Features.TagFeatures.Queries;

[ApiController]
[Route(ApiRoutes.Tag)]
public class TagController : ControllerBase
{
    private readonly IMediator _mediator;

    public TagController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Search([FromQuery] SearchTagsQuery query)
    {
        var tags = await _mediator.Send(query);
        return Ok(tags);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create(CreateTagCommand command)
    {
        var tag = await _mediator.Send(command);
        return Created($"/{ApiRoutes.Tag}/{tag.Id}", tag);
    }

    [HttpPut(ApiRouteTemplates.Id)]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, UpdateTagCommand command)
    {
        command.Id = id;
        return Ok(await _mediator.Send(command));
    }

    [HttpDelete(ApiRouteTemplates.Id)]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteTagCommand(id));
        return NoContent();
    }
}
