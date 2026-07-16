namespace Application.Common.Interfaces;

public interface IBookCoverRemoteImageService
{
    public Task<BookCoverStoredFiles> SaveFromUrlAsync(Guid ownerId, Guid bookId, string imageUrl,
        CancellationToken cancellationToken);
}
