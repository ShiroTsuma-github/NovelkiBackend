namespace Application.Common.Interfaces;

public interface IBookCoverRemoteImageService
{
    Task<BookCoverStoredFile> SaveFromUrlAsync(Guid ownerId, Guid bookId, string imageUrl, CancellationToken cancellationToken);
}
