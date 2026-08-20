namespace Infrastructure.Persistence;

using Application.Common.DTOs.Book;
using Domain.Entities;
using Domain.Repositories;

public sealed class BookSearchSuggestionQueryService : IBookSearchSuggestionQueryService
{
    private readonly ApplicationDbContext _context;
    private readonly BookSearchCriteriaApplier _criteriaApplier;

    public BookSearchSuggestionQueryService(ApplicationDbContext context)
    {
        _context = context;
        _criteriaApplier = new BookSearchCriteriaApplier(context);
    }

    public Task<IReadOnlyCollection<BookSearchSuggestionDto>> GetSuggestionsAsync(
        Guid ownerId,
        string field,
        string? search,
        BookSearchCriteria? criteria,
        int take,
        CancellationToken cancellationToken)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? string.Empty
            : MappingExtensions.NormalizeName(search);

        return field switch
        {
            BookSearchSuggestionFields.Author =>
                GetAuthorSuggestionsAsync(ownerId, normalizedSearch, criteria, take, cancellationToken),
            BookSearchSuggestionFields.Tag =>
                GetTagSuggestionsAsync(ownerId, normalizedSearch, criteria, take, cancellationToken),
            BookSearchSuggestionFields.Genre =>
                GetGenreSuggestionsAsync(ownerId, normalizedSearch, criteria, take, cancellationToken),
            BookSearchSuggestionFields.Status =>
                GetStatusSuggestionsAsync(ownerId, search, criteria, take, cancellationToken),
            BookSearchSuggestionFields.Type =>
                GetTypeSuggestionsAsync(ownerId, search, criteria, take, cancellationToken),
            _ => Task.FromResult<IReadOnlyCollection<BookSearchSuggestionDto>>([])
        };
    }

    private IQueryable<Book> GetScopedBooks(Guid ownerId, BookSearchCriteria? criteria)
    {
        var books = _context.Books
            .AsNoTracking()
            .Where(book => book.OwnerId == ownerId);

        return criteria?.HasFilters == true
            ? _criteriaApplier.Apply(books, criteria)
            : books;
    }

    private Task<IReadOnlyCollection<BookSearchSuggestionDto>> GetAuthorSuggestionsAsync(
        Guid ownerId,
        string normalizedSearch,
        BookSearchCriteria? criteria,
        int take,
        CancellationToken cancellationToken)
    {
        var scoped = criteria?.HasFilters == true;
        var scopedBookIds = GetScopedBooks(ownerId, criteria).Select(book => book.Id);
        var books = GetOwnerBooks(ownerId)
            .Where(book => book.Author != null);
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
                Key = group.Key,
                Value = group.Min(book => book.Author!.PrimaryName)!,
                Count = scoped
                    ? group.Where(book => scopedBookIds.Contains(book.Id)).Select(book => book.Id).Distinct().Count()
                    : group.Select(book => book.Id).Distinct().Count(),
                IsExact = normalizedSearch.Length > 0 &&
                          (group.Key == normalizedSearch ||
                           group.Any(book =>
                               book.Author!.Names.Any(name => name.NormalizedName == normalizedSearch)))
            });
        return ExecuteAsync(rows, scoped, take, cancellationToken);
    }

    private Task<IReadOnlyCollection<BookSearchSuggestionDto>> GetTagSuggestionsAsync(
        Guid ownerId,
        string normalizedSearch,
        BookSearchCriteria? criteria,
        int take,
        CancellationToken cancellationToken)
    {
        var scoped = criteria?.HasFilters == true;
        var scopedBookIds = GetScopedBooks(ownerId, criteria).Select(book => book.Id);
        var tags = GetOwnerBooks(ownerId)
            .SelectMany(book => book.BookTags);
        if (normalizedSearch.Length > 0)
        {
            tags = tags.Where(bookTag => bookTag.Tag.NormalizedName.Contains(normalizedSearch));
        }

        var rows = tags
            .GroupBy(bookTag => bookTag.Tag.NormalizedName)
            .Select(group => new SuggestionRow
            {
                Key = group.Key,
                Value = group.Min(bookTag => bookTag.Tag.Name)!,
                Count = scoped
                    ? group.Where(bookTag => scopedBookIds.Contains(bookTag.BookId)).Select(bookTag => bookTag.BookId).Distinct().Count()
                    : group.Select(bookTag => bookTag.BookId).Distinct().Count(),
                IsExact = normalizedSearch.Length > 0 && group.Key == normalizedSearch
            });
        return ExecuteAsync(rows, scoped, take, cancellationToken);
    }

    private Task<IReadOnlyCollection<BookSearchSuggestionDto>> GetGenreSuggestionsAsync(
        Guid ownerId,
        string normalizedSearch,
        BookSearchCriteria? criteria,
        int take,
        CancellationToken cancellationToken)
    {
        var scoped = criteria?.HasFilters == true;
        var scopedBookIds = GetScopedBooks(ownerId, criteria).Select(book => book.Id);
        var genres = GetOwnerBooks(ownerId)
            .SelectMany(book => book.BookGenres);
        if (normalizedSearch.Length > 0)
        {
            genres = genres.Where(bookGenre => bookGenre.Genre.NormalizedName.Contains(normalizedSearch));
        }

        var rows = genres
            .GroupBy(bookGenre => bookGenre.Genre.NormalizedName)
            .Select(group => new SuggestionRow
            {
                Key = group.Key,
                Value = group.Min(bookGenre => bookGenre.Genre.Name)!,
                Count = scoped
                    ? group.Where(bookGenre => scopedBookIds.Contains(bookGenre.BookId)).Select(bookGenre => bookGenre.BookId).Distinct().Count()
                    : group.Select(bookGenre => bookGenre.BookId).Distinct().Count(),
                IsExact = normalizedSearch.Length > 0 && group.Key == normalizedSearch
            });
        return ExecuteAsync(rows, scoped, take, cancellationToken);
    }

    private Task<IReadOnlyCollection<BookSearchSuggestionDto>> GetStatusSuggestionsAsync(
        Guid ownerId,
        string? search,
        BookSearchCriteria? criteria,
        int take,
        CancellationToken cancellationToken)
    {
        var normalizedSearch = search?.Trim().ToUpperInvariant() ?? string.Empty;
        var scoped = criteria?.HasFilters == true;
        var scopedBookIds = GetScopedBooks(ownerId, criteria).Select(book => book.Id);
        var books = GetOwnerBooks(ownerId);
        if (normalizedSearch.Length > 0)
        {
            books = books.Where(book => book.Status.Name.ToUpper().Contains(normalizedSearch));
        }

        var rows = books
            .GroupBy(book => new { book.StatusId, book.Status.Name })
            .Select(group => new SuggestionRow
            {
                Key = group.Key.Name.ToUpper(),
                Value = group.Key.Name,
                Count = scoped
                    ? group.Count(book => scopedBookIds.Contains(book.Id))
                    : group.Count(),
                IsExact = normalizedSearch.Length > 0 && group.Key.Name.ToUpper() == normalizedSearch
            });
        return ExecuteAsync(rows, scoped, take, cancellationToken);
    }

    private Task<IReadOnlyCollection<BookSearchSuggestionDto>> GetTypeSuggestionsAsync(
        Guid ownerId,
        string? search,
        BookSearchCriteria? criteria,
        int take,
        CancellationToken cancellationToken)
    {
        var normalizedSearch = search?.Trim().ToUpperInvariant() ?? string.Empty;
        var scoped = criteria?.HasFilters == true;
        var scopedBookIds = GetScopedBooks(ownerId, criteria).Select(book => book.Id);
        var books = GetOwnerBooks(ownerId);
        if (normalizedSearch.Length > 0)
        {
            books = books.Where(book => book.ContentType.Name.ToUpper().Contains(normalizedSearch));
        }

        var rows = books
            .GroupBy(book => new { book.ContentTypeId, book.ContentType.Name })
            .Select(group => new SuggestionRow
            {
                Key = group.Key.Name.ToUpper(),
                Value = group.Key.Name,
                Count = scoped
                    ? group.Count(book => scopedBookIds.Contains(book.Id))
                    : group.Count(),
                IsExact = normalizedSearch.Length > 0 && group.Key.Name.ToUpper() == normalizedSearch
            });
        return ExecuteAsync(rows, scoped, take, cancellationToken);
    }

    private IQueryable<Book> GetOwnerBooks(Guid ownerId)
    {
        return _context.Books
            .AsNoTracking()
            .Where(book => book.OwnerId == ownerId);
    }

    private static async Task<IReadOnlyCollection<BookSearchSuggestionDto>> ExecuteAsync(
        IQueryable<SuggestionRow> rows,
        bool scoped,
        int take,
        CancellationToken cancellationToken)
    {
        return await rows
            .OrderByDescending(row => row.IsExact)
            .ThenByDescending(row => !scoped || row.Count > 0)
            .ThenByDescending(row => row.Count)
            .ThenBy(row => row.Value)
            .Take(take)
            .Select(row => new BookSearchSuggestionDto(
                row.Value,
                row.Count,
                row.IsExact,
                !scoped || row.Count > 0))
            .ToListAsync(cancellationToken);
    }

    private sealed class SuggestionRow
    {
        public required string Key { get; init; }
        public required string Value { get; init; }
        public int Count { get; init; }
        public bool IsExact { get; init; }
    }
}
