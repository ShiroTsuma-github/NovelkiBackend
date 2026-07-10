namespace Infrastructure.BookCovers;

using Microsoft.Extensions.Options;

public sealed class LocalBookCoverStorage : IBookCoverStorage
{
    private readonly BookCoverOptions _options;

    public LocalBookCoverStorage(IOptions<BookCoverOptions> options)
    {
        _options = options.Value;
    }

    public async Task<BookCoverStoredFile> SaveAsync(Guid ownerId, Guid bookId, Stream content, string fileName, string? contentType, CancellationToken cancellationToken)
    {
        var validated = await BookCoverStorageValidation.ReadAndValidateAsync(content, contentType, _options.MaxBytes, cancellationToken);
        var directory = Path.Combine(_options.StorageRoot, ownerId.ToString("N"));
        Directory.CreateDirectory(directory);
        var fullPath = Path.GetFullPath(Path.Combine(directory, $"{bookId:N}{validated.Extension}"));
        var rootPath = Path.GetFullPath(_options.StorageRoot);
        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid cover storage path.");
        }

        await using (var file = File.Create(fullPath))
        {
            await file.WriteAsync(validated.Bytes, cancellationToken);
        }

        return new BookCoverStoredFile(Path.GetRelativePath(_options.StorageRoot, fullPath), validated.MimeType, validated.Bytes.Length, null, null);
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
            throw new InvalidOperationException("Invalid cover storage path.");
        }

        return fullPath;
    }
}
