namespace Api.Controllers;

using Application.Features.StatusFeatures.Commands;
using Application.Features.StatusFeatures.Queries.GetStatus;

[ApiController]
[Route("api/v1/[controller]")]
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

        return Ok(status);
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

        if (statusDto == null)
        {
            return NotFound();
        }

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