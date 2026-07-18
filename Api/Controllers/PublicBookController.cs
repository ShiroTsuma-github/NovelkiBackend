namespace Api.Controllers;

using Application.Common.Interfaces;

[ApiController]
[Route(ApiRoutes.PublicBook)]
[Authorize]
public sealed class PublicBookController(IPublicBookService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? search, [FromQuery] int skip = 0,
        [FromQuery] int take = 20, [FromQuery] bool mineOnly = false, CancellationToken cancellationToken = default) =>
        Ok(await service.SearchAsync(search, skip, take, mineOnly, cancellationToken));

    [HttpPost("source/{bookId:guid}")]
    public async Task<IActionResult> Publish(Guid bookId, CancellationToken cancellationToken) =>
        Ok(await service.PublishAsync(bookId, cancellationToken));

    [HttpPut("{id:guid}/refresh")]
    public async Task<IActionResult> Refresh(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.RefreshAsync(id, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Unlist(Guid id, CancellationToken cancellationToken)
    {
        await service.UnlistAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/copy")]
    public async Task<IActionResult> Copy(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.CopyAsync(id, cancellationToken));

    [HttpGet("{id:guid}/cover")]
    public async Task<IActionResult> Cover(Guid id, CancellationToken cancellationToken)
    {
        var cover = await service.OpenCoverAsync(id, cancellationToken);
        return File(cover.Content, cover.MimeType);
    }
}
