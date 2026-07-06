namespace Api.Controllers;

using Api.Observability;
using Application.Features.BookFeatures.Commands;
using Application.Features.BookFeatures.Queries.GetBook;
using Application.Features.GenreFeatures.Commands;
using Application.Features.StatusFeatures.Commands;
using Application.Features.TypeFeatures.Commands;
using System.Diagnostics;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IMediator mediator, ILogger<AdminController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet("books")]
    public async Task<IActionResult> GetBooks([FromQuery] GetAllAdminBooksQuery query)
    {
        using var activity = NovelkiTelemetry.ActivitySource.StartActivity("Admin.Book.Search", ActivityKind.Internal);
        activity?.SetTag("book.query", query.Query);
        activity?.SetTag("book.sort_by", query.SortBy);
        activity?.SetTag("book.sort_direction", query.SortDirection);
        NovelkiTelemetry.BookSearchRequests.Add(1);
        var books = await _mediator.Send(query);
        activity?.SetTag("book.result_count", books.Data.Count);

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
        NovelkiTelemetry.BooksUpdated.Add(1);
        _logger.LogInformation("Admin updated book. BookId={BookId}", id);

        return NoContent();
    }

    [HttpPost("statuses")]
    public async Task<IActionResult> CreateStatus([FromBody] CreateStatusCommand command)
    {
        var status = await _mediator.Send(command);
        NovelkiTelemetry.AdminDictionaryCreated.Add(1, new KeyValuePair<string, object?>("dictionary.type", "status"));
        _logger.LogInformation("Admin created status. StatusId={StatusId}", status.Id);

        return Created($"/api/v1/status/{status.Id}", status);
    }

    [HttpPost("types")]
    public async Task<IActionResult> CreateType([FromBody] CreateTypeCommand command)
    {
        var type = await _mediator.Send(command);
        NovelkiTelemetry.AdminDictionaryCreated.Add(1, new KeyValuePair<string, object?>("dictionary.type", "type"));
        _logger.LogInformation("Admin created type. TypeId={TypeId}", type.Id);

        return Created($"/api/v1/type/{type.Id}", type);
    }

    [HttpPost("genres")]
    public async Task<IActionResult> CreateGenre([FromBody] CreateGenreCommand command)
    {
        var genre = await _mediator.Send(command);
        NovelkiTelemetry.AdminDictionaryCreated.Add(1, new KeyValuePair<string, object?>("dictionary.type", "genre"));
        _logger.LogInformation("Admin created genre. GenreId={GenreId}", genre.Id);

        return Created($"/api/v1/genre/{genre.Id}", genre);
    }
}
