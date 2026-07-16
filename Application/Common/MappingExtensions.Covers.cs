namespace Application.Common;

using Application.Common.DTOs.Book;

public static partial class MappingExtensions
{
    public static BookCoverDto ToDto(this BookCover source, Guid bookId)
    {
        var version = GetCoverVersion(source).ToUnixTimeMilliseconds();
        return new BookCoverDto
        {
            Id = source.Id,
            Status = source.Status.ToString(),
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
            FailureReason = source.FailureReason,
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
