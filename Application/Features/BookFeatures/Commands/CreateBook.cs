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
    string? Comment,
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
        var ownerId = _user.RequiredId;
        await EnsureBookDoesNotExistAsync(ownerId, request.PrimaryTitle, request.AlternativeTitles, cancellationToken);

        var contentType = await _typeRepository.GetByIdAsync(request.ContentTypeId, cancellationToken)
            ?? throw new EntityNotFoundException<ContentType, Guid>(request.ContentTypeId);
        var status = await _statusRepository.GetByIdAsync(request.StatusId, cancellationToken)
            ?? throw new EntityNotFoundException<Status, Guid>(request.StatusId);

        var author = await ResolveAuthorAsync(request, cancellationToken);
        var book = new Book
        {
            PrimaryTitle = request.PrimaryTitle,
            NormalizedPrimaryTitle = MappingExtensions.NormalizeName(request.PrimaryTitle),
            Description = request.Description,
            OwnerId = ownerId,
            AuthorId = author?.Id,
            Author = author,
            ContentTypeId = contentType.Id,
            ContentType = contentType,
            StatusId = status.Id,
            Status = status,
            TotalChapters = request.TotalChapters,
            CurrentChapterNumber = request.CurrentChapterNumber,
            CurrentChapterLabel = request.CurrentChapterLabel,
            Rating = request.Rating,
            Priority = request.Priority,
            Comment = request.Comment,
            Notes = request.Notes,
            RawImportedLine = request.RawImportedLine,
            Cover = new BookCover()
        };

        book.Titles.Add(request.PrimaryTitle.ToPrimaryTitle());
        foreach (var title in request.AlternativeTitles ?? Enumerable.Empty<BookTitleInput>())
        {
            if (!string.IsNullOrWhiteSpace(title.Title))
            {
                book.Titles.Add(title.ToBookTitle());
            }
        }

        foreach (var link in request.Links ?? Enumerable.Empty<BookLinkInput>())
        {
            book.Links.Add(link.ToBookLink());
        }

        foreach (var genre in await _genreRepository.GetByIdsAsync(request.GenreIds ?? Enumerable.Empty<Guid>(), cancellationToken))
        {
            book.BookGenres.Add(new BookGenre { Book = book, Genre = genre });
        }

        foreach (var tag in await ResolveTagsAsync(ownerId, request.Tags ?? Enumerable.Empty<string>(), cancellationToken))
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

    private async Task EnsureBookDoesNotExistAsync(
        Guid ownerId,
        string primaryTitle,
        IEnumerable<BookTitleInput>? alternativeTitles,
        CancellationToken cancellationToken)
    {
        foreach (var title in EnumerateTitles(primaryTitle, alternativeTitles))
        {
            var existing = await _bookRepository.GetByNameAsync(title, ownerId, cancellationToken);
            if (existing != null)
            {
                throw new EntityAlreadyExistsException<Book, Guid>(title, existing.Id);
            }
        }
    }

    private async Task<Author?> ResolveAuthorAsync(CreateBookCommand request, CancellationToken cancellationToken)
    {
        if (request.AuthorId.HasValue)
        {
            return await _authorRepository.GetByIdAsync(request.AuthorId.Value, cancellationToken)
                ?? throw new EntityNotFoundException<Author, Guid>(request.AuthorId.Value);
        }

        if (string.IsNullOrWhiteSpace(request.AuthorName))
        {
            return null;
        }

        var existing = await _authorRepository.GetByNameAsync(request.AuthorName, cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        var author = new Author
        {
            PrimaryName = request.AuthorName,
            NormalizedPrimaryName = MappingExtensions.NormalizeName(request.AuthorName)
        };
        author.Names.Add(new AuthorName
        {
            Name = request.AuthorName,
            NormalizedName = MappingExtensions.NormalizeName(request.AuthorName),
            IsPrimary = true,
            Source = "Manual"
        });
        await _authorRepository.AddAsync(author, cancellationToken);
        return author;
    }

    private async Task<IEnumerable<Tag>> ResolveTagsAsync(Guid ownerId, IEnumerable<string> tagNames, CancellationToken cancellationToken)
    {
        var names = tagNames.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var existingTags = (await _tagRepository.GetByNamesAsync(ownerId, names, cancellationToken)).ToList();
        var existingNormalized = existingTags.Select(t => t.NormalizedName).ToHashSet();
        foreach (var name in names)
        {
            var normalized = MappingExtensions.NormalizeName(name);
            if (!existingNormalized.Contains(normalized))
            {
                var tag = new Tag
                {
                    OwnerId = ownerId,
                    Name = name,
                    NormalizedName = normalized
                };
                await _tagRepository.AddAsync(tag, cancellationToken);
                existingTags.Add(tag);
            }
        }

        return existingTags;
    }

    private static IEnumerable<string> EnumerateTitles(string primaryTitle, IEnumerable<BookTitleInput>? alternativeTitles)
    {
        yield return primaryTitle;

        foreach (var title in alternativeTitles ?? Enumerable.Empty<BookTitleInput>())
        {
            if (!string.IsNullOrWhiteSpace(title.Title))
            {
                yield return title.Title;
            }
        }
    }
}
