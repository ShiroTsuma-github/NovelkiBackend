namespace Infrastructure.Persistence;

using Application.Common;
using Application.Common.DTOs.Book;
using Application.Common.Interfaces;
using Application.Common.Models;
using Domain.Entities;
using Domain.Models;
using System.Linq.Expressions;

public sealed class BookReadQueryService : IBookListQueryService, IBookExportQueryService, IBookSummaryQueryService
{
    private readonly ApplicationDbContext _context;
    private readonly bool _supportsILike;

    public BookReadQueryService(ApplicationDbContext context)
    {
        _context = context;
        _supportsILike = context.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
    }

    public async Task<IReadOnlyCollection<BookListItemDto>> GetBooksAsync(
        Guid ownerId,
        BookSearchCriteria criteria,
        int skip,
        int take,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        var query = _context.Books.AsNoTracking().Where(book => book.OwnerId == ownerId);
        if (criteria.HasFilters)
        {
            query = ApplyCriteria(query, criteria);
        }

        return await ToProjectedListPageAsync(query, skip, take, sortBy, sortDirection, cancellationToken);
    }

    public Task<int> GetBookCountAsync(Guid ownerId, BookSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var query = _context.Books.Where(book => book.OwnerId == ownerId);
        if (criteria.HasFilters)
        {
            query = ApplyCriteria(query, criteria);
        }

        return query.CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AdminBookListItemDto>> GetAdminBooksAsync(
        BookSearchCriteria criteria,
        int skip,
        int take,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        var query = _context.Books.AsNoTracking();
        if (criteria.HasFilters)
        {
            query = ApplyCriteria(query, criteria);
        }

        return await ToProjectedAdminListPageAsync(query, skip, take, sortBy, sortDirection, cancellationToken);
    }

    public Task<int> GetAdminBookCountAsync(BookSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var query = criteria.HasFilters ? ApplyCriteria(_context.Books, criteria) : _context.Books;
        return query.CountAsync(cancellationToken);
    }

    public async Task<PaginatedResult<BookDto>> GetBooksForExportAsync(
        Guid ownerId,
        BookSearchCriteria criteria,
        int skip,
        int take,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        var query = IncludeDetails(_context.Books).Where(book => book.OwnerId == ownerId);
        if (criteria.HasFilters)
        {
            query = ApplyCriteria(query, criteria);
        }

        var books = await ToSortedPageAsync(query, skip, take, sortBy, sortDirection, cancellationToken);
        var total = await GetBookCountAsync(ownerId, criteria, cancellationToken);
        return new PaginatedResult<BookDto>
        {
            Skip = skip,
            Take = take,
            Total = total,
            Data = books.Select(book => book.ToDto()).ToList()
        };
    }

    public async Task<BookSummarySnapshot> GetSummaryAsync(Guid ownerId, BookSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var query = _context.Books
            .AsNoTracking()
            .Where(book => book.OwnerId == ownerId);
        if (criteria.HasFilters)
        {
            query = ApplyCriteria(query, criteria);
        }

        var totalBooks = await query.CountAsync(cancellationToken);
        var ratedBooks = await query.CountAsync(book => book.Rating != null, cancellationToken);
        var averageRating = ratedBooks == 0
            ? null
            : await query
                .Where(book => book.Rating != null)
                .Select(book => (double?)book.Rating)
                .AverageAsync(cancellationToken);
        var currentChapters = await query
            .Where(book => book.CurrentChapterNumber != null)
            .Select(book => book.CurrentChapterNumber ?? 0)
            .SumAsync(cancellationToken);
        var booksWithKnownCurrentChapter = await query.CountAsync(book => book.CurrentChapterNumber != null, cancellationToken);
        var statusCountRows = await query
            .Select(book => book.Status.Name)
            .GroupBy(status => status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count(),
            })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.Status)
            .ToListAsync(cancellationToken);
        var statusCounts = statusCountRows
            .Select(group => new BookStatusCountSnapshot(group.Status, group.Count))
            .ToList();
        var typeCountRows = await query
            .GroupBy(book => book.ContentType.Name)
            .Select(group => new
            {
                Type = group.Key,
                BookCount = group.Count(),
                CurrentChapters = group.Sum(book => book.CurrentChapterNumber ?? 0),
            })
            .OrderByDescending(group => group.BookCount)
            .ThenBy(group => group.Type)
            .ToListAsync(cancellationToken);
        var typeCounts = typeCountRows
            .Select(group => new BookTypeSummarySnapshot(group.Type, group.BookCount, group.CurrentChapters))
            .ToList();
        var genreCountRows = await query
            .SelectMany(book => book.BookGenres.Select(bookGenre => bookGenre.Genre.Name))
            .GroupBy(genre => genre)
            .Select(group => new
            {
                Genre = group.Key,
                BookCount = group.Count(),
            })
            .OrderByDescending(group => group.BookCount)
            .ThenBy(group => group.Genre)
            .ToListAsync(cancellationToken);
        var genreCounts = genreCountRows
            .Select(group => new BookGenreCountSnapshot(group.Genre, group.BookCount))
            .ToList();
        var ratingCountRows = await query
            .Where(book => book.Rating != null)
            .GroupBy(book => book.Rating!.Value)
            .Select(group => new
            {
                Rating = group.Key,
                BookCount = group.Count(),
            })
            .OrderBy(group => group.Rating)
            .ToListAsync(cancellationToken);
        var ratingCounts = ratingCountRows
            .Select(group => new BookRatingCountSnapshot(group.Rating, group.BookCount))
            .ToList();

