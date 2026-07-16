namespace Api.Controllers;

using Application.Common.DTOs.Author;
using Application.Features.AuthorFeatures.Queries;

[ApiController]
[Route("api/v1/author")]
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
        IReadOnlyCollection<AuthorDto> authors = await _mediator.Send(query);
        return Ok(authors);
    }
}
