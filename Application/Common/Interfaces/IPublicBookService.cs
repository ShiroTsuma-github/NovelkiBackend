namespace Application.Common.Interfaces;

using DTOs.Book;

public interface IPublicBookService
{
    Task<PaginatedResult<PublicBookSnapshotDto>> SearchAsync(string? search, int skip, int take, bool mineOnly,
        CancellationToken cancellationToken);
    Task<PublicBookSnapshotDto> PublishAsync(Guid bookId, CancellationToken cancellationToken);
    Task<PublicBookSnapshotDto> RefreshAsync(Guid snapshotId, CancellationToken cancellationToken);
    Task UnlistAsync(Guid snapshotId, CancellationToken cancellationToken);
    Task UnlistBySourceBookAsync(Guid bookId, CancellationToken cancellationToken);
    Task UnlistAllForOwnerAsync(Guid ownerId, CancellationToken cancellationToken);
    Task<CopyPublicBookResult> CopyAsync(Guid snapshotId, CancellationToken cancellationToken);
    Task<(Stream Content, string MimeType)> OpenCoverAsync(Guid snapshotId, CancellationToken cancellationToken);
}
