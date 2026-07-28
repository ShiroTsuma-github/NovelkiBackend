namespace Infrastructure.Persistence;

using Application.Common.DTOs.Book;

public sealed class BookSearchSuggestionQueryService : IBookSearchSuggestionQueryService
{
    private readonly ApplicationDbContext _context;

    public BookSearchSuggestionQueryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<IReadOnlyCollection<BookSearchSuggestionDto>> GetSuggestionsAsync(
        Guid ownerId,
        string field,
        string? search,
        int take,
        CancellationToken cancellationToken)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? string.Empty
            : MappingExtensions.NormalizeName(search);

        return field switch
        {
            BookSearchSuggestionFields.Author =>
                GetAuthorSuggestionsAsync(ownerId, normalizedSearch, take, cancellationToken),
            BookSearchSuggestionFields.Tag =>
                GetTagSuggestionsAsync(ownerId, normalizedSearch, take, cancellationToken),
            BookSearchSuggestionFields.Genre =>
                GetGenreSuggestionsAsync(ownerId, normalizedSearch, take, cancellationToken),
            BookSearchSuggestionFields.Status =>
                GetStatusSuggestionsAsync(ownerId, search, take, cancellationToken),
            BookSearchSuggestionFields.Type =>
                GetTypeSuggestionsAsync(ownerId, search, take, cancellationToken),
            _ => Task.FromResult<IReadOnlyCollection<BookSearchSuggestionDto>>([])
        };
    }

    private Task<IReadOnlyCollection<BookSearchSuggestionDto>> GetAuthorSuggestionsAsync(
        Guid ownerId,
        string normalizedSearch,
        int take,
        CancellationToken cancellationToken)
    {
        var books = _context.Books
            .AsNoTracking()
            .Where(book => book.OwnerId == ownerId && book.Author != null);
        if (normalizedSearch.Length > 0)
        {
            books = books.Where(book =>
                book.Author!.NormalizedPrimaryName.Contains(normalizedSearch) ||
                book.Author.Names.Any(name => name.NormalizedName.Contains(normalizedSearch)));
        }

        var rows = books
            .GroupBy(book => book.Author!.NormalizedPrimaryName)
            .Select(group => new SuggestionRow
            {
                Value = group.Min(book => book.Author!.PrimaryName)!,
                Count = group.Select(book => book.Id).Distinct().Count(),
                IsExact = normalizedSearch.Length > 0 &&
                          (group.Key == normalizedSearch ||
                           group.Any(book =>
                               book.Author!.Names.Any(name => name.NormalizedName == normalizedSearch)))
            });

        return ExecuteAsync(rows, take, cancellationToken);
    }

    private Task<IReadOnlyCollection<BookSearchSuggestionDto>> GetTagSuggestionsAsync(
        Guid ownerId,
        string normalizedSearch,
        int take,
        CancellationToken cancellationToken)
    {
        var tags = _context.Books
            .AsNoTracking()
            .Where(book => book.OwnerId == ownerId)
            .SelectMany(book => book.BookTags);
        if (normalizedSearch.Length > 0)
        {
            tags = tags.Where(bookTag => bookTag.Tag.NormalizedName.Contains(normalizedSearch));
        }

        var rows = tags
            .GroupBy(bookTag => bookTag.Tag.NormalizedName)
            .Select(group => new SuggestionRow
            {
                Value = group.Min(bookTag => bookTag.Tag.Name)!,
                Count = group.Select(bookTag => bookTag.BookId).Distinct().Count(),
                IsExact = normalizedSearch.Length > 0 && group.Key == normalizedSearch
            });

        return ExecuteAsync(rows, take, cancellationToken);
    }

    private Task<IReadOnlyCollection<BookSearchSuggestionDto>> GetGenreSuggestionsAsync(
        Guid ownerId,
        string normalizedSearch,
        int take,
        CancellationToken cancellationToken)
    {
        var genres = _context.Books
            .AsNoTracking()
            .Where(book => book.OwnerId == ownerId)
            .SelectMany(book => book.BookGenres);
        if (normalizedSearch.Length > 0)
        {
            genres = genres.Where(bookGenre => bookGenre.Genre.NormalizedName.Contains(normalizedSearch));
        }

        var rows = genres
            .GroupBy(bookGenre => bookGenre.Genre.NormalizedName)
            .Select(group => new SuggestionRow
            {
                Value = group.Min(bookGenre => bookGenre.Genre.Name)!,
                Count = group.Select(bookGenre => bookGenre.BookId).Distinct().Count(),
                IsExact = normalizedSearch.Length > 0 && group.Key == normalizedSearch
            });

        return ExecuteAsync(rows, take, cancellationToken);
    }

    private Task<IReadOnlyCollection<BookSearchSuggestionDto>> GetStatusSuggestionsAsync(
        Guid ownerId,
        string? search,
        int take,
        CancellationToken cancellationToken)
    {
        var normalizedSearch = search?.Trim().ToUpperInvariant() ?? string.Empty;
        var books = _context.Books
            .AsNoTracking()
            .Where(book => book.OwnerId == ownerId);
        if (normalizedSearch.Length > 0)
        {
            books = books.Where(book => book.Status.Name.ToUpper().Contains(normalizedSearch));
        }

        var rows = books
            .GroupBy(book => new { book.StatusId, book.Status.Name })
            .Select(group => new SuggestionRow
            {
                Value = group.Key.Name,
                Count = group.Count(),
                IsExact = normalizedSearch.Length > 0 && group.Key.Name.ToUpper() == normalizedSearch
            });

        return ExecuteAsync(rows, take, cancellationToken);
    }

    private Task<IReadOnlyCollection<BookSearchSuggestionDto>> GetTypeSuggestionsAsync(
        Guid ownerId,
        string? search,
        int take,
        CancellationToken cancellationToken)
    {
        var normalizedSearch = search?.Trim().ToUpperInvariant() ?? string.Empty;
        var books = _context.Books
            .AsNoTracking()
            .Where(book => book.OwnerId == ownerId);
        if (normalizedSearch.Length > 0)
        {
            books = books.Where(book => book.ContentType.Name.ToUpper().Contains(normalizedSearch));
        }

        var rows = books
            .GroupBy(book => new { book.ContentTypeId, book.ContentType.Name })
            .Select(group => new SuggestionRow
            {
                Value = group.Key.Name,
                Count = group.Count(),
                IsExact = normalizedSearch.Length > 0 && group.Key.Name.ToUpper() == normalizedSearch
            });

        return ExecuteAsync(rows, take, cancellationToken);
    }

    private static async Task<IReadOnlyCollection<BookSearchSuggestionDto>> ExecuteAsync(
        IQueryable<SuggestionRow> rows,
        int take,
        CancellationToken cancellationToken)
    {
        return await rows
            .OrderByDescending(row => row.IsExact)
            .ThenByDescending(row => row.Count)
            .ThenBy(row => row.Value)
            .Take(take)
            .Select(row => new BookSearchSuggestionDto(row.Value, row.Count, row.IsExact))
            .ToListAsync(cancellationToken);
    }

    private sealed class SuggestionRow
    {
        public required string Value { get; init; }
        public int Count { get; init; }
        public bool IsExact { get; init; }
    }
}
