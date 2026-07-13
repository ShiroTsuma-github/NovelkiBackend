namespace Infrastructure.Persistence;

using Application.Common;
using Domain.Entities;
using System.Linq.Expressions;

public sealed class BookSortBuilder
{
    private readonly ApplicationDbContext _context;
    private readonly bool _supportsILike;

    public BookSortBuilder(ApplicationDbContext context)
    {
        _context = context;
        _supportsILike = context.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
    }

    public bool ShouldSortDateOnClient(string? sortBy)
    {
        return !_supportsILike && IsDateSort(sortBy);
    }

    public async Task<IQueryable<Book>> ApplySortingAsync(
        IQueryable<Book> query,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        switch (NormalizeSort(sortBy))
        {
            case "title":
                return descending
                    ? query.OrderByDescending(book => book.NormalizedPrimaryTitle).ThenByDescending(book => book.PrimaryTitle).ThenByDescending(book => book.Id)
                    : query.OrderBy(book => book.NormalizedPrimaryTitle).ThenBy(book => book.PrimaryTitle).ThenBy(book => book.Id);
            case "author":
                return descending
                    ? query.OrderByDescending(book => book.Author != null ? book.Author.PrimaryName : "").ThenBy(book => book.PrimaryTitle)
                    : query.OrderBy(book => book.Author != null ? book.Author.PrimaryName : "").ThenBy(book => book.PrimaryTitle);
            case "status":
                return ApplyNamedCycleOrder(
                    query,
                    await _context.Statuses.AsNoTracking().OrderBy(status => status.Id.ToString()).Select(status => status.Name).ToListAsync(cancellationToken),
                    sortDirection,
                    book => book.Status.Name);
            case "type":
                return ApplyNamedCycleOrder(
                    query,
                    await _context.ContentTypes.AsNoTracking().OrderBy(type => type.Id.ToString()).Select(type => type.Name).ToListAsync(cancellationToken),
                    sortDirection,
                    book => book.ContentType.Name);
            case "progress":
                return descending
                    ? query.OrderBy(book => book.CurrentChapterNumber == null).ThenByDescending(book => book.CurrentChapterNumber).ThenBy(book => book.PrimaryTitle)
                    : query.OrderBy(book => book.CurrentChapterNumber == null).ThenBy(book => book.CurrentChapterNumber).ThenBy(book => book.PrimaryTitle);
            case "chapters":
                return descending
                    ? query.OrderBy(book => book.TotalChapters == null).ThenByDescending(book => book.TotalChapters).ThenBy(book => book.PrimaryTitle)
                    : query.OrderBy(book => book.TotalChapters == null).ThenBy(book => book.TotalChapters).ThenBy(book => book.PrimaryTitle);
            case "rating":
                return descending
                    ? query.OrderBy(book => book.Rating == null).ThenByDescending(book => book.Rating).ThenBy(book => book.PrimaryTitle)
                    : query.OrderBy(book => book.Rating == null).ThenBy(book => book.Rating).ThenBy(book => book.PrimaryTitle);
            case "priority":
                return descending
                    ? query.OrderBy(book => book.Priority == null).ThenByDescending(book => book.Priority).ThenBy(book => book.PrimaryTitle)
                    : query.OrderBy(book => book.Priority == null).ThenBy(book => book.Priority).ThenBy(book => book.PrimaryTitle);
            case "owner":
                return descending
                    ? query.OrderByDescending(book => book.OwnerId).ThenBy(book => book.PrimaryTitle)
                    : query.OrderBy(book => book.OwnerId).ThenBy(book => book.PrimaryTitle);
            case "created":
                return descending
                    ? query.OrderByDescending(book => book.Created).ThenBy(book => book.PrimaryTitle)
                    : query.OrderBy(book => book.Created).ThenBy(book => book.PrimaryTitle);
            case "lastmodified":
                return descending
                    ? query.OrderByDescending(book => book.LastModified).ThenBy(book => book.PrimaryTitle)
                    : query.OrderBy(book => book.LastModified).ThenBy(book => book.PrimaryTitle);
            default:
                return query.OrderByDescending(book => book.LastModified).ThenBy(book => book.PrimaryTitle);
        }
    }

    internal static IEnumerable<BookListProjection> SortProjectedByDate(
        IEnumerable<BookListProjection> books,
        string? sortBy,
        string? sortDirection)
    {
        var descending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        return NormalizeSort(sortBy) == "created"
            ? descending
                ? books.OrderByDescending(book => book.Created).ThenBy(book => book.PrimaryTitle)
                : books.OrderBy(book => book.Created).ThenBy(book => book.PrimaryTitle)
            : descending
                ? books.OrderByDescending(book => book.LastModified).ThenBy(book => book.PrimaryTitle)
                : books.OrderBy(book => book.LastModified).ThenBy(book => book.PrimaryTitle);
    }

