namespace Api.Controllers;

using Api.Observability;
using Application.Features.BookFeatures.Queries.GetBook;
using Application.Features.BookFeatures.Commands;
using System.Diagnostics;

[ApiController]
[Route("api/v1/book")]
public class BookController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<BookController> _logger;

    public BookController(IMediator mediator, ILogger<BookController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateBookCommand command)
    {
        using var activity = NovelkiTelemetry.ActivitySource.StartActivity("Book.Create", ActivityKind.Internal);
        var bookId= await _mediator.Send(command);
        activity?.SetTag("book.id", bookId);
        NovelkiTelemetry.BooksCreated.Add(1);
        _logger.LogInformation("Book created. BookId={BookId}", bookId);

        return CreatedAtAction(nameof(GetById), new { id = bookId }, new { Id = bookId });
    }

    [HttpGet()]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] GetAllBooksQuery getAllBooks)
    {
        using var activity = NovelkiTelemetry.ActivitySource.StartActivity("Book.Search", ActivityKind.Internal);
        activity?.SetTag("book.query", getAllBooks.Query);
        activity?.SetTag("book.sort_by", getAllBooks.SortBy);
        activity?.SetTag("book.sort_direction", getAllBooks.SortDirection);
        NovelkiTelemetry.BookSearchRequests.Add(1);
        var books = await _mediator.Send(getAllBooks);
        activity?.SetTag("book.result_count", books.Data.Count);

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
        NovelkiTelemetry.BooksUpdated.Add(1);
        _logger.LogInformation("Book updated. BookId={BookId}", id);

        return NoContent();
    }

    [HttpPatch("{id:guid}/progress")]
    [Authorize]
    public async Task<IActionResult> UpdateProgress(Guid id, UpdateBookProgressCommand model)
    {
        var command = new UpdateBookProgressCommand(id, model.CurrentChapterNumber, model.CurrentChapterLabel, model.Comment);
        await _mediator.Send(command);
        NovelkiTelemetry.BookProgressUpdated.Add(1);
        _logger.LogInformation("Book progress updated. BookId={BookId}", id);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteBookCommand(id));
        _logger.LogInformation("Book deleted. BookId={BookId}", id);
        return NoContent();
    }
}
