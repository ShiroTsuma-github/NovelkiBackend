namespace Infrastructure.BookCovers;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

internal static class BookCoverImageProcessor
{
    private const int FullJpegQuality = 86;
    private const int ThumbnailJpegQuality = 82;
    private const int ThumbnailTargetWidth = 500;
    private static readonly Color AlphaBackground = Color.White;

    private static readonly HashSet<string> AllowedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "JPEG", "PNG", "WEBP"
    };

    public static async Task<ProcessedBookCoverContent> ProcessAsync(
        Stream content,
        string? contentType,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        ValidatedBookCoverContent input =
            await BookCoverStorageValidation.ReadAndValidateAsync(content, contentType, maxBytes, cancellationToken);
        IImageFormat? format = Image.DetectFormat(input.Bytes);
        if (format == null || !AllowedFormats.Contains(format.Name))
        {
            throw new FluentValidation.ValidationException("Cover file must be a JPEG, PNG, or WebP image.");
        }

        try
        {
            using var source = Image.Load<Rgba32>(input.Bytes);
            BookCoverStoredVariantContent original =
                await EncodeVariantAsync(source, source.Width, FullJpegQuality, cancellationToken);
            int thumbnailWidth = Math.Min(ThumbnailTargetWidth, source.Width);
            BookCoverStoredVariantContent thumbnail =
                await EncodeVariantAsync(source, thumbnailWidth, ThumbnailJpegQuality, cancellationToken);
            return new ProcessedBookCoverContent(original, thumbnail);
        }
        catch (UnknownImageFormatException)
        {
            throw new FluentValidation.ValidationException("Cover file must be a JPEG, PNG, or WebP image.");
        }
        catch (InvalidImageContentException)
        {
            throw new FluentValidation.ValidationException("Cover file must be a valid image.");
        }
    }

    private static async Task<BookCoverStoredVariantContent> EncodeVariantAsync(
        Image<Rgba32> source,
        int targetWidth,
        int quality,
        CancellationToken cancellationToken)
    {
        using Image<Rgb24> prepared = RenderOnWhiteBackground(source, targetWidth);
        await using var output = new MemoryStream();
        await prepared.SaveAsJpegAsync(output, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = quality },
            cancellationToken);

        return new BookCoverStoredVariantContent(output.ToArray(), "image/jpeg", prepared.Width, prepared.Height);
    }

    private static Image<Rgb24> RenderOnWhiteBackground(Image<Rgba32> source, int targetWidth)
    {
        int targetHeight = source.Width == targetWidth
            ? source.Height
            : Math.Max(1, (int)Math.Round(source.Height * (targetWidth / (double)source.Width)));

        var canvas = new Image<Rgb24>(targetWidth, targetHeight, AlphaBackground.ToPixel<Rgb24>());
        using Image<Rgba32> scaled = source.Width == targetWidth && source.Height == targetHeight
            ? source.Clone()
            : source.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(targetWidth, targetHeight),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Lanczos3
            }));

        canvas.Mutate(ctx => ctx.DrawImage(scaled, 1f));
        return canvas;
    }
}

internal sealed record ProcessedBookCoverContent(
    BookCoverStoredVariantContent Original,
    BookCoverStoredVariantContent Thumbnail);

internal sealed record BookCoverStoredVariantContent(byte[] Bytes, string MimeType, int Width, int Height);
