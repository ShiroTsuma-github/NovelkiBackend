namespace Api.Controllers;

using Application.Features.TagFeatures.Queries;

[ApiController]
[Route("api/v1/tag")]
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
}
