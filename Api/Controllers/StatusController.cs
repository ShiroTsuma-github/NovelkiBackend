namespace Api.Controllers;

using Application.Features.StatusFeatures.Commands;
using Application.Features.StatusFeatures.Queries.GetStatus;

[ApiController]
[Route("api/v1/status")]
public class StatusController : ControllerBase
{
    private readonly IMediator _mediator;

    public StatusController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateStatusCommand command)
    {
        var status = await _mediator.Send(command);

        return CreatedAtAction(nameof(GetById), new { id = status.Id }, status);
    }

    [HttpGet()]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] GetAllStatusesQuery getAllStatuses)
    {
        var statuses = await _mediator.Send(getAllStatuses);

        return Ok(statuses);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var statusDto = await _mediator.Send(new GetStatusQuery(id));

        return Ok(statusDto);
    }

    [HttpGet("{id:guid}/details")]
    [Authorize]
    public async Task<IActionResult> GetByIdDetails(Guid id)
    {
        var statusDto = await _mediator.Send(new GetStatusDetailsQuery(id));

        return Ok(statusDto);
    }

    [HttpGet("by-name/{name}")]
    [Authorize]
    public async Task<IActionResult> GetByName(string name)
    {
        var statusDto = await _mediator.Send(new GetStatusByNameQuery(name));

        return Ok(statusDto);
    }

    [HttpGet("by-name/{name}/details")]
    [Authorize]
    public async Task<IActionResult> GetByNameDetails(string name)
    {
        var statusDto = await _mediator.Send(new GetStatusDetailsByNameQuery(name));

        return Ok(statusDto);
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, UpdateStatusCommand updateStatus)
    {
        updateStatus.Id = id;
        var statusDto = await _mediator.Send(updateStatus);

        return Ok(statusDto);
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteStatusCommand(id));
        return NoContent();
    }
}
