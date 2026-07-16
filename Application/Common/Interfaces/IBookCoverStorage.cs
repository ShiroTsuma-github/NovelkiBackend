namespace Application.Common.Interfaces;

public interface IBookCoverStorage
{
    public Task<BookCoverStoredFiles> SaveAsync(Guid ownerId, Guid bookId, Stream content, string fileName,
        string? contentType, CancellationToken cancellationToken);

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken);
    public Task DeleteIfExistsAsync(string? storagePath, CancellationToken cancellationToken);
}

public sealed record BookCoverStoredVariant(string StoragePath, string MimeType, long SizeBytes, int Width, int Height);

public sealed record BookCoverStoredFiles(BookCoverStoredVariant Original, BookCoverStoredVariant Thumbnail);
