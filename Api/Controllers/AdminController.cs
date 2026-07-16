namespace Api.Controllers;

using Observability;
using Application.Features.BookFeatures.Commands;
using Application.Features.BookFeatures.Queries.GetBook;
using Application.Features.GenreFeatures.Commands;
using Application.Features.StatusFeatures.Commands;
using Application.Features.TypeFeatures.Commands;
using System.Diagnostics;
using Application.Common.DTOs.Book;
using Application.Common.DTOs.Genre;
using Application.Common.DTOs.Status;
using Application.Common.DTOs.Type;
using Application.Common.Interfaces;
using Application.Common.Models;

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
    public async Task<IActionResult> GetBooks(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        [FromQuery] string? query = null,
        [FromQuery] string? sortBy = "lastModified",
        [FromQuery] string? sortDirection = "desc",
        CancellationToken cancellationToken = default)
    {
        var request = new GetAllAdminBooksQuery(skip, take, query, sortBy, sortDirection);
        using Activity? activity =
            NovelkiTelemetry.ActivitySource.StartActivity("Admin.Book.Search", ActivityKind.Internal);
        activity?.SetTag("book.query", request.Query);
        activity?.SetTag("book.sort_by", request.SortBy);
        activity?.SetTag("book.sort_direction", request.SortDirection);
        NovelkiTelemetry.BookSearchRequests.Add(1);
        PaginatedResult<AdminBookListItemDto> books = await _mediator.Send(request, cancellationToken);
        activity?.SetTag("book.result_count", books.Data.Count);

        return Ok(books);
    }

    [HttpGet("books/{id:guid}")]
    public async Task<IActionResult> GetBook(Guid id)
    {
        AdminBookDto book = await _mediator.Send(new GetAdminBookQuery(id));

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
            model.Notes,
            model.RawImportedLine,
            model.Links) { AdminScope = true };

        await _mediator.Send(command);
        NovelkiTelemetry.BooksUpdated.Add(1);
        _logger.LogInformation("Admin updated book. BookId={BookId}", id);

        return NoContent();
    }

    [HttpPost("statuses")]
    public async Task<IActionResult> CreateStatus([FromBody] CreateStatusCommand command)
    {
        StatusDto status = await _mediator.Send(command);
        NovelkiTelemetry.AdminDictionaryCreated.Add(1, new KeyValuePair<string, object?>("dictionary.type", "status"));
        _logger.LogInformation("Admin created status. StatusId={StatusId}", status.Id);

        return Created($"/api/v1/status/{status.Id}", status);
    }

    [HttpPost("types")]
    public async Task<IActionResult> CreateType([FromBody] CreateTypeCommand command)
    {
        TypeDto type = await _mediator.Send(command);
        NovelkiTelemetry.AdminDictionaryCreated.Add(1, new KeyValuePair<string, object?>("dictionary.type", "type"));
        _logger.LogInformation("Admin created type. TypeId={TypeId}", type.Id);

        return Created($"/api/v1/type/{type.Id}", type);
    }

    [HttpPost("genres")]
    public async Task<IActionResult> CreateGenre([FromBody] CreateGenreCommand command)
    {
        GenreDto genre = await _mediator.Send(command);
        NovelkiTelemetry.AdminDictionaryCreated.Add(1, new KeyValuePair<string, object?>("dictionary.type", "genre"));
        _logger.LogInformation("Admin created genre. GenreId={GenreId}", genre.Id);

        return Created($"/api/v1/genre/{genre.Id}", genre);
    }

    [HttpDelete("books/owner/{ownerId:guid}")]
    public async Task<IActionResult> DeleteBooksByOwner(Guid ownerId, CancellationToken cancellationToken)
    {
        AdminLibraryPurgeResult result =
            await _mediator.Send(new DeleteBooksByOwnerCommand(ownerId), cancellationToken);
        _logger.LogInformation(
            "Admin purged owner library. OwnerId={OwnerId} DeletedBooks={DeletedBooks} DeletedAuthors={DeletedAuthors} DeletedTags={DeletedTags}",
            ownerId,
            result.DeletedBooks,
            result.DeletedAuthors,
            result.DeletedTags);
        return Ok(result);
    }
}
