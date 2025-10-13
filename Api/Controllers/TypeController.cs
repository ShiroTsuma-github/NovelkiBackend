namespace Api.Controllers;

using Application.Common.DTOs.Type;
using Application.Features.TypeFeatures.Commands;
using Application.Features.TypeFeatures.Queries.GetType;

[ApiController]
[Route("api/v1/type")]
public class TypeController : ControllerBase
{
    private readonly IMediator _mediator;

    public TypeController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateTypeCommand command)
    {
        var type = await _mediator.Send(command);

        return Ok(type);
    }

    [HttpGet()]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] GetAllTypesQuery getAllTypees)
    {
        var types = await _mediator.Send(getAllTypees);

        return Ok(types);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var typeDto = await _mediator.Send(new GetTypeQuery<TypeDto>(id));

        if (typeDto == null)
        {
            return NotFound();
        }

        return Ok(typeDto);
    }

    [HttpGet("{id:guid}/details")]
    [Authorize]
    public async Task<IActionResult> GetByIdDetails(Guid id)
    {
        var typeDto = await _mediator.Send(new GetTypeQuery<TypeDetailsDto>(id));

        if (typeDto == null)
        {
            return NotFound();
        }

        return Ok(typeDto);
    }

    [HttpGet("by-name/{name}")]
    [Authorize]
    public async Task<IActionResult> GetByName(string name)
    {
        var typeDto = await _mediator.Send(new GetTypeByNameQuery<TypeDto>(name));

        if (typeDto == null)
        {
            return NotFound();
        }

        return Ok(typeDto);
    }

    [HttpGet("by-name/{name}/details")]
    [Authorize]
    public async Task<IActionResult> GetByNameDetails(string name)
    {
        var typeDto = await _mediator.Send(new GetTypeByNameQuery<TypeDetailsDto>(name));

        if (typeDto == null)
        {
            return NotFound();
        }

        return Ok(typeDto);
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, UpdateTypeCommand updateType)
    {
        updateType.Id = id;
        var typeDto = await _mediator.Send(updateType);

        return Ok(typeDto);
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteTypeCommand(id));
        return NoContent();
    }
}