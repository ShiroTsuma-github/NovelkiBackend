namespace Application.Features.BookFeatures.Commands;

using Application.Common.DTOs.Book;

public sealed record UploadBookCoverCommand(
    Guid BookId,
    Stream Content,
    string FileName,
    string? ContentType,
    long? Length) : IRequest<BookCoverDto>;

public sealed record SetBookCoverFromUrlCommand(Guid BookId, string ImageUrl) : IRequest<BookCoverDto>;

public class UploadBookCoverHandler : IRequestHandler<UploadBookCoverCommand, BookCoverDto>
{
    private readonly IBookRepository _bookRepository;
    private readonly IBookCoverRepository _coverRepository;
    private readonly IBookCoverStorage _storage;
    private readonly IUser _user;

    public UploadBookCoverHandler(
        IBookRepository bookRepository,
        IBookCoverRepository coverRepository,
        IBookCoverStorage storage,
        IUser user)
    {
        _bookRepository = bookRepository;
        _coverRepository = coverRepository;
        _storage = storage;
        _user = user;
    }

    public async Task<BookCoverDto> Handle(UploadBookCoverCommand request, CancellationToken cancellationToken)
    {
        if (request.Length is <= 0)
        {
            throw new ValidationException("Cover file is empty.");
        }

        var book = await _bookRepository.GetByIdAsync(request.BookId, _user.RequiredId, cancellationToken)
            ?? throw new EntityNotFoundException<Book, Guid>(request.BookId);
        var cover = book.Cover ?? new BookCover { BookId = book.Id, Book = book };
        var previousStoragePath = cover.StoragePath;
        var stored = await _storage.SaveAsync(book.OwnerId, book.Id, request.Content, request.FileName, request.ContentType, cancellationToken);

        cover.Status = BookCoverStatus.Uploaded;
        cover.Source = BookCoverSource.ManualUpload;
        cover.StoragePath = stored.StoragePath;
        cover.OriginalImageUrl = null;
        cover.MimeType = stored.MimeType;
        cover.SizeBytes = stored.SizeBytes;
        cover.Width = stored.Width;
        cover.Height = stored.Height;
        cover.FailureReason = null;
        cover.LastAttemptAt = DateTimeOffset.UtcNow;

        if (book.Cover == null)
        {
            await _coverRepository.AddAsync(cover, cancellationToken);
        }
        else
        {
            await _coverRepository.SaveAsync(cancellationToken);
        }

        if (!string.Equals(previousStoragePath, stored.StoragePath, StringComparison.OrdinalIgnoreCase))
        {
            await _storage.DeleteIfExistsAsync(previousStoragePath, cancellationToken);
        }

        return cover.ToDto(book.Id);
    }
}

public class SetBookCoverFromUrlHandler : IRequestHandler<SetBookCoverFromUrlCommand, BookCoverDto>
{
    private readonly IBookRepository _bookRepository;
    private readonly IBookCoverRepository _coverRepository;
    private readonly IBookCoverStorage _storage;
    private readonly IBookCoverRemoteImageService _remoteImageService;
    private readonly IUser _user;

    public SetBookCoverFromUrlHandler(
        IBookRepository bookRepository,
        IBookCoverRepository coverRepository,
        IBookCoverStorage storage,
        IBookCoverRemoteImageService remoteImageService,
        IUser user)
    {
        _bookRepository = bookRepository;
        _coverRepository = coverRepository;
        _storage = storage;
        _remoteImageService = remoteImageService;
        _user = user;
    }

    public async Task<BookCoverDto> Handle(SetBookCoverFromUrlCommand request, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(request.ImageUrl, UriKind.Absolute, out var imageUri) ||
            imageUri.Scheme is not ("http" or "https"))
        {
            throw new ValidationException("Image URL must be an absolute HTTP or HTTPS URL.");
        }

        var book = await _bookRepository.GetByIdAsync(request.BookId, _user.RequiredId, cancellationToken)
            ?? throw new EntityNotFoundException<Book, Guid>(request.BookId);
        var cover = book.Cover ?? new BookCover { BookId = book.Id, Book = book };
        var previousStoragePath = cover.StoragePath;

        var stored = await _remoteImageService.SaveFromUrlAsync(book.OwnerId, book.Id, request.ImageUrl, cancellationToken);

