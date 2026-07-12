namespace Api.Controllers;

using Application.Features.BookFeatures.Commands;
using Application.Features.BookFeatures.Queries.GetBook;
using Application.Common.DTOs.Book;
using Application.Common.Interfaces;
using Api.Observability;
using System.Diagnostics;
using System.Text;

[ApiController]
[Route("api/v1/book")]
public class BookController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IBookCsvImportService _bookCsvImportService;
    private readonly ILogger<BookController> _logger;

    public BookController(IMediator mediator, IBookCsvImportService bookCsvImportService, ILogger<BookController> logger)
    {
        _mediator = mediator;
        _bookCsvImportService = bookCsvImportService;
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
            model.Notes,
            model.RawImportedLine,
            model.Links);
        await _mediator.Send(command);
        NovelkiTelemetry.BooksUpdated.Add(1);
        _logger.LogInformation("Book updated. BookId={BookId}", id);

        return NoContent();
    }

    [HttpPost("import/sessions")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> CreateImportSession(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null)
        {
            return BadRequest(new { error = "CSV file is required." });
        }

        if (file.Length == 0)
        {
            return BadRequest(new { error = "CSV file is empty." });
        }

        await using var stream = file.OpenReadStream();
        var result = await _bookCsvImportService.CreateSessionAsync(stream, file.FileName, cancellationToken);
        _logger.LogInformation("Book CSV import session created. SessionId={SessionId}", result.SessionId);

        return Ok(result);
    }

    [HttpGet("import/template")]
    [Authorize]
    public IActionResult DownloadImportTemplate()
    {
        var template = _bookCsvImportService.CreateTemplate();
        var bytes = Encoding.UTF8.GetBytes(template);
        return File(bytes, "text/csv; charset=utf-8", "book-import-template.csv");
    }

    [HttpGet("import/sessions/{sessionId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetImportSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _bookCsvImportService.GetSessionAsync(sessionId, cancellationToken);
        return Ok(result);
    }

    [HttpPut("import/sessions/{sessionId:guid}/rows/{rowId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateImportRow(Guid sessionId, Guid rowId, [FromBody] UpdateBookImportRowRequest request, CancellationToken cancellationToken)
    {
        var result = await _bookCsvImportService.UpdateRowAsync(sessionId, rowId, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("import/sessions/{sessionId:guid}/rows/{rowId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteImportRow(Guid sessionId, Guid rowId, CancellationToken cancellationToken)
    {
        var result = await _bookCsvImportService.DeleteRowAsync(sessionId, rowId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("import/sessions/{sessionId:guid}/finalize")]
    [Authorize]
    public async Task<IActionResult> FinalizeImport(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _bookCsvImportService.FinalizeAsync(sessionId, cancellationToken);
        _logger.LogInformation("Book CSV import finalized. SessionId={SessionId} Imported={Imported} Skipped={Skipped}", sessionId, result.ImportedCount, result.SkippedCount);
        return Ok(result);
    }

    [HttpDelete("import/sessions/{sessionId:guid}")]
    [Authorize]
    public async Task<IActionResult> CancelImport(Guid sessionId, CancellationToken cancellationToken)
    {
        await _bookCsvImportService.CancelAsync(sessionId, cancellationToken);
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

    [HttpPut("{id:guid}/cover")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadCover(Guid id, IFormFile? file)
    {
        if (file == null)
        {
            return BadRequest(new { error = "Cover file is required." });
        }

        if (file.Length == 0)
        {
            return BadRequest(new { error = "Cover file is empty." });
        }

        await using var stream = file.OpenReadStream();
        var cover = await _mediator.Send(new UploadBookCoverCommand(id, stream, file.FileName, file.ContentType, file.Length));
        _logger.LogInformation("Book cover uploaded. BookId={BookId}", id);

        return Ok(cover);
    }

    [HttpPut("{id:guid}/cover/url")]
    [Authorize]
    public async Task<IActionResult> SetCoverFromUrl(Guid id, [FromBody] SetBookCoverFromUrlRequest request)
    {
        var cover = await _mediator.Send(new SetBookCoverFromUrlCommand(id, request.ImageUrl));
        _logger.LogInformation("Book cover set from URL. BookId={BookId}", id);

        return Ok(cover);
    }

    [HttpPost("{id:guid}/cover/refresh")]
    [Authorize]
    public async Task<IActionResult> RefreshCover(Guid id)
    {
        var cover = await _mediator.Send(new RefreshBookCoverCommand(id));
        _logger.LogInformation("Book cover refresh queued. BookId={BookId}", id);

        return Accepted(cover);
    }

    [HttpDelete("{id:guid}/cover")]
    [Authorize]
    public async Task<IActionResult> DeleteCover(Guid id)
    {
        await _mediator.Send(new DeleteBookCoverCommand(id));
        _logger.LogInformation("Book cover deleted. BookId={BookId}", id);
        return NoContent();
    }

    [HttpGet("{id:guid}/cover/file")]
    [Authorize]
    public async Task<IActionResult> GetCoverFile(Guid id)
    {
        var result = await _mediator.Send(new GetBookCoverFileQuery(id));

        return File(result.Content, result.MimeType, result.FileName);
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

public sealed record SetBookCoverFromUrlRequest(string ImageUrl);
