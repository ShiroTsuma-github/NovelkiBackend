namespace Infrastructure.BookCovers;

using Microsoft.Extensions.Options;

public sealed class LocalBookCoverStorage : IBookCoverStorage
{
    private const string InvalidStoragePathMessage = "Invalid cover storage path.";

    private readonly BookCoverOptions _options;

    public LocalBookCoverStorage(IOptions<BookCoverOptions> options)
    {
        _options = options.Value;
    }

    public async Task<BookCoverStoredFiles> SaveAsync(Guid ownerId, Guid bookId, Stream content, string fileName,
        string? contentType, CancellationToken cancellationToken)
    {
        var processed =
            await BookCoverImageProcessor.ProcessAsync(content, contentType, _options.MaxBytes, cancellationToken);
        var directory = Path.Combine(_options.StorageRoot, ownerId.ToString("N"));
        Directory.CreateDirectory(directory);
        var rootPath = Path.GetFullPath(_options.StorageRoot);
        var fullPath = EnsurePathWithinRoot(rootPath, directory, $"{bookId:N}.jpg");
        var thumbnailPath = EnsurePathWithinRoot(rootPath, directory, $"{bookId:N}.thumb.jpg");

        await using (var file = File.Create(fullPath))
        {
            await file.WriteAsync(processed.Original.Bytes, cancellationToken);
        }

        await using (var file = File.Create(thumbnailPath))
        {
            await file.WriteAsync(processed.Thumbnail.Bytes, cancellationToken);
        }

        return new BookCoverStoredFiles(
            new BookCoverStoredVariant(
                Path.GetRelativePath(_options.StorageRoot, fullPath),
                processed.Original.MimeType,
                processed.Original.Bytes.Length,
                processed.Original.Width,
                processed.Original.Height),
            new BookCoverStoredVariant(
                Path.GetRelativePath(_options.StorageRoot, thumbnailPath),
                processed.Thumbnail.MimeType,
                processed.Thumbnail.Bytes.Length,
                processed.Thumbnail.Width,
                processed.Thumbnail.Height));
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken)
    {
        var path = ResolveStoragePath(storagePath);
        if (!File.Exists(path))
        {
            throw new EntityNotFoundException<BookCover, Guid>(Guid.Empty);
        }

        return Task.FromResult<Stream>(File.OpenRead(path));
    }

    public Task DeleteIfExistsAsync(string? storagePath, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(storagePath))
        {
            var path = ResolveStoragePath(storagePath);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        return Task.CompletedTask;
    }

    private string ResolveStoragePath(string storagePath)
    {
        var rootPath = Path.GetFullPath(_options.StorageRoot);
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, storagePath));
        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(InvalidStoragePathMessage);
        }

        return fullPath;
    }

    private static string EnsurePathWithinRoot(string rootPath, string directory, string fileName)
    {
        var fullPath = Path.GetFullPath(Path.Combine(directory, fileName));
        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(InvalidStoragePathMessage);
        }

        return fullPath;
    }
}
