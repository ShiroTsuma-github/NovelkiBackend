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

        return Ok(bookDto);
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBookCommand model)
    {
        var command = new UpdateBookCommand(
            id,
            model.PrimaryTitle,
            model.ContentTypeId,
            model.StatusId,
            model.AuthorId,
            model.AuthorName,
            model.AlternativeTitles,
            model.GenreIds,
            model.Tags,
            model.TotalChapters,
            model.CurrentChapterNumber,
            model.CurrentChapterLabel,
            model.Rating,
            model.Priority,
            model.Description,
            model.Comment,
            model.Notes,
            model.RawImportedLine,
            model.Links);
        await _mediator.Send(command);

        return NoContent();
    }

    [HttpPatch("{id:guid}/progress")]
    [Authorize]
    public async Task<IActionResult> UpdateProgress(Guid id, UpdateBookProgressCommand model)
    {
        var command = new UpdateBookProgressCommand(id, model.CurrentChapterNumber, model.CurrentChapterLabel, model.Comment);
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