    public async Task<IEnumerable<Book>> ToSortedPageAsync(
        IQueryable<Book> query,
        int skip,
        int take,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        if (ShouldSortDateOnClient(sortBy))
        {
            var descending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
            var books = await query.ToListAsync(cancellationToken);
            var sorted = NormalizeSort(sortBy) == "created"
                ? descending
                    ? books.OrderByDescending(book => book.Created).ThenBy(book => book.PrimaryTitle)
                    : books.OrderBy(book => book.Created).ThenBy(book => book.PrimaryTitle)
                : descending
                    ? books.OrderByDescending(book => book.LastModified).ThenBy(book => book.PrimaryTitle)
                    : books.OrderBy(book => book.LastModified).ThenBy(book => book.PrimaryTitle);
            return sorted.Skip(skip).Take(take).ToList();
        }

        return await (await ApplySortingAsync(query, sortBy, sortDirection, cancellationToken))
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<string?> GetNextCycleSortDirectionAsync(
        IQueryable<Book> query,
        string sortBy,
        string? currentSortDirection,
        CancellationToken cancellationToken)
    {
        var normalizedSort = NormalizeSort(sortBy);
        var orderedAvailableNames = normalizedSort switch
        {
            "status" => await GetOrderedAvailableNamesAsync(
                query.Select(book => book.Status.Name),
                _context.Statuses.AsNoTracking().OrderBy(status => status.Id.ToString()).Select(status => status.Name),
                cancellationToken),
            "type" => await GetOrderedAvailableNamesAsync(
                query.Select(book => book.ContentType.Name),
                _context.ContentTypes.AsNoTracking().OrderBy(type => type.Id.ToString()).Select(type => type.Name),
                cancellationToken),
            _ => []
        };

        if (orderedAvailableNames.Count == 0)
        {
            return null;
        }

        var normalizedCurrent = NormalizeCycleValue(currentSortDirection);
        var currentIndex = normalizedCurrent == null
            ? -1
            : orderedAvailableNames.FindIndex(name => string.Equals(NormalizeCycleValue(name), normalizedCurrent, StringComparison.Ordinal));
        var nextIndex = (currentIndex + 1 + orderedAvailableNames.Count) % orderedAvailableNames.Count;
        return orderedAvailableNames[nextIndex];
    }

    private static string NormalizeSort(string? sortBy)
    {
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "title" or "primarytitle" => "title",
            "author" => "author",
            "status" => "status",
            "type" or "contenttype" => "type",
            "progress" or "currentchapter" => "progress",
            "chapter" or "chapters" or "totalchapter" or "totalchapters" => "chapters",
            "rating" => "rating",
            "priority" => "priority",
            "owner" or "ownerid" => "owner",
            "created" or "createdat" => "created",
            "lastmodified" or "updated" or "updatedat" => "lastmodified",
            _ => "lastmodified"
        };
    }

    private static bool IsDateSort(string? sortBy)
    {
        var normalizedSort = NormalizeSort(sortBy);
        return normalizedSort is "created" or "lastmodified";
    }

    private static IQueryable<Book> ApplyNamedCycleOrder(
        IQueryable<Book> query,
        IReadOnlyList<string> orderedNames,
        string? startName,
        Expression<Func<Book, string>> keySelector)
    {
        if (orderedNames.Count == 0)
        {
            return query.OrderBy(book => book.PrimaryTitle).ThenBy(book => book.Id);
        }

        var orderedNameArray = orderedNames.ToArray();
        var normalizedStartName = NormalizeCycleValue(startName);
        var startIndex = normalizedStartName == null
            ? -1
            : Array.FindIndex(
                orderedNameArray,
                name => string.Equals(NormalizeCycleValue(name), normalizedStartName, StringComparison.Ordinal));
        if (startIndex < 0)
        {
            startIndex = 0;
        }

        var rotatedNames = orderedNameArray.Skip(startIndex).Concat(orderedNameArray.Take(startIndex)).ToArray();
        var parameter = keySelector.Parameters[0];
        Expression body = Expression.Constant(rotatedNames.Length);
        for (var index = rotatedNames.Length - 1; index >= 0; index--)
        {
            body = Expression.Condition(
                Expression.Equal(keySelector.Body, Expression.Constant(rotatedNames[index])),
                Expression.Constant(index),
                body);
        }

        var rankSelector = Expression.Lambda<Func<Book, int>>(body, parameter);
        return query.OrderBy(rankSelector).ThenBy(book => book.PrimaryTitle).ThenBy(book => book.Id);
    }

    private static async Task<List<string>> GetOrderedAvailableNamesAsync(
        IQueryable<string> valuesQuery,
        IQueryable<string> orderedNamesQuery,
        CancellationToken cancellationToken)
    {
        var availableNames = (await valuesQuery.Distinct().ToListAsync(cancellationToken))
            .Select(NormalizeCycleValue)
            .Where(name => name != null)
            .ToHashSet(StringComparer.Ordinal);
        return (await orderedNamesQuery.ToListAsync(cancellationToken))
            .Where(name => availableNames.Contains(NormalizeCycleValue(name)!))
            .ToList();
    }

    private static string? NormalizeCycleValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : MappingExtensions.NormalizeName(value);
    }
}
