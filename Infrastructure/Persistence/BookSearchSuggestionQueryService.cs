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
        var allBooks = GetOwnerBooks(ownerId)
            .Where(book => book.Author != null);
        var scopedBooks = GetScopedBooks(ownerId, criteria)
            .Where(book => book.Author != null);
        if (normalizedSearch.Length > 0)
        {
            allBooks = allBooks.Where(book =>
                book.Author!.NormalizedPrimaryName.Contains(normalizedSearch) ||
                book.Author.Names.Any(name => name.NormalizedName.Contains(normalizedSearch)));
            scopedBooks = scopedBooks.Where(book =>
                book.Author!.NormalizedPrimaryName.Contains(normalizedSearch) ||
                book.Author.Names.Any(name => name.NormalizedName.Contains(normalizedSearch)));
        }

        var allRows = allBooks
            .GroupBy(book => book.Author!.NormalizedPrimaryName)
            .Select(group => new SuggestionRow
            {
                Key = group.Key,
                Value = group.Min(book => book.Author!.PrimaryName)!,
                Count = group.Select(book => book.Id).Distinct().Count(),
                IsExact = normalizedSearch.Length > 0 &&
                          (group.Key == normalizedSearch ||
                           group.Any(book =>
                               book.Author!.Names.Any(name => name.NormalizedName == normalizedSearch)))
            });
        var scopedRows = scopedBooks
            .GroupBy(book => book.Author!.NormalizedPrimaryName)
            .Select(group => new SuggestionRow
            {
                Key = group.Key,
                Value = group.Min(book => book.Author!.PrimaryName)!,
                Count = group.Select(book => book.Id).Distinct().Count(),
                IsExact = normalizedSearch.Length > 0 &&
                          (group.Key == normalizedSearch ||
                           group.Any(book =>
                               book.Author!.Names.Any(name => name.NormalizedName == normalizedSearch)))
            });

        return ExecuteAsync(allRows, scopedRows, criteria, take, cancellationToken);
    }

    private Task<IReadOnlyCollection<BookSearchSuggestionDto>> GetTagSuggestionsAsync(
        Guid ownerId,
        string normalizedSearch,
        BookSearchCriteria? criteria,
        int take,
        CancellationToken cancellationToken)
    {
        var allTags = GetOwnerBooks(ownerId)
            .SelectMany(book => book.BookTags);
        var scopedTags = GetScopedBooks(ownerId, criteria)
            .SelectMany(book => book.BookTags);
        if (normalizedSearch.Length > 0)
        {
            allTags = allTags.Where(bookTag => bookTag.Tag.NormalizedName.Contains(normalizedSearch));
            scopedTags = scopedTags.Where(bookTag => bookTag.Tag.NormalizedName.Contains(normalizedSearch));
        }

        var allRows = allTags
            .GroupBy(bookTag => bookTag.Tag.NormalizedName)
            .Select(group => new SuggestionRow
            {
                Key = group.Key,
                Value = group.Min(bookTag => bookTag.Tag.Name)!,
                Count = group.Select(bookTag => bookTag.BookId).Distinct().Count(),
                IsExact = normalizedSearch.Length > 0 && group.Key == normalizedSearch
            });
        var scopedRows = scopedTags
            .GroupBy(bookTag => bookTag.Tag.NormalizedName)
            .Select(group => new SuggestionRow
            {
                Key = group.Key,
                Value = group.Min(bookTag => bookTag.Tag.Name)!,
                Count = group.Select(bookTag => bookTag.BookId).Distinct().Count(),
                IsExact = normalizedSearch.Length > 0 && group.Key == normalizedSearch
            });

        return ExecuteAsync(allRows, scopedRows, criteria, take, cancellationToken);
    }

    private Task<IReadOnlyCollection<BookSearchSuggestionDto>> GetGenreSuggestionsAsync(
        Guid ownerId,
        string normalizedSearch,
        BookSearchCriteria? criteria,
        int take,
        CancellationToken cancellationToken)
    {
        var allGenres = GetOwnerBooks(ownerId)
            .SelectMany(book => book.BookGenres);
        var scopedGenres = GetScopedBooks(ownerId, criteria)
            .SelectMany(book => book.BookGenres);
        if (normalizedSearch.Length > 0)
        {
            allGenres = allGenres.Where(bookGenre => bookGenre.Genre.NormalizedName.Contains(normalizedSearch));
            scopedGenres = scopedGenres.Where(bookGenre => bookGenre.Genre.NormalizedName.Contains(normalizedSearch));
        }

        var allRows = allGenres
            .GroupBy(bookGenre => bookGenre.Genre.NormalizedName)
            .Select(group => new SuggestionRow
            {
                Key = group.Key,
                Value = group.Min(bookGenre => bookGenre.Genre.Name)!,
                Count = group.Select(bookGenre => bookGenre.BookId).Distinct().Count(),
                IsExact = normalizedSearch.Length > 0 && group.Key == normalizedSearch
            });
        var scopedRows = scopedGenres
            .GroupBy(bookGenre => bookGenre.Genre.NormalizedName)
            .Select(group => new SuggestionRow
            {
                Key = group.Key,
                Value = group.Min(bookGenre => bookGenre.Genre.Name)!,
                Count = group.Select(bookGenre => bookGenre.BookId).Distinct().Count(),
                IsExact = normalizedSearch.Length > 0 && group.Key == normalizedSearch
            });

        return ExecuteAsync(allRows, scopedRows, criteria, take, cancellationToken);
    }

    private Task<IReadOnlyCollection<BookSearchSuggestionDto>> GetStatusSuggestionsAsync(
        Guid ownerId,
        string? search,
        BookSearchCriteria? criteria,
        int take,
        CancellationToken cancellationToken)
    {
        var normalizedSearch = search?.Trim().ToUpperInvariant() ?? string.Empty;
        var allBooks = GetOwnerBooks(ownerId);
        var scopedBooks = GetScopedBooks(ownerId, criteria);
        if (normalizedSearch.Length > 0)
        {
            allBooks = allBooks.Where(book => book.Status.Name.ToUpper().Contains(normalizedSearch));
            scopedBooks = scopedBooks.Where(book => book.Status.Name.ToUpper().Contains(normalizedSearch));
        }

        var allRows = allBooks
            .GroupBy(book => new { book.StatusId, book.Status.Name })
            .Select(group => new SuggestionRow
            {
                Key = group.Key.Name.ToUpper(),
                Value = group.Key.Name,
                Count = group.Count(),
                IsExact = normalizedSearch.Length > 0 && group.Key.Name.ToUpper() == normalizedSearch
            });
        var scopedRows = scopedBooks
            .GroupBy(book => new { book.StatusId, book.Status.Name })
            .Select(group => new SuggestionRow
            {
                Key = group.Key.Name.ToUpper(),
                Value = group.Key.Name,
                Count = group.Count(),
                IsExact = normalizedSearch.Length > 0 && group.Key.Name.ToUpper() == normalizedSearch
            });

        return ExecuteAsync(allRows, scopedRows, criteria, take, cancellationToken);
    }

    private Task<IReadOnlyCollection<BookSearchSuggestionDto>> GetTypeSuggestionsAsync(
        Guid ownerId,
        string? search,
        BookSearchCriteria? criteria,
        int take,
        CancellationToken cancellationToken)
    {
        var normalizedSearch = search?.Trim().ToUpperInvariant() ?? string.Empty;
        var allBooks = GetOwnerBooks(ownerId);
        var scopedBooks = GetScopedBooks(ownerId, criteria);
        if (normalizedSearch.Length > 0)
        {
            allBooks = allBooks.Where(book => book.ContentType.Name.ToUpper().Contains(normalizedSearch));
            scopedBooks = scopedBooks.Where(book => book.ContentType.Name.ToUpper().Contains(normalizedSearch));
        }

        var allRows = allBooks
            .GroupBy(book => new { book.ContentTypeId, book.ContentType.Name })
            .Select(group => new SuggestionRow
            {
                Key = group.Key.Name.ToUpper(),
                Value = group.Key.Name,
                Count = group.Count(),
                IsExact = normalizedSearch.Length > 0 && group.Key.Name.ToUpper() == normalizedSearch
            });
        var scopedRows = scopedBooks
            .GroupBy(book => new { book.ContentTypeId, book.ContentType.Name })
            .Select(group => new SuggestionRow
            {
                Key = group.Key.Name.ToUpper(),
                Value = group.Key.Name,
                Count = group.Count(),
                IsExact = normalizedSearch.Length > 0 && group.Key.Name.ToUpper() == normalizedSearch
            });

        return ExecuteAsync(allRows, scopedRows, criteria, take, cancellationToken);
    }

    private IQueryable<Book> GetOwnerBooks(Guid ownerId)
    {
        return _context.Books
            .AsNoTracking()
            .Where(book => book.OwnerId == ownerId);
    }

    private static async Task<IReadOnlyCollection<BookSearchSuggestionDto>> ExecuteAsync(
        IQueryable<SuggestionRow> allRows,
        IQueryable<SuggestionRow> rows,
        BookSearchCriteria? criteria,
        int take,
        CancellationToken cancellationToken)
    {
        if (criteria?.HasFilters != true)
        {
            return await rows
                .OrderByDescending(row => row.IsExact)
                .ThenByDescending(row => row.Count)
                .ThenBy(row => row.Value)
                .Take(take)
                .Select(row => new BookSearchSuggestionDto(row.Value, row.Count, row.IsExact, true))
                .ToListAsync(cancellationToken);
        }

        var availableRows = await rows
            .OrderByDescending(row => row.IsExact)
            .ThenByDescending(row => row.Count)
            .ThenBy(row => row.Value)
            .Take(take)
            .ToListAsync(cancellationToken);
        var allMatches = await allRows
            .OrderByDescending(row => row.IsExact)
            .ThenByDescending(row => row.Count)
            .ThenBy(row => row.Value)
            .Take(take)
            .ToListAsync(cancellationToken);

        var availableByKey = availableRows.ToDictionary(row => row.Key, StringComparer.OrdinalIgnoreCase);
        return allMatches
            .Select(row =>
            {
                var isAvailable = availableByKey.TryGetValue(row.Key, out var available);
                return new BookSearchSuggestionDto(
                    row.Value,
                    isAvailable ? available!.Count : 0,
                    row.IsExact || (isAvailable && available!.IsExact),
                    isAvailable);
            })
            .Concat(availableRows
                .Where(row => allMatches.All(match => !match.Key.Equals(row.Key, StringComparison.OrdinalIgnoreCase)))
                .Select(row => new BookSearchSuggestionDto(row.Value, row.Count, row.IsExact, true)))
            .OrderByDescending(row => row.IsExact)
            .ThenByDescending(row => row.IsAvailable)
            .ThenByDescending(row => row.Count)
            .ThenBy(row => row.Value)
            .Take(take)
            .ToList();
    }

    private sealed class SuggestionRow
    {
        public required string Key { get; init; }
        public required string Value { get; init; }
        public int Count { get; init; }
        public bool IsExact { get; init; }
    }
}
