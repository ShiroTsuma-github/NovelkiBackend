namespace Application.Features.BookFeatures.Commands;

using Application.Common.DTOs.Book;
using Domain.Associations;
using FluentValidation;

public sealed record UpdateBookCommand(
    Guid Id,
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
    IEnumerable<BookLinkInput>? Links) : IRequest
{
    [JsonIgnore] public bool AdminScope { get; set; }
}

public class UpdateBookHandler : IRequestHandler<UpdateBookCommand>
{
    private readonly IBookRepository _bookRepository;
    private readonly IAuthorRepository _authorRepository;
    private readonly ITypeRepository _typeRepository;
    private readonly IStatusRepository _statusRepository;
    private readonly IGenreRepository _genreRepository;
    private readonly ITagRepository _tagRepository;
    private readonly IBookListCacheInvalidator _cacheInvalidator;
    private readonly IUser _user;

    public UpdateBookHandler(
        IBookRepository bookRepository,
        IAuthorRepository authorRepository,
        ITypeRepository typeRepository,
        IStatusRepository statusRepository,
        IGenreRepository genreRepository,
        ITagRepository tagRepository,
        IBookListCacheInvalidator cacheInvalidator,
        IUser user)
    {
        _bookRepository = bookRepository;
        _authorRepository = authorRepository;
        _typeRepository = typeRepository;
        _statusRepository = statusRepository;
        _genreRepository = genreRepository;
        _tagRepository = tagRepository;
        _cacheInvalidator = cacheInvalidator;
        _user = user;
    }

    public async Task Handle(UpdateBookCommand request, CancellationToken cancellationToken)
    {
        Book? book = request.AdminScope
            ? await _bookRepository.GetForUpdateAsync(request.Id, cancellationToken)
            : await _bookRepository.GetForUpdateAsync(request.Id, _user.RequiredId, cancellationToken);
        if (book == null)
        {
            throw new EntityNotFoundException<Book, Guid>(request.Id);
        }

        Guid ownerId = book.OwnerId;

        ContentType contentType = await _typeRepository.GetByIdAsync(request.ContentTypeId, cancellationToken)
                                  ?? throw new EntityNotFoundException<ContentType, Guid>(request.ContentTypeId);
        Status status = await _statusRepository.GetByIdAsync(request.StatusId, cancellationToken)
                        ?? throw new EntityNotFoundException<Status, Guid>(request.StatusId);
        await BookMutationSupport.EnsureBookDoesNotExistAsync(
            _bookRepository,
            ownerId,
            book.Id,
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

        bool progressChanged = book.CurrentChapterNumber != request.CurrentChapterNumber ||
                               book.CurrentChapterLabel != currentChapterLabel;

        book.PrimaryTitle = primaryTitle;
        book.NormalizedPrimaryTitle = MappingExtensions.NormalizeName(primaryTitle);
        book.Description = description;
        book.AuthorId = author?.Id;
        book.Author = author;
        book.ContentTypeId = contentType.Id;
        book.ContentType = contentType;
        book.StatusId = status.Id;
        book.Status = status;
        book.TotalChapters = request.TotalChapters;
        book.CurrentChapterNumber = request.CurrentChapterNumber;
        book.CurrentChapterLabel = currentChapterLabel;
        book.Rating = request.Rating;
        book.Priority = request.Priority;
        book.Notes = notes;
        book.RawImportedLine = rawImportedLine;

        List<BookTitle> titles = BookMutationSupport.BuildTitles(primaryTitle, request.AlternativeTitles);
        List<BookLink> links = BookMutationSupport.BuildLinks(request.Links);

        var genreIds =
            (await _genreRepository.GetByIdsAsync(request.GenreIds ?? Enumerable.Empty<Guid>(), cancellationToken))
            .Select(g => g.Id)
            .ToList();
        var tagIds = (await BookMutationSupport.ResolveTagsAsync(_tagRepository, ownerId,
                request.Tags ?? Enumerable.Empty<string>(), cancellationToken))
            .Select(t => t.Id)
            .ToList();
        BookProgressHistory? progressHistory = progressChanged
            ? new BookProgressHistory
            {
                ChapterNumber = request.CurrentChapterNumber, ChapterLabel = currentChapterLabel
            }
            : null;

        await _bookRepository.ReplaceEditableCollectionsAsync(book.Id, titles, links, genreIds, tagIds, progressHistory,
            cancellationToken);

        await _bookRepository.SaveAsync(cancellationToken);
        await _cacheInvalidator.InvalidateBooksAsync(ownerId, cancellationToken);
    }
}

public record UpdateBookProgressCommand(
    Guid Id,
    decimal? CurrentChapterNumber,
    string? CurrentChapterLabel,
    string? Comment) : IRequest;

public class UpdateBookProgressHandler : IRequestHandler<UpdateBookProgressCommand>
{
    private readonly IBookRepository _repository;
    private readonly IBookListCacheInvalidator _cacheInvalidator;
    private readonly IUser _user;

    public UpdateBookProgressHandler(IBookRepository repository, IBookListCacheInvalidator cacheInvalidator, IUser user)
    {
        _repository = repository;
        _cacheInvalidator = cacheInvalidator;
        _user = user;
    }

    public async Task Handle(UpdateBookProgressCommand request, CancellationToken cancellationToken)
    {
        Book book = await _repository.GetByIdAsync(request.Id, _user.RequiredId, cancellationToken)
                    ?? throw new EntityNotFoundException<Book, Guid>(request.Id);
        if (book.TotalChapters.HasValue && book.TotalChapters > 0 && request.CurrentChapterNumber.HasValue &&
            request.CurrentChapterNumber > book.TotalChapters)
        {
            throw new ValidationException("Current chapter cannot be greater than total chapters.");
        }

        bool updated = await _repository.UpdateProgressAsync(
            request.Id,
            _user.RequiredId,
            request.CurrentChapterNumber,
            request.CurrentChapterLabel,
            request.Comment,
            cancellationToken);
        if (!updated)
        {
            throw new EntityNotFoundException<Book, Guid>(request.Id);
        }

        await _cacheInvalidator.InvalidateBooksAsync(_user.RequiredId, cancellationToken);
    }
}
