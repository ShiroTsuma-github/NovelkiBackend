namespace Application.Common;

using DTOs.Book;

public static partial class MappingExtensions
{
    public static BookCoverDto ToDto(this BookCover source, Guid bookId)
    {
        var version = GetCoverVersion(source).ToUnixTimeMilliseconds();
        var hasStoredCover = !string.IsNullOrWhiteSpace(source.StoragePath) ||
                             !string.IsNullOrWhiteSpace(source.ThumbnailStoragePath);
        var effectiveStatus = hasStoredCover && source.Status is not (BookCoverStatus.Found or BookCoverStatus.Uploaded)
            ? source.Source is BookCoverSource.ManualUpload or BookCoverSource.ManualUrl
                ? BookCoverStatus.Uploaded
                : BookCoverStatus.Found
            : source.Status;
        return new BookCoverDto
        {
            Id = source.Id,
            Status = effectiveStatus.ToString(),
            Source = source.Source?.ToString(),
            ImageUrl = source.StoragePath == null ? null : ApiRoutes.BookCoverFile(bookId, version),
            ThumbnailImageUrl =
                source.ThumbnailStoragePath == null ? null : ApiRoutes.BookCoverThumbnail(bookId, version),
            OriginalImageUrl = source.OriginalImageUrl,
            MimeType = source.MimeType,
            SizeBytes = source.SizeBytes,
            Width = source.Width,
            Height = source.Height,
            ThumbnailMimeType = source.ThumbnailMimeType,
            ThumbnailSizeBytes = source.ThumbnailSizeBytes,
            ThumbnailWidth = source.ThumbnailWidth,
            ThumbnailHeight = source.ThumbnailHeight,
            FailureReason = hasStoredCover ? null : source.FailureReason,
            LastAttemptAt = source.LastAttemptAt
        };
    }

    private static DateTimeOffset GetCoverVersion(BookCover source)
    {
        if (source.LastAttemptAt.HasValue)
        {
            return source.LastAttemptAt.Value;
        }

        if (source.LastModified != default)
        {
            return source.LastModified;
        }

        return source.Created != default ? source.Created : DateTimeOffset.UnixEpoch;
    }
}
