namespace Application.Common.Interfaces;

public interface IBookCoverStorage
{
    Task<BookCoverStoredFile> SaveAsync(Guid ownerId, Guid bookId, Stream content, string fileName, string? contentType, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken);
    Task DeleteIfExistsAsync(string? storagePath, CancellationToken cancellationToken);
}

public sealed record BookCoverStoredFile(string StoragePath, string MimeType, long SizeBytes, int? Width, int? Height);