        cover.Status = BookCoverStatus.Uploaded;
        cover.Source = BookCoverSource.ManualUrl;
        cover.StoragePath = stored.StoragePath;
        cover.OriginalImageUrl = request.ImageUrl;
        cover.MimeType = stored.MimeType;
        cover.SizeBytes = stored.SizeBytes;
        cover.Width = stored.Width;
        cover.Height = stored.Height;
        cover.FailureReason = null;
        cover.LastAttemptAt = DateTimeOffset.UtcNow;

        if (book.Cover == null)
        {
            await _coverRepository.AddAsync(cover, cancellationToken);
        }
        else
        {
            await _coverRepository.SaveAsync(cancellationToken);
        }

        if (!string.Equals(previousStoragePath, stored.StoragePath, StringComparison.OrdinalIgnoreCase))
        {
            await _storage.DeleteIfExistsAsync(previousStoragePath, cancellationToken);
        }

        return cover.ToDto(book.Id);
    }
}

public sealed record RefreshBookCoverCommand(Guid BookId) : IRequest<BookCoverDto>;

public class RefreshBookCoverHandler : IRequestHandler<RefreshBookCoverCommand, BookCoverDto>
{
    private readonly IBookRepository _bookRepository;
    private readonly IBookCoverRepository _coverRepository;
    private readonly IBookCoverQueue _queue;
    private readonly IBookCoverStorage _storage;
    private readonly IUser _user;

    public RefreshBookCoverHandler(
        IBookRepository bookRepository,
        IBookCoverRepository coverRepository,
        IBookCoverQueue queue,
        IBookCoverStorage storage,
        IUser user)
    {
        _bookRepository = bookRepository;
        _coverRepository = coverRepository;
        _queue = queue;
        _storage = storage;
        _user = user;
    }

    public async Task<BookCoverDto> Handle(RefreshBookCoverCommand request, CancellationToken cancellationToken)
    {
        var book = await _bookRepository.GetByIdAsync(request.BookId, _user.RequiredId, cancellationToken)
            ?? throw new EntityNotFoundException<Book, Guid>(request.BookId);
        var cover = book.Cover ?? new BookCover { BookId = book.Id, Book = book };
        var previousStoragePath = cover.StoragePath;

        cover.Status = BookCoverStatus.Pending;
        cover.Source = null;
        cover.StoragePath = null;
        cover.OriginalImageUrl = null;
        cover.MimeType = null;
        cover.SizeBytes = null;
        cover.Width = null;
        cover.Height = null;
        cover.FailureReason = null;
        cover.LastAttemptAt = DateTimeOffset.UtcNow;

        if (book.Cover == null)
        {
            await _coverRepository.AddAsync(cover, cancellationToken);
        }
        else
        {
            await _coverRepository.SaveAsync(cancellationToken);
        }

        await _storage.DeleteIfExistsAsync(previousStoragePath, cancellationToken);
        await _queue.QueueAsync(book.Id, cancellationToken);
        return cover.ToDto(book.Id);
    }
}

public sealed record GetBookCoverFileQuery(Guid BookId) : IRequest<BookCoverFileResult>;

public class GetBookCoverFileHandler : IRequestHandler<GetBookCoverFileQuery, BookCoverFileResult>
{
    private readonly IBookCoverRepository _coverRepository;
    private readonly IBookCoverStorage _storage;
    private readonly IUser _user;

    public GetBookCoverFileHandler(IBookCoverRepository coverRepository, IBookCoverStorage storage, IUser user)
    {
        _coverRepository = coverRepository;
        _storage = storage;
        _user = user;
    }

    public async Task<BookCoverFileResult> Handle(GetBookCoverFileQuery request, CancellationToken cancellationToken)
    {
        var cover = await _coverRepository.GetByBookIdAsync(request.BookId, _user.RequiredId, cancellationToken)
            ?? throw new EntityNotFoundException<Book, Guid>(request.BookId);
        if (cover.StoragePath == null || cover.MimeType == null)
        {
            throw new EntityNotFoundException<BookCover, Guid>(request.BookId);
        }

        var stream = await _storage.OpenReadAsync(cover.StoragePath, cancellationToken);
        return new BookCoverFileResult(stream, cover.MimeType, $"{request.BookId}{Path.GetExtension(cover.StoragePath)}");
    }
}
