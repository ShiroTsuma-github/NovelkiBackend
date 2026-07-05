namespace Application.Features.BookFeatures.Commands;

using Application.Common.DTOs.Book;
using Domain.Associations;

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
    string? Comment,
    string? Notes,
    string? RawImportedLine,
    IEnumerable<BookLinkInput>? Links) : IRequest
{
    [JsonIgnore]
    public bool AdminScope { get; set; }
}

public class UpdateBookHandler : IRequestHandler<UpdateBookCommand>
{
    private readonly IBookRepository _bookRepository;
    private readonly IAuthorRepository _authorRepository;
    private readonly ITypeRepository _typeRepository;
    private readonly IStatusRepository _statusRepository;
    private readonly IGenreRepository _genreRepository;
    private readonly ITagRepository _tagRepository;
    private readonly IUser _user;

    public UpdateBookHandler(
        IBookRepository bookRepository,
        IAuthorRepository authorRepository,
        ITypeRepository typeRepository,
        IStatusRepository statusRepository,
        IGenreRepository genreRepository,
        ITagRepository tagRepository,
        IUser user)
    {
        _bookRepository = bookRepository;
        _authorRepository = authorRepository;
        _typeRepository = typeRepository;
        _statusRepository = statusRepository;
        _genreRepository = genreRepository;
        _tagRepository = tagRepository;
        _user = user;
    }

    public async Task Handle(UpdateBookCommand request, CancellationToken cancellationToken)
    {
        var book = request.AdminScope
            ? await _bookRepository.GetForUpdateAsync(request.Id, cancellationToken)
            : await _bookRepository.GetForUpdateAsync(request.Id, _user.RequiredId, cancellationToken);
        if (book == null)
        {
            throw new EntityNotFoundException<Book, Guid>(request.Id);
        }

        var ownerId = book.OwnerId;

        await EnsureBookDoesNotExistAsync(ownerId, book.Id, request.PrimaryTitle, request.AlternativeTitles, cancellationToken);

        var contentType = await _typeRepository.GetByIdAsync(request.ContentTypeId, cancellationToken)
            ?? throw new EntityNotFoundException<ContentType, Guid>(request.ContentTypeId);
        var status = await _statusRepository.GetByIdAsync(request.StatusId, cancellationToken)
            ?? throw new EntityNotFoundException<Status, Guid>(request.StatusId);
        var author = await ResolveAuthorAsync(request, cancellationToken);

        var progressChanged = book.CurrentChapterNumber != request.CurrentChapterNumber ||
                              book.CurrentChapterLabel != request.CurrentChapterLabel;

        book.PrimaryTitle = request.PrimaryTitle;
        book.NormalizedPrimaryTitle = MappingExtensions.NormalizeName(request.PrimaryTitle);
        book.Description = request.Description;
        book.AuthorId = author?.Id;
        book.Author = author;
        book.ContentTypeId = contentType.Id;
        book.ContentType = contentType;
        book.StatusId = status.Id;
        book.Status = status;
        book.TotalChapters = request.TotalChapters;
        book.CurrentChapterNumber = request.CurrentChapterNumber;
        book.CurrentChapterLabel = request.CurrentChapterLabel;
        book.Rating = request.Rating;
        book.Priority = request.Priority;
        book.Comment = request.Comment;
        book.Notes = request.Notes;
        book.RawImportedLine = request.RawImportedLine;

        var titles = new List<BookTitle> { request.PrimaryTitle.ToPrimaryTitle() };
        foreach (var title in request.AlternativeTitles ?? Enumerable.Empty<BookTitleInput>())
        {
            if (!string.IsNullOrWhiteSpace(title.Title))
            {
                titles.Add(title.ToBookTitle());
            }
        }

        var links = new List<BookLink>();
        foreach (var link in request.Links ?? Enumerable.Empty<BookLinkInput>())
        {
            links.Add(link.ToBookLink());
        }

        var genreIds = (await _genreRepository.GetByIdsAsync(request.GenreIds ?? Enumerable.Empty<Guid>(), cancellationToken))
            .Select(g => g.Id)
            .ToList();
        var tagIds = (await ResolveTagsAsync(ownerId, request.Tags ?? Enumerable.Empty<string>(), cancellationToken))
            .Select(t => t.Id)
            .ToList();
        var progressHistory = progressChanged
            ? new BookProgressHistory
            {
                ChapterNumber = request.CurrentChapterNumber,
                ChapterLabel = request.CurrentChapterLabel,
                Comment = request.Comment
            }
            : null;

        await _bookRepository.ReplaceEditableCollectionsAsync(book.Id, titles, links, genreIds, tagIds, progressHistory, cancellationToken);

        await _bookRepository.SaveAsync(cancellationToken);
    }

    private async Task EnsureBookDoesNotExistAsync(
        Guid ownerId,
        Guid currentBookId,
        string primaryTitle,
        IEnumerable<BookTitleInput>? alternativeTitles,
        CancellationToken cancellationToken)
    {
        foreach (var title in EnumerateTitles(primaryTitle, alternativeTitles))
        {
            var existing = await _bookRepository.GetByNameAsync(title, ownerId, cancellationToken);
            if (existing != null && existing.Id != currentBookId)
            {
                throw new EntityAlreadyExistsException<Book, Guid>(title, existing.Id);
            }
        }
    }

    private async Task<Author?> ResolveAuthorAsync(UpdateBookCommand request, CancellationToken cancellationToken)
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

public record UpdateBookProgressCommand(Guid Id, decimal? CurrentChapterNumber, string? CurrentChapterLabel, string? Comment) : IRequest;

public class UpdateBookProgressHandler : IRequestHandler<UpdateBookProgressCommand>
{
    private readonly IBookRepository _repository;
    private readonly IUser _user;

    public UpdateBookProgressHandler(IBookRepository repository, IUser user)
    {
        _repository = repository;
        _user = user;
    }

    public async Task Handle(UpdateBookProgressCommand request, CancellationToken cancellationToken)
    {
        var updated = await _repository.UpdateProgressAsync(
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
    }
}