        return new BookSummarySnapshot(
            totalBooks,
            ratedBooks,
            averageRating,
            currentChapters,
            booksWithKnownCurrentChapter,
            statusCounts,
            typeCounts,
            genreCounts,
            ratingCounts);
    }

    public async Task<string?> GetNextCycleSortDirectionAsync(
        Guid ownerId,
        BookSearchCriteria criteria,
        string sortBy,
        string? currentSortDirection,
        CancellationToken cancellationToken)
    {
        var query = IncludeDetails(_context.Books).Where(book => book.OwnerId == ownerId);
        if (criteria.HasFilters)
        {
            query = ApplyCriteria(query, criteria);
        }

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

    private static IQueryable<Book> IncludeDetails(IQueryable<Book> query)
    {
        return query
            .Include(book => book.Author).ThenInclude(author => author!.Names)
            .Include(book => book.Cover)
            .Include(book => book.ContentType)
            .Include(book => book.Status)
            .Include(book => book.Titles)
            .Include(book => book.BookGenres).ThenInclude(bookGenre => bookGenre.Genre)
            .Include(book => book.BookTags).ThenInclude(bookTag => bookTag.Tag)
            .Include(book => book.Links)
            .Include(book => book.ProgressHistory);
    }

    private async Task<List<BookListItemDto>> ToProjectedListPageAsync(
        IQueryable<Book> query,
        int skip,
        int take,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        if (ShouldSortDateOnClient(sortBy))
        {
            var clientSortedPage = SortProjectedByDate(await ProjectBooks(query).ToListAsync(cancellationToken), sortBy, sortDirection)
                .Skip(skip)
                .Take(take)
                .ToList();

            return clientSortedPage.Select(MapListProjection).ToList();
        }

        var pageQuery = await BuildProjectedPageQueryAsync(query, skip, take, sortBy, sortDirection, cancellationToken);
        var page = await pageQuery.ToListAsync(cancellationToken);

        return page.Select(MapListProjection).ToList();
    }

    private async Task<List<AdminBookListItemDto>> ToProjectedAdminListPageAsync(
        IQueryable<Book> query,
        int skip,
        int take,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        if (ShouldSortDateOnClient(sortBy))
        {
            var sortedPage = SortProjectedByDate(await ProjectBooks(query).ToListAsync(cancellationToken), sortBy, sortDirection)
                .Skip(skip)
                .Take(take)
                .ToList();
            var clientSortedOwners = await GetOwnersAsync(sortedPage.Select(book => book.OwnerId), cancellationToken);

            return sortedPage.Select(book => MapAdminListProjection(book, clientSortedOwners)).ToList();
        }

        var pageQuery = await BuildProjectedPageQueryAsync(query, skip, take, sortBy, sortDirection, cancellationToken);
        var page = await pageQuery.ToListAsync(cancellationToken);
        var owners = await GetOwnersAsync(page.Select(book => book.OwnerId), cancellationToken);

        return page.Select(book => MapAdminListProjection(book, owners)).ToList();
    }

    private async Task<Dictionary<Guid, BookOwnerProjection>> GetOwnersAsync(IEnumerable<Guid> ownerIds, CancellationToken cancellationToken)
    {
        var distinctOwnerIds = ownerIds.Distinct().ToArray();
        var owners = await _context.Users
            .AsNoTracking()
            .Where(user => distinctOwnerIds.Contains(user.Id))
            .Select(user => new BookOwnerProjection
            {
                Id = user.Id,
                Username = user.UserName,
                Email = user.Email
            })
            .ToDictionaryAsync(user => user.Id, cancellationToken);
        return owners;
    }

    private async Task<IQueryable<BookListProjection>> BuildProjectedPageQueryAsync(
        IQueryable<Book> query,
        int skip,
        int take,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        var sortedPage = (await ApplySortingAsync(query, sortBy, sortDirection, cancellationToken))
            .Skip(skip)
            .Take(take);
        return ProjectBooks(sortedPage);
    }

    private static IQueryable<BookListProjection> ProjectBooks(IQueryable<Book> query)
    {
        return query
            .Select(book => new BookListProjection
            {
                Id = book.Id,
                Created = book.Created,
                LastModified = book.LastModified,
                OwnerId = book.OwnerId,
                PrimaryTitle = book.PrimaryTitle,
                Description = book.Description == null
                    ? null
                    : book.Description.Length > 80
                        ? book.Description.Substring(0, 77) + "..."
                        : book.Description,
                AlternativeTitles = book.Titles
                    .Where(title => !title.IsPrimary)
                    .OrderBy(title => title.Id)
                    .Select(title => title.Title)
                    .Take(4)
                    .ToList(),
                AlternativeTitlesCount = book.Titles.Count(title => !title.IsPrimary),
                Author = book.Author != null ? book.Author.PrimaryName : null,
                ContentType = book.ContentType.Name,
                Status = book.Status.Name,
                CurrentChapterNumber = book.CurrentChapterNumber,
                CurrentChapterLabel = book.CurrentChapterLabel,
                TotalChapters = book.TotalChapters,
                Rating = book.Rating,
                Priority = book.Priority,
                Notes = book.Notes == null
                    ? null
                    : book.Notes.Length > 80
                        ? book.Notes.Substring(0, 77) + "..."
                        : book.Notes,
                Genres = book.BookGenres
                    .OrderBy(bookGenre => bookGenre.Genre.Name)
                    .Select(bookGenre => bookGenre.Genre.Name)
                    .Take(4)
                    .ToList(),
                GenresCount = book.BookGenres.Count(),
                Tags = book.BookTags
                    .OrderBy(bookTag => bookTag.Tag.Name)
                    .Select(bookTag => bookTag.Tag.Name)
                    .Take(4)
                    .ToList(),
                TagsCount = book.BookTags.Count(),
                LinksCount = book.Links.Count(),
                CoverStatus = book.Cover != null ? book.Cover.Status : null,
                CoverSource = book.Cover != null ? book.Cover.Source : null,
                CoverFailureReason = book.Cover != null ? book.Cover.FailureReason : null,
                CoverLastAttemptAt = book.Cover != null ? book.Cover.LastAttemptAt : null,
                CoverLastModified = book.Cover != null ? book.Cover.LastModified : null,
                CoverCreated = book.Cover != null ? book.Cover.Created : null,
                HasCoverStoragePath = book.Cover != null && book.Cover.StoragePath != null,
                HasCoverThumbnailStoragePath = book.Cover != null && book.Cover.ThumbnailStoragePath != null
            });
    }

    private bool ShouldSortDateOnClient(string? sortBy)
    {
        return !_supportsILike && IsDateSort(sortBy);
    }

    private static IEnumerable<BookListProjection> SortProjectedByDate(
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

    private async Task<IEnumerable<Book>> ToSortedPageAsync(
        IQueryable<Book> query,
        int skip,
        int take,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        if (!_supportsILike && IsDateSort(sortBy))
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

    private async Task<IQueryable<Book>> ApplySortingAsync(
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
                    ? query.OrderByDescending(book => (double?)book.CurrentChapterNumber).ThenBy(book => book.PrimaryTitle)
                    : query.OrderBy(book => (double?)book.CurrentChapterNumber).ThenBy(book => book.PrimaryTitle);
            case "chapters":
                return descending
                    ? query.OrderByDescending(book => (double?)book.TotalChapters).ThenBy(book => book.PrimaryTitle)
                    : query.OrderBy(book => (double?)book.TotalChapters).ThenBy(book => book.PrimaryTitle);
            case "rating":
                return descending
                    ? query.OrderBy(book => book.Rating == null).ThenByDescending(book => book.Rating).ThenBy(book => book.PrimaryTitle)
                    : query.OrderBy(book => book.Rating == null).ThenBy(book => book.Rating).ThenBy(book => book.PrimaryTitle);
            case "priority":
                return descending
                    ? query.OrderByDescending(book => book.Priority).ThenBy(book => book.PrimaryTitle)
                    : query.OrderBy(book => book.Priority).ThenBy(book => book.PrimaryTitle);
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

    private IQueryable<Book> ApplyCriteria(IQueryable<Book> query, BookSearchCriteria criteria)
    {
        foreach (var term in criteria.Terms)
        {
            query = ApplyGeneralTextSearch(query, term);
        }

        foreach (var filter in criteria.Fields)
        {
            query = filter.Field switch
            {
                BookSearchField.Title => ApplyTitleSearch(query, filter.Values),
                BookSearchField.Author => ApplyAuthorSearch(query, filter.Values),
                BookSearchField.Tag => ApplyTagSearch(query, filter.Values),
                BookSearchField.Genre => ApplyGenreSearch(query, filter.Values),
                BookSearchField.Status => ApplyStatusSearch(query, filter.Values),
                BookSearchField.Type => ApplyTypeSearch(query, filter.Values),
                _ => query
            };
        }

        foreach (var filter in criteria.Numbers)
        {
            query = filter.Field switch
            {
                BookSearchNumberField.Rating => ApplyRating(query, filter.Operator, filter.Value),
                BookSearchNumberField.Priority => ApplyPriority(query, filter.Operator, filter.Value),
                BookSearchNumberField.CurrentChapter => ApplyCurrentChapter(query, filter.Operator, filter.Value),
                BookSearchNumberField.TotalChapters => ApplyTotalChapters(query, filter.Operator, filter.Value),
                _ => query
            };
        }

        return query;
    }

    private IQueryable<Book> ApplyGeneralTextSearch(IQueryable<Book> query, string term)
    {
        var pattern = ToLikePattern(term);
        if (_supportsILike)
        {
            return query.Where(book =>
                EF.Functions.ILike(book.PrimaryTitle, pattern) ||
                book.Titles.Any(title => EF.Functions.ILike(title.Title, pattern)) ||
                (book.Author != null && (
                    EF.Functions.ILike(book.Author.PrimaryName, pattern) ||
                    book.Author.Names.Any(name => EF.Functions.ILike(name.Name, pattern)))));
        }

        var normalizedPattern = ToLikePattern(MappingExtensions.NormalizeName(term));
        return query.Where(book =>
            EF.Functions.Like(book.NormalizedPrimaryTitle, normalizedPattern) ||
            book.Titles.Any(title => EF.Functions.Like(title.NormalizedTitle, normalizedPattern)) ||
            (book.Author != null && (
                EF.Functions.Like(book.Author.NormalizedPrimaryName, normalizedPattern) ||
                book.Author.Names.Any(name => EF.Functions.Like(name.NormalizedName, normalizedPattern)))));
    }

    private IQueryable<Book> ApplyTitleSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        var predicates = _supportsILike
            ? searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var pattern = ToLikePattern(search);
                return book =>
                    EF.Functions.ILike(book.PrimaryTitle, pattern) ||
                    book.Titles.Any(title => EF.Functions.ILike(title.Title, pattern));
            })
            : searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var normalizedPattern = ToLikePattern(MappingExtensions.NormalizeName(search));
                return book =>
                    EF.Functions.Like(book.NormalizedPrimaryTitle, normalizedPattern) ||
                    book.Titles.Any(title => EF.Functions.Like(title.NormalizedTitle, normalizedPattern));
            });

        return ApplyAnyFieldMatch(query, predicates);
    }

    private IQueryable<Book> ApplyAuthorSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        var predicates = _supportsILike
            ? searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var pattern = ToLikePattern(search);
                return book => book.Author != null && (
                    EF.Functions.ILike(book.Author.PrimaryName, pattern) ||
                    book.Author.Names.Any(name => EF.Functions.ILike(name.Name, pattern)));
            })
            : searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var normalizedPattern = ToLikePattern(MappingExtensions.NormalizeName(search));
                return book => book.Author != null && (
                    EF.Functions.Like(book.Author.NormalizedPrimaryName, normalizedPattern) ||
                    book.Author.Names.Any(name => EF.Functions.Like(name.NormalizedName, normalizedPattern)));
            });

        return ApplyAnyFieldMatch(query, predicates);
    }

    private IQueryable<Book> ApplyTagSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        var predicates = _supportsILike
            ? searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var pattern = ToLikePattern(search);
                return book => book.BookTags.Any(bookTag => EF.Functions.ILike(bookTag.Tag.Name, pattern));
            })
            : searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var normalizedPattern = ToLikePattern(MappingExtensions.NormalizeName(search));
                return book => book.BookTags.Any(bookTag => EF.Functions.Like(bookTag.Tag.NormalizedName, normalizedPattern));
            });

        return ApplyAnyFieldMatch(query, predicates);
    }

    private IQueryable<Book> ApplyGenreSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        var predicates = _supportsILike
            ? searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var pattern = ToLikePattern(search);
                return book => book.BookGenres.Any(bookGenre => EF.Functions.ILike(bookGenre.Genre.Name, pattern));
            })
            : searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var normalizedPattern = ToLikePattern(MappingExtensions.NormalizeName(search));
                return book => book.BookGenres.Any(bookGenre => EF.Functions.Like(bookGenre.Genre.NormalizedName, normalizedPattern));
            });

        return ApplyAnyFieldMatch(query, predicates);
    }

    private IQueryable<Book> ApplyStatusSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        var predicates = _supportsILike
            ? searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var pattern = ToLikePattern(search);
                return book => EF.Functions.ILike(book.Status.Name, pattern);
            })
            : searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var normalizedPattern = ToLikePattern(MappingExtensions.NormalizeName(search));
                return book => EF.Functions.Like(book.Status.Name.ToUpper(), normalizedPattern);
            });

        return ApplyAnyFieldMatch(query, predicates);
    }

    private IQueryable<Book> ApplyTypeSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        var predicates = _supportsILike
            ? searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var pattern = ToLikePattern(search);
                return book => EF.Functions.ILike(book.ContentType.Name, pattern);
            })
            : searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var normalizedPattern = ToLikePattern(MappingExtensions.NormalizeName(search));
                return book => EF.Functions.Like(book.ContentType.Name.ToUpper(), normalizedPattern);
            });

        return ApplyAnyFieldMatch(query, predicates);
    }

    private static IQueryable<Book> ApplyAnyFieldMatch(
        IQueryable<Book> query,
        IEnumerable<Expression<Func<Book, bool>>> predicates)
    {
        Expression<Func<Book, bool>>? combined = null;

        foreach (var predicate in predicates)
        {
            combined = combined == null ? predicate : OrElse(combined, predicate);
        }

        return combined == null ? query : query.Where(combined);
    }

    private static Expression<Func<Book, bool>> OrElse(
        Expression<Func<Book, bool>> left,
        Expression<Func<Book, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(Book), "book");
        var leftBody = ReplaceParameter(left.Body, left.Parameters[0], parameter);
        var rightBody = ReplaceParameter(right.Body, right.Parameters[0], parameter);
        return Expression.Lambda<Func<Book, bool>>(Expression.OrElse(leftBody, rightBody), parameter);
    }

    private static Expression ReplaceParameter(Expression expression, ParameterExpression source, ParameterExpression target)
    {
        return new ParameterReplaceVisitor(source, target).Visit(expression)!;
    }

    private sealed class ParameterReplaceVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _source;
        private readonly ParameterExpression _target;

        public ParameterReplaceVisitor(ParameterExpression source, ParameterExpression target)
        {
            _source = source;
            _target = target;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _source ? _target : base.VisitParameter(node);
        }
    }

    private static string ToLikePattern(string value)
    {
        var pattern = EscapeLike(value.Trim()).Replace("*", "%", StringComparison.Ordinal);
        return pattern.Contains('%') ? pattern : $"%{pattern}%";
    }

    private static string EscapeLike(string value)
    {
        return value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
    }

    private static IQueryable<Book> ApplyRating(IQueryable<Book> query, BookSearchOperator op, decimal value)
    {
        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(book => book.Rating != null && (decimal)book.Rating.Value > value),
            BookSearchOperator.GreaterThanOrEqual => query.Where(book => book.Rating != null && (decimal)book.Rating.Value >= value),
            BookSearchOperator.LessThan => query.Where(book => book.Rating != null && (decimal)book.Rating.Value < value),
            BookSearchOperator.LessThanOrEqual => query.Where(book => book.Rating != null && (decimal)book.Rating.Value <= value),
            _ => query.Where(book => book.Rating != null && (decimal)book.Rating.Value == value)
        };
    }

    private static IQueryable<Book> ApplyPriority(IQueryable<Book> query, BookSearchOperator op, decimal value)
    {
        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(book => book.Priority != null && (decimal)book.Priority.Value > value),
            BookSearchOperator.GreaterThanOrEqual => query.Where(book => book.Priority != null && (decimal)book.Priority.Value >= value),
            BookSearchOperator.LessThan => query.Where(book => book.Priority != null && (decimal)book.Priority.Value < value),
            BookSearchOperator.LessThanOrEqual => query.Where(book => book.Priority != null && (decimal)book.Priority.Value <= value),
            _ => query.Where(book => book.Priority != null && (decimal)book.Priority.Value == value)
        };
    }

    private static IQueryable<Book> ApplyCurrentChapter(IQueryable<Book> query, BookSearchOperator op, decimal value)
    {
        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(book => book.CurrentChapterNumber != null && book.CurrentChapterNumber > value),
            BookSearchOperator.GreaterThanOrEqual => query.Where(book => book.CurrentChapterNumber != null && book.CurrentChapterNumber >= value),
            BookSearchOperator.LessThan => query.Where(book => book.CurrentChapterNumber != null && book.CurrentChapterNumber < value),
            BookSearchOperator.LessThanOrEqual => query.Where(book => book.CurrentChapterNumber != null && book.CurrentChapterNumber <= value),
            _ => query.Where(book => book.CurrentChapterNumber != null && book.CurrentChapterNumber == value)
        };
    }

    private static IQueryable<Book> ApplyTotalChapters(IQueryable<Book> query, BookSearchOperator op, decimal value)
    {
        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(book => book.TotalChapters != null && book.TotalChapters > value),
            BookSearchOperator.GreaterThanOrEqual => query.Where(book => book.TotalChapters != null && book.TotalChapters >= value),
            BookSearchOperator.LessThan => query.Where(book => book.TotalChapters != null && book.TotalChapters < value),
            BookSearchOperator.LessThanOrEqual => query.Where(book => book.TotalChapters != null && book.TotalChapters <= value),
            _ => query.Where(book => book.TotalChapters != null && book.TotalChapters == value)
        };
    }

    private static BookListItemDto MapListProjection(BookListProjection projection)
    {
        return new BookListItemDto
        {
            Id = projection.Id,
            Created = projection.Created,
            LastModified = projection.LastModified,
            PrimaryTitle = projection.PrimaryTitle,
            Description = projection.Description,
            AlternativeTitles = projection.AlternativeTitles,
            AlternativeTitlesCount = projection.AlternativeTitlesCount,
            Author = projection.Author,
            ContentType = projection.ContentType,
            Status = projection.Status,
            CurrentChapterNumber = projection.CurrentChapterNumber,
            CurrentChapterLabel = projection.CurrentChapterLabel,
            TotalChapters = projection.TotalChapters,
            Rating = projection.Rating,
            Priority = projection.Priority,
            Notes = projection.Notes,
            Genres = projection.Genres,
            GenresCount = projection.GenresCount,
            Tags = projection.Tags,
            TagsCount = projection.TagsCount,
            LinksCount = projection.LinksCount,
            Cover = MapCoverProjection(projection)
        };
    }

    private static AdminBookListItemDto MapAdminListProjection(
        BookListProjection projection,
        IReadOnlyDictionary<Guid, BookOwnerProjection> owners)
    {
        var dto = MapListProjection(projection);
        owners.TryGetValue(projection.OwnerId, out var owner);
        return new AdminBookListItemDto
        {
            Id = dto.Id,
            Created = dto.Created,
            LastModified = dto.LastModified,
            PrimaryTitle = dto.PrimaryTitle,
            Description = dto.Description,
            AlternativeTitles = dto.AlternativeTitles,
            AlternativeTitlesCount = dto.AlternativeTitlesCount,
            Author = dto.Author,
            ContentType = dto.ContentType,
            Status = dto.Status,
            CurrentChapterNumber = dto.CurrentChapterNumber,
            CurrentChapterLabel = dto.CurrentChapterLabel,
            TotalChapters = dto.TotalChapters,
            Rating = dto.Rating,
            Priority = dto.Priority,
            Notes = dto.Notes,
            Cover = dto.Cover,
            Genres = dto.Genres,
            GenresCount = dto.GenresCount,
            Tags = dto.Tags,
            TagsCount = dto.TagsCount,
            LinksCount = dto.LinksCount,
            OwnerId = projection.OwnerId,
            OwnerUsername = owner?.Username,
            OwnerEmail = owner?.Email
        };
    }

    private static BookCoverDto? MapCoverProjection(BookListProjection projection)
    {
        if (!projection.CoverStatus.HasValue)
        {
            return null;
        }

        var version = projection.CoverLastAttemptAt
            ?? projection.CoverLastModified
            ?? projection.CoverCreated
            ?? DateTimeOffset.UnixEpoch;

        return new BookCoverDto
        {
            Status = projection.CoverStatus.Value.ToString(),
            Source = projection.CoverSource?.ToString(),
            FailureReason = projection.CoverFailureReason,
            LastAttemptAt = projection.CoverLastAttemptAt,
            ImageUrl = projection.HasCoverStoragePath ? $"/api/v1/book/{projection.Id}/cover/file?v={version.ToUnixTimeMilliseconds()}" : null,
            ThumbnailImageUrl = projection.HasCoverThumbnailStoragePath ? $"/api/v1/book/{projection.Id}/cover/thumbnail?v={version.ToUnixTimeMilliseconds()}" : null
        };
    }

    private sealed class BookListProjection
    {
        public Guid Id { get; init; }
        public DateTimeOffset Created { get; init; }
        public DateTimeOffset LastModified { get; init; }
        public Guid OwnerId { get; init; }
        public required string PrimaryTitle { get; init; }
        public string? Description { get; init; }
        public required List<string> AlternativeTitles { get; init; }
        public int AlternativeTitlesCount { get; init; }
        public string? Author { get; init; }
        public required string ContentType { get; init; }
        public required string Status { get; init; }
        public decimal? CurrentChapterNumber { get; init; }
        public string? CurrentChapterLabel { get; init; }
        public decimal? TotalChapters { get; init; }
        public int? Rating { get; init; }
        public int? Priority { get; init; }
        public string? Notes { get; init; }
        public required List<string> Genres { get; init; }
        public int GenresCount { get; init; }
        public required List<string> Tags { get; init; }
        public int TagsCount { get; init; }
        public int LinksCount { get; init; }
        public BookCoverStatus? CoverStatus { get; init; }
        public BookCoverSource? CoverSource { get; init; }
        public string? CoverFailureReason { get; init; }
        public DateTimeOffset? CoverLastAttemptAt { get; init; }
        public DateTimeOffset? CoverLastModified { get; init; }
        public DateTimeOffset? CoverCreated { get; init; }
        public bool HasCoverStoragePath { get; init; }
        public bool HasCoverThumbnailStoragePath { get; init; }
    }

    private sealed class BookOwnerProjection
    {
        public Guid Id { get; init; }
        public string? Username { get; init; }
        public string? Email { get; init; }
    }
}
