namespace Api.Controllers;

using Application.Common.DTOs.Type;
using Application.Common.Models;
using Application.Features.TypeFeatures.Commands;
using Application.Features.TypeFeatures.Queries.GetType;

[ApiController]
[Route(ApiRoutes.Type)]
public class TypeController : ControllerBase
{
    private readonly IMediator _mediator;

    public TypeController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [Authorize(Roles = AuthorizationRoles.Admin)]
    public async Task<IActionResult> Create([FromBody] CreateTypeCommand command)
    {
        var type = await _mediator.Send(command);

        return CreatedAtAction(nameof(GetById), new { id = type.Id }, type);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] GetAllTypesQuery getAllTypees)
    {
        var types = await _mediator.Send(getAllTypees);

        return Ok(types);
    }

    [HttpGet(ApiRouteTemplates.Id)]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var typeDto = await _mediator.Send(new GetTypeQuery(id));

        return Ok(typeDto);
    }

    [HttpGet(ApiRouteTemplates.IdDetails)]
    [Authorize]
    public async Task<IActionResult> GetByIdDetails(Guid id)
    {
        var typeDto = await _mediator.Send(new GetTypeDetailsQuery(id));

        return Ok(typeDto);
    }

    [HttpGet(ApiRouteTemplates.ByName)]
    [Authorize]
    public async Task<IActionResult> GetByName(string name)
    {
        var typeDto = await _mediator.Send(new GetTypeByNameQuery(name));

        return Ok(typeDto);
    }

    [HttpGet(ApiRouteTemplates.ByNameDetails)]
    [Authorize]
    public async Task<IActionResult> GetByNameDetails(string name)
    {
        var typeDto = await _mediator.Send(new GetTypeDetailsByNameQuery(name));

        return Ok(typeDto);
    }

    [HttpPut(ApiRouteTemplates.Id)]
    [Authorize(Roles = AuthorizationRoles.Admin)]
    public async Task<IActionResult> Update(Guid id, UpdateTypeCommand updateType)
    {
        updateType.Id = id;
        var typeDto = await _mediator.Send(updateType);

        return Ok(typeDto);
    }

    [HttpDelete(ApiRouteTemplates.Id)]
    [Authorize(Roles = AuthorizationRoles.Admin)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteTypeCommand(id));
        return NoContent();
    }
}
