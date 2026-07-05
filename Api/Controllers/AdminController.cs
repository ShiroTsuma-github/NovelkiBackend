namespace Api.Controllers;

using Application.Features.BookFeatures.Commands;
using Application.Features.BookFeatures.Queries.GetBook;
using Application.Features.GenreFeatures.Commands;
using Application.Features.StatusFeatures.Commands;
using Application.Features.TypeFeatures.Commands;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("books")]
    public async Task<IActionResult> GetBooks([FromQuery] GetAllAdminBooksQuery query)
    {
        var books = await _mediator.Send(query);

        return Ok(books);
    }

    [HttpGet("books/{id:guid}")]
    public async Task<IActionResult> GetBook(Guid id)
    {
        var book = await _mediator.Send(new GetAdminBookQuery(id));

        return Ok(book);
    }

    [HttpPut("books/{id:guid}")]
    public async Task<IActionResult> UpdateBook(Guid id, [FromBody] UpdateBookCommand model)
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
            model.Links)
        {
            AdminScope = true
        };

        await _mediator.Send(command);

        return NoContent();
    }

    [HttpPost("statuses")]
    public async Task<IActionResult> CreateStatus([FromBody] CreateStatusCommand command)
    {
        var status = await _mediator.Send(command);

        return Created($"/api/v1/status/{status.Id}", status);
    }

    [HttpPost("types")]
    public async Task<IActionResult> CreateType([FromBody] CreateTypeCommand command)
    {
        var type = await _mediator.Send(command);

        return Created($"/api/v1/type/{type.Id}", type);
    }

    [HttpPost("genres")]
    public async Task<IActionResult> CreateGenre([FromBody] CreateGenreCommand command)
    {
        var genre = await _mediator.Send(command);

        return Created($"/api/v1/genre/{genre.Id}", genre);
    }
}
