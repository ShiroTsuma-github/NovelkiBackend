namespace Api.Controllers;

using Application.Common.DTOs.Status;
using Application.Common.Models;
using Application.Features.StatusFeatures.Commands;
using Application.Features.StatusFeatures.Queries.GetStatus;

[ApiController]
[Route(ApiRoutes.Status)]
public class StatusController : ControllerBase
{
    private readonly IMediator _mediator;

    public StatusController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [Authorize(Roles = AuthorizationRoles.Admin)]
    public async Task<IActionResult> Create([FromBody] CreateStatusCommand command)
    {
        var status = await _mediator.Send(command);

        return CreatedAtAction(nameof(GetById), new { id = status.Id }, status);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] GetAllStatusesQuery getAllStatuses)
    {
        var statuses = await _mediator.Send(getAllStatuses);

        return Ok(statuses);
    }

    [HttpGet(ApiRouteTemplates.Id)]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var statusDto = await _mediator.Send(new GetStatusQuery(id));

        return Ok(statusDto);
    }

    [HttpGet(ApiRouteTemplates.IdDetails)]
    [Authorize]
    public async Task<IActionResult> GetByIdDetails(Guid id)
    {
        var statusDto = await _mediator.Send(new GetStatusDetailsQuery(id));

        return Ok(statusDto);
    }

    [HttpGet(ApiRouteTemplates.ByName)]
    [Authorize]
    public async Task<IActionResult> GetByName(string name)
    {
        var statusDto = await _mediator.Send(new GetStatusByNameQuery(name));

        return Ok(statusDto);
    }

    [HttpGet(ApiRouteTemplates.ByNameDetails)]
    [Authorize]
    public async Task<IActionResult> GetByNameDetails(string name)
    {
        var statusDto = await _mediator.Send(new GetStatusDetailsByNameQuery(name));

        return Ok(statusDto);
    }

    [HttpPut(ApiRouteTemplates.Id)]
    [Authorize(Roles = AuthorizationRoles.Admin)]
    public async Task<IActionResult> Update(Guid id, UpdateStatusCommand updateStatus)
    {
        updateStatus.Id = id;
        var statusDto = await _mediator.Send(updateStatus);

        return Ok(statusDto);
    }

    [HttpDelete(ApiRouteTemplates.Id)]
    [Authorize(Roles = AuthorizationRoles.Admin)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteStatusCommand(id));
        return NoContent();
    }
}
