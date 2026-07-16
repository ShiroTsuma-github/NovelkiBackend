namespace Infrastructure.BookCovers;

internal static class BookCoverStorageValidation
{
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    public static async Task<ValidatedBookCoverContent> ReadAndValidateAsync(
        Stream content,
        string? contentType,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0)
        {
            throw new FluentValidation.ValidationException("Cover file is empty.");
        }

        if (buffer.Length > maxBytes)
        {
            throw new FluentValidation.ValidationException($"Cover file cannot exceed {maxBytes} bytes.");
        }

        var bytes = buffer.ToArray();
        var detectedMimeType = DetectMimeType(bytes) ?? contentType;
        if (detectedMimeType == null || !AllowedMimeTypes.Contains(detectedMimeType))
        {
            throw new FluentValidation.ValidationException("Cover file must be a JPEG, PNG, or WebP image.");
        }

        return new ValidatedBookCoverContent(bytes, detectedMimeType);
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
}

internal sealed record ValidatedBookCoverContent(byte[] Bytes, string MimeType);
