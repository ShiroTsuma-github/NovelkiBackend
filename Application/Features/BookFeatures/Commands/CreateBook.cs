namespace Application.Features.BookFeatures.Commands;

using Application.Common.DTOs.Book;
using Domain.Associations;

public sealed record CreateBookCommand(
    string PrimaryTitle,
    Guid ContentTypeId,
    Guid StatusId,
    Guid? AuthorId,
    string? AuthorName,
    IEnumerable<BookTitleInput>? AlternativeTitles,
    IEnumerable<Guid>? GenreIds,
    IEnumerable<string>? Tags,
    decimal? TotalChapters,
    decimal? CurrentChapterNumber,
    string? CurrentChapterLabel,
    int? Rating,
    int? Priority,
    string? Description,
    string? Notes,
    string? RawImportedLine,
    IEnumerable<BookLinkInput>? Links) : IRequest<Guid>;

public class CreateBookHandler : IRequestHandler<CreateBookCommand, Guid>
{
    private readonly IBookRepository _bookRepository;
    private readonly IAuthorRepository _authorRepository;
    private readonly ITypeRepository _typeRepository;
    private readonly IStatusRepository _statusRepository;
    private readonly IGenreRepository _genreRepository;
    private readonly ITagRepository _tagRepository;
    private readonly IBookCoverQueue _bookCoverQueue;
    private readonly IBookListCacheInvalidator _cacheInvalidator;
    private readonly IUser _user;

    public CreateBookHandler(
        IBookRepository bookRepository,
        IAuthorRepository authorRepository,
        ITypeRepository typeRepository,
        IStatusRepository statusRepository,
        IGenreRepository genreRepository,
        ITagRepository tagRepository,
        IBookCoverQueue bookCoverQueue,
        IBookListCacheInvalidator cacheInvalidator,
        IUser user)
    {
        _bookRepository = bookRepository;
        _authorRepository = authorRepository;
        _typeRepository = typeRepository;
        _statusRepository = statusRepository;
        _genreRepository = genreRepository;
        _tagRepository = tagRepository;
        _bookCoverQueue = bookCoverQueue;
        _cacheInvalidator = cacheInvalidator;
        _user = user;
    }

    public async Task<Guid> Handle(CreateBookCommand request, CancellationToken cancellationToken)
    {
        Guid ownerId = _user.RequiredId;
        ContentType contentType = await _typeRepository.GetByIdAsync(request.ContentTypeId, cancellationToken)
                                  ?? throw new EntityNotFoundException<ContentType, Guid>(request.ContentTypeId);
        Status status = await _statusRepository.GetByIdAsync(request.StatusId, cancellationToken)
                        ?? throw new EntityNotFoundException<Status, Guid>(request.StatusId);
        await BookMutationSupport.EnsureBookDoesNotExistAsync(
            _bookRepository,
            ownerId,
            null,
            contentType.Id,
            request.PrimaryTitle,
            request.AlternativeTitles,
            cancellationToken);

        Author? author = await BookMutationSupport.ResolveAuthorAsync(_authorRepository, request.AuthorId,
            request.AuthorName, cancellationToken);
        string primaryTitle = request.PrimaryTitle.Trim();
        string? description = BookMutationSupport.TrimToNull(request.Description);
        string? currentChapterLabel = BookMutationSupport.TrimToNull(request.CurrentChapterLabel);
        string? notes = BookMutationSupport.TrimToNull(request.Notes);
        string? rawImportedLine = BookMutationSupport.TrimToNull(request.RawImportedLine);
        var book = new Book
        {
            PrimaryTitle = primaryTitle,
            NormalizedPrimaryTitle = MappingExtensions.NormalizeName(primaryTitle),
            Description = description,
            OwnerId = ownerId,
            AuthorId = author?.Id,
            Author = author,
            ContentTypeId = contentType.Id,
            ContentType = contentType,
            StatusId = status.Id,
            Status = status,
            TotalChapters = request.TotalChapters,
            CurrentChapterNumber = request.CurrentChapterNumber,
            CurrentChapterLabel = currentChapterLabel,
            Rating = request.Rating,
            Priority = request.Priority,
            Notes = notes,
            RawImportedLine = rawImportedLine,
            Cover = new BookCover()
        };

        foreach (BookTitle title in BookMutationSupport.BuildTitles(primaryTitle, request.AlternativeTitles))
        {
            book.Titles.Add(title);
        }

        foreach (BookLink link in BookMutationSupport.BuildLinks(request.Links))
        {
            book.Links.Add(link);
        }

        foreach (Genre genre in await _genreRepository.GetByIdsAsync(request.GenreIds ?? Enumerable.Empty<Guid>(),
                     cancellationToken))
        {
            book.BookGenres.Add(new BookGenre { Book = book, Genre = genre });
        }

        foreach (Tag tag in await BookMutationSupport.ResolveTagsAsync(_tagRepository, ownerId,
                     request.Tags ?? Enumerable.Empty<string>(), cancellationToken))
        {
            book.BookTags.Add(new BookTag { Book = book, Tag = tag });
        }

        if (request.CurrentChapterNumber != null || !string.IsNullOrWhiteSpace(request.CurrentChapterLabel))
        {
            book.ProgressHistory.Add(new BookProgressHistory
            {
                ChapterNumber = request.CurrentChapterNumber,
                ChapterLabel = request.CurrentChapterLabel,
                Comment = "Initial progress"
            });
        }

        await _bookRepository.AddAsync(book, cancellationToken);
        await _bookCoverQueue.QueueAsync(book.Id, cancellationToken);
        await _cacheInvalidator.InvalidateBooksAsync(ownerId, cancellationToken);
        return book.Id;
    }
}
