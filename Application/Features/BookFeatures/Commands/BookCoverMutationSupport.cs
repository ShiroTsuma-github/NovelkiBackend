namespace Application.Features.BookFeatures.Commands;

internal static class BookCoverMutationSupport
{
    public static BookCoverChange ApplyStoredCover(
        Book book,
        BookCoverStoredFiles stored,
        BookCoverSource source,
        string? originalImageUrl)
    {
        var change = PrepareCover(book);
        var cover = change.Cover;

        cover.Status = BookCoverStatus.Uploaded;
        cover.Source = source;
        cover.StoragePath = stored.Original.StoragePath;
        cover.ThumbnailStoragePath = stored.Thumbnail.StoragePath;
        cover.OriginalImageUrl = originalImageUrl;
        cover.MimeType = stored.Original.MimeType;
        cover.ThumbnailMimeType = stored.Thumbnail.MimeType;
        cover.SizeBytes = stored.Original.SizeBytes;
        cover.ThumbnailSizeBytes = stored.Thumbnail.SizeBytes;
        cover.Width = stored.Original.Width;
        cover.Height = stored.Original.Height;
        cover.ThumbnailWidth = stored.Thumbnail.Width;
        cover.ThumbnailHeight = stored.Thumbnail.Height;
        cover.FailureReason = null;
        cover.LastAttemptAt = DateTimeOffset.UtcNow;
        BookCoverLinkHelper.TouchBook(book);

        return change;
    }

    public static BookCoverChange ApplyPendingRefresh(Book book)
    {
        var change = PrepareCover(book);
        var cover = change.Cover;

        cover.Status = BookCoverStatus.Pending;
        cover.Source = null;
        cover.StoragePath = null;
        cover.ThumbnailStoragePath = null;
        cover.OriginalImageUrl = null;
        cover.MimeType = null;
        cover.ThumbnailMimeType = null;
        cover.SizeBytes = null;
        cover.ThumbnailSizeBytes = null;
        cover.Width = null;
        cover.Height = null;
        cover.ThumbnailWidth = null;
        cover.ThumbnailHeight = null;
        cover.FailureReason = null;
        cover.LastAttemptAt = DateTimeOffset.UtcNow;
        BookCoverLinkHelper.TouchBook(book);

        return change;
    }

    public static async Task SaveAsync(
        BookCoverChange change,
        IBookRepository bookRepository,
        IBookCoverRepository coverRepository,
        CancellationToken cancellationToken)
    {
        if (change.HadExistingCover)
        {
            await bookRepository.SaveAsync(cancellationToken);
        }
        else
        {
            await coverRepository.AddAsync(change.Cover, cancellationToken);
        }
    }

    public static async Task DeletePreviousFilesIfChangedAsync(
        BookCoverChange change,
        BookCoverStoredFiles stored,
        IBookCoverStorage storage,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(change.PreviousStoragePath, stored.Original.StoragePath, StringComparison.OrdinalIgnoreCase))
        {
            await storage.DeleteIfExistsAsync(change.PreviousStoragePath, cancellationToken);
        }

        if (!string.Equals(change.PreviousThumbnailStoragePath, stored.Thumbnail.StoragePath,
                StringComparison.OrdinalIgnoreCase))
        {
            await storage.DeleteIfExistsAsync(change.PreviousThumbnailStoragePath, cancellationToken);
        }
    }

    private static BookCoverChange PrepareCover(Book book)
    {
        var hadExistingCover = book.Cover is not null;
        book.Cover ??= new BookCover { BookId = book.Id, Book = book };
        return new BookCoverChange(
            book.Cover,
            hadExistingCover,
            book.Cover.StoragePath,
            book.Cover.ThumbnailStoragePath);
    }
}

internal sealed record BookCoverChange(
    BookCover Cover,
    bool HadExistingCover,
    string? PreviousStoragePath,
    string? PreviousThumbnailStoragePath);
