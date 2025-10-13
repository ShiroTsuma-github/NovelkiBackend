namespace Api.Controllers;

using Application.Features.BookFeatures.Queries.GetBook;
using Application.Features.BookFeatures.Commands;

[ApiController]
[Route("api/v1/book")]
public class BookController : ControllerBase
{
    private readonly IMediator _mediator;

    public BookController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateBookCommand command)
    {
        var bookId= await _mediator.Send(command);

        return CreatedAtAction(nameof(GetById), new { id = bookId }, new { Id = bookId });
    }

    [HttpGet()]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] GetAllBooksQuery getAllBooks)
    {
        var books = await _mediator.Send(getAllBooks);

        return Ok(books);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var bookDto = await _mediator.Send(new GetBookQuery(id));

        if (bookDto == null)
        {
            return NotFound();
        }

        return Ok(bookDto);
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, UpdateBookCommand model)
    {
        var command = new UpdateBookCommand(id, model.Title, model.Author);
        await _mediator.Send(command);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteBookCommand(id));
        return NoContent();
    }
}