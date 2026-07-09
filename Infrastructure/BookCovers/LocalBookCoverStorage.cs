namespace Infrastructure.BookCovers;

using Microsoft.Extensions.Options;

public sealed class LocalBookCoverStorage : IBookCoverStorage
{
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly BookCoverOptions _options;

    public LocalBookCoverStorage(IOptions<BookCoverOptions> options)
    {
        _options = options.Value;
    }

    public async Task<BookCoverStoredFile> SaveAsync(Guid ownerId, Guid bookId, Stream content, string fileName, string? contentType, CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0)
        {
            throw new FluentValidation.ValidationException("Cover file is empty.");
        }

        if (buffer.Length > _options.MaxBytes)
        {
            throw new FluentValidation.ValidationException($"Cover file cannot exceed {_options.MaxBytes} bytes.");
        }

        var detectedMimeType = DetectMimeType(buffer.ToArray()) ?? contentType;
        if (detectedMimeType == null || !AllowedMimeTypes.Contains(detectedMimeType))
        {
            throw new FluentValidation.ValidationException("Cover file must be a JPEG, PNG, or WebP image.");
        }

        var extension = GetExtension(detectedMimeType);
        var directory = Path.Combine(_options.StorageRoot, ownerId.ToString("N"));
        Directory.CreateDirectory(directory);
        var fullPath = Path.GetFullPath(Path.Combine(directory, $"{bookId:N}{extension}"));
        var rootPath = Path.GetFullPath(_options.StorageRoot);
        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid cover storage path.");
        }

        buffer.Position = 0;
        await using (var file = File.Create(fullPath))
        {
            await buffer.CopyToAsync(file, cancellationToken);
        }

        return new BookCoverStoredFile(Path.GetRelativePath(_options.StorageRoot, fullPath), detectedMimeType, buffer.Length, null, null);
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

    private static string? DetectMimeType(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 &&
            bytes[1] == 0x50 &&
            bytes[2] == 0x4E &&
            bytes[3] == 0x47 &&
            bytes[4] == 0x0D &&
            bytes[5] == 0x0A &&
            bytes[6] == 0x1A &&
            bytes[7] == 0x0A)
        {
            return "image/png";
        }

        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 &&
            bytes[1] == 0x49 &&
            bytes[2] == 0x46 &&
            bytes[3] == 0x46 &&
            bytes[8] == 0x57 &&
            bytes[9] == 0x45 &&
            bytes[10] == 0x42 &&
            bytes[11] == 0x50)
        {
            return "image/webp";
        }

        return null;
    }

    private static string GetExtension(string mimeType)
    {
        return mimeType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".img"
        };
    }
}
