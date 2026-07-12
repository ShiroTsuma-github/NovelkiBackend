namespace Infrastructure.Persistence;

using Application.Common;
using Domain.Models;
using System.Linq.Expressions;
using FluentValidation;

public class BookRepository : IBookRepository
{
    private readonly ApplicationDbContext _context;
    private readonly bool _supportsILike;

    public BookRepository(ApplicationDbContext context)
    {
        _context = context;
        _supportsILike = context.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
    }

    public async Task<Book?> GetByIdAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
    {
        return await IncludeDetails(_context.Books)
            .FirstOrDefaultAsync(b => b.Id == id && b.OwnerId == ownerId, cancellationToken);
    }

    public async Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await IncludeDetails(_context.Books)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<Book?> GetForUpdateAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
    {
        return await _context.Books
            .FirstOrDefaultAsync(b => b.Id == id && b.OwnerId == ownerId, cancellationToken);
    }

    public async Task<Book?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Books
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<Book?> GetByNameAsync(string name, Guid ownerId, Guid contentTypeId, CancellationToken cancellationToken)
    {
        var normalizedName = MappingExtensions.NormalizeName(name);
        return await _context.Books
            .AsNoTracking()
            .FirstOrDefaultAsync(
                b => b.OwnerId == ownerId &&
                     b.ContentTypeId == contentTypeId &&
                     (b.NormalizedPrimaryTitle == normalizedName ||
                      b.Titles.Any(t => t.NormalizedTitle == normalizedName)),
                cancellationToken);
    }

    public async Task<IEnumerable<Book>> GetAllAsync(Guid ownerId, int Skip, int Take, CancellationToken cancellationToken)
    {
        return await GetAllAsync(ownerId, Skip, Take, null, null, cancellationToken);
    }

    public async Task<IEnumerable<Book>> GetAllAsync(Guid ownerId, int Skip, int Take, string? SortBy, string? SortDirection, CancellationToken cancellationToken)
    {
        return await ToSortedPageAsync(
            IncludeDetails(_context.Books).Where(b => b.OwnerId == ownerId),
            Skip,
            Take,
            SortBy,
            SortDirection,
            cancellationToken);
    }

    public async Task<IEnumerable<Book>> GetAllAsync(int Skip, int Take, CancellationToken cancellationToken)
    {
        return await GetAllAsync(Skip, Take, null, null, cancellationToken);
    }

    public async Task<IEnumerable<Book>> GetAllAsync(int Skip, int Take, string? SortBy, string? SortDirection, CancellationToken cancellationToken)
    {
        return await ToSortedPageAsync(IncludeDetails(_context.Books), Skip, Take, SortBy, SortDirection, cancellationToken);
    }

    public async Task<IEnumerable<Book>> SearchAsync(Guid ownerId, BookSearchCriteria criteria, int Skip, int Take, CancellationToken cancellationToken)
    {
        return await SearchAsync(ownerId, criteria, Skip, Take, null, null, cancellationToken);
    }

    public async Task<IEnumerable<Book>> SearchAsync(Guid ownerId, BookSearchCriteria criteria, int Skip, int Take, string? SortBy, string? SortDirection, CancellationToken cancellationToken)
    {
        return await ToSortedPageAsync(
            ApplyCriteria(IncludeDetails(_context.Books).Where(b => b.OwnerId == ownerId), criteria),
            Skip,
            Take,
            SortBy,
            SortDirection,
            cancellationToken);
    }

    public async Task<IEnumerable<Book>> SearchAsync(BookSearchCriteria criteria, int Skip, int Take, CancellationToken cancellationToken)
    {
        return await SearchAsync(criteria, Skip, Take, null, null, cancellationToken);
    }

    public async Task<IEnumerable<Book>> SearchAsync(BookSearchCriteria criteria, int Skip, int Take, string? SortBy, string? SortDirection, CancellationToken cancellationToken)
    {
        return await ToSortedPageAsync(ApplyCriteria(IncludeDetails(_context.Books), criteria), Skip, Take, SortBy, SortDirection, cancellationToken);
    }

    public async Task AddAsync(Book book, CancellationToken cancellationToken)
    {
        _context.Books.Add(book);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
    {
        var book = await _context.Books.FirstOrDefaultAsync(b => b.Id == id && b.OwnerId == ownerId, cancellationToken);
        if (book != null)
        {
            _context.Books.Remove(book);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<int> GetCountAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        return _context.Books.CountAsync(b => b.OwnerId == ownerId, cancellationToken);
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken)
    {
        return _context.Books.CountAsync(cancellationToken);
    }

    public Task<int> GetSearchCountAsync(Guid ownerId, BookSearchCriteria criteria, CancellationToken cancellationToken)
    {
        return ApplyCriteria(_context.Books.Where(b => b.OwnerId == ownerId), criteria)
            .CountAsync(cancellationToken);
    }

    public Task<int> GetSearchCountAsync(BookSearchCriteria criteria, CancellationToken cancellationToken)
    {
        return ApplyCriteria(_context.Books, criteria)
            .CountAsync(cancellationToken);
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
        var query = IncludeDetails(_context.Books).Where(b => b.OwnerId == ownerId);
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

    public async Task<bool> UpdateProgressAsync(
        Guid id,
        Guid ownerId,
        decimal? currentChapterNumber,
        string? currentChapterLabel,
        string? comment,
        CancellationToken cancellationToken)
    {
        var book = await _context.Books
            .FirstOrDefaultAsync(b => b.Id == id && b.OwnerId == ownerId, cancellationToken);
        if (book == null)
        {
            return false;
        }

        var progressChanged = book.CurrentChapterNumber != currentChapterNumber ||
                              book.CurrentChapterLabel != currentChapterLabel;
        var hasComment = !string.IsNullOrWhiteSpace(comment);
        if (book.TotalChapters.HasValue && book.TotalChapters > 0 && currentChapterNumber.HasValue && currentChapterNumber > book.TotalChapters)
        {
            throw new ValidationException("Current chapter cannot be greater than total chapters.");
        }

        book.CurrentChapterNumber = currentChapterNumber;
        book.CurrentChapterLabel = currentChapterLabel;

        if (progressChanged || hasComment)
        {
            _context.BookProgressHistory.Add(new BookProgressHistory
            {
                BookId = book.Id,
                ChapterNumber = currentChapterNumber,
                ChapterLabel = currentChapterLabel,
                Comment = comment
            });
        }

        await SaveAsync(cancellationToken);
        return true;
    }

    public async Task<decimal?> GetTotalChaptersAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
    {
        return await _context.Books
            .Where(b => b.Id == id && b.OwnerId == ownerId)
            .Select(b => b.TotalChapters)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task ReplaceEditableCollectionsAsync(
        Guid bookId,
        IEnumerable<BookTitle> titles,
        IEnumerable<BookLink> links,
        IEnumerable<Guid> genreIds,
        IEnumerable<Guid> tagIds,
        BookProgressHistory? progressHistory,
        CancellationToken cancellationToken)
    {
        await _context.BookTitles.Where(t => t.BookId == bookId).ExecuteDeleteAsync(cancellationToken);
        await _context.BookLinks.Where(l => l.BookId == bookId).ExecuteDeleteAsync(cancellationToken);
        await _context.Set<BookGenre>().Where(bg => bg.BookId == bookId).ExecuteDeleteAsync(cancellationToken);
        await _context.Set<BookTag>().Where(bt => bt.BookId == bookId).ExecuteDeleteAsync(cancellationToken);
        DetachTrackedEditableCollections(bookId);

        _context.BookTitles.AddRange(titles.Select(t =>
        {
            t.BookId = bookId;
            return t;
        }));
        _context.BookLinks.AddRange(links.Select(l =>
        {
            l.BookId = bookId;
            return l;
        }));
        _context.Set<BookGenre>().AddRange(genreIds.Distinct().Select(genreId => new BookGenre
        {
            BookId = bookId,
            GenreId = genreId
        }));
        _context.Set<BookTag>().AddRange(tagIds.Distinct().Select(tagId => new BookTag
        {
            BookId = bookId,
            TagId = tagId
        }));

        if (progressHistory != null)
        {
            progressHistory.BookId = bookId;
            _context.BookProgressHistory.Add(progressHistory);
        }
    }

    private void DetachTrackedEditableCollections(Guid bookId)
    {
        foreach (var entry in _context.ChangeTracker.Entries<BookTitle>().Where(e => e.Entity.BookId == bookId).ToList())
        {
            entry.State = EntityState.Detached;
        }

        foreach (var entry in _context.ChangeTracker.Entries<BookLink>().Where(e => e.Entity.BookId == bookId).ToList())
        {
            entry.State = EntityState.Detached;
        }

        foreach (var entry in _context.ChangeTracker.Entries<BookGenre>().Where(e => e.Entity.BookId == bookId).ToList())
        {
            entry.State = EntityState.Detached;
        }

        foreach (var entry in _context.ChangeTracker.Entries<BookTag>().Where(e => e.Entity.BookId == bookId).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private static IQueryable<Book> IncludeDetails(IQueryable<Book> query)
    {
        return query
            .Include(b => b.Author).ThenInclude(a => a!.Names)
            .Include(b => b.Cover)
            .Include(b => b.ContentType)
            .Include(b => b.Status)
            .Include(b => b.Titles)
            .Include(b => b.BookGenres).ThenInclude(bg => bg.Genre)
            .Include(b => b.BookTags).ThenInclude(bt => bt.Tag)
            .Include(b => b.Links)
            .Include(b => b.ProgressHistory);
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
                    ? books.OrderByDescending(b => b.Created).ThenBy(b => b.PrimaryTitle)
                    : books.OrderBy(b => b.Created).ThenBy(b => b.PrimaryTitle)
                : descending
                    ? books.OrderByDescending(b => b.LastModified).ThenBy(b => b.PrimaryTitle)
                    : books.OrderBy(b => b.LastModified).ThenBy(b => b.PrimaryTitle);
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
                ? query.OrderByDescending(b => b.NormalizedPrimaryTitle).ThenByDescending(b => b.PrimaryTitle).ThenByDescending(b => b.Id)
                : query.OrderBy(b => b.NormalizedPrimaryTitle).ThenBy(b => b.PrimaryTitle).ThenBy(b => b.Id);
            case "author":
                return descending
                ? query.OrderByDescending(b => b.Author != null ? b.Author.PrimaryName : "").ThenBy(b => b.PrimaryTitle)
                : query.OrderBy(b => b.Author != null ? b.Author.PrimaryName : "").ThenBy(b => b.PrimaryTitle);
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
                ? query.OrderByDescending(b => b.CurrentChapterNumber).ThenBy(b => b.PrimaryTitle)
                : query.OrderBy(b => b.CurrentChapterNumber).ThenBy(b => b.PrimaryTitle);
            case "rating":
                return descending
                ? query.OrderBy(b => b.Rating == null).ThenByDescending(b => b.Rating).ThenBy(b => b.PrimaryTitle)
                : query.OrderBy(b => b.Rating == null).ThenBy(b => b.Rating).ThenBy(b => b.PrimaryTitle);
            case "priority":
                return descending
                ? query.OrderByDescending(b => b.Priority).ThenBy(b => b.PrimaryTitle)
                : query.OrderBy(b => b.Priority).ThenBy(b => b.PrimaryTitle);
            case "owner":
                return descending
                ? query.OrderByDescending(b => b.OwnerId).ThenBy(b => b.PrimaryTitle)
                : query.OrderBy(b => b.OwnerId).ThenBy(b => b.PrimaryTitle);
            case "created":
                return descending
                ? query.OrderByDescending(b => b.Created).ThenBy(b => b.PrimaryTitle)
                : query.OrderBy(b => b.Created).ThenBy(b => b.PrimaryTitle);
            case "lastmodified":
                return descending
                ? query.OrderByDescending(b => b.LastModified).ThenBy(b => b.PrimaryTitle)
                : query.OrderBy(b => b.LastModified).ThenBy(b => b.PrimaryTitle);
            default:
                return query.OrderByDescending(b => b.LastModified).ThenBy(b => b.PrimaryTitle);
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
            return query.OrderBy(b => b.PrimaryTitle).ThenBy(b => b.Id);
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
        return query.OrderBy(rankSelector).ThenBy(b => b.PrimaryTitle).ThenBy(b => b.Id);
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
            return query.Where(b =>
                EF.Functions.ILike(b.PrimaryTitle, pattern) ||
                b.Titles.Any(t => EF.Functions.ILike(t.Title, pattern)) ||
                (b.Author != null && (
                    EF.Functions.ILike(b.Author.PrimaryName, pattern) ||
                    b.Author.Names.Any(n => EF.Functions.ILike(n.Name, pattern)))));
        }

        var normalizedPattern = ToLikePattern(MappingExtensions.NormalizeName(term));
        return query.Where(b =>
            EF.Functions.Like(b.NormalizedPrimaryTitle, normalizedPattern) ||
            b.Titles.Any(t => EF.Functions.Like(t.NormalizedTitle, normalizedPattern)) ||
            (b.Author != null && (
                EF.Functions.Like(b.Author.NormalizedPrimaryName, normalizedPattern) ||
                b.Author.Names.Any(n => EF.Functions.Like(n.NormalizedName, normalizedPattern)))));
    }

    private IQueryable<Book> ApplyTitleSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        var predicates = _supportsILike
            ? searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var pattern = ToLikePattern(search);
                return b =>
                    EF.Functions.ILike(b.PrimaryTitle, pattern) ||
                    b.Titles.Any(t => EF.Functions.ILike(t.Title, pattern));
            })
            : searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var normalizedPattern = ToLikePattern(MappingExtensions.NormalizeName(search));
                return b =>
                    EF.Functions.Like(b.NormalizedPrimaryTitle, normalizedPattern) ||
                    b.Titles.Any(t => EF.Functions.Like(t.NormalizedTitle, normalizedPattern));
            });

        return ApplyAnyFieldMatch(query, predicates);
    }

    private IQueryable<Book> ApplyAuthorSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        var predicates = _supportsILike
            ? searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var pattern = ToLikePattern(search);
                return b => b.Author != null && (
                    EF.Functions.ILike(b.Author.PrimaryName, pattern) ||
                    b.Author.Names.Any(n => EF.Functions.ILike(n.Name, pattern)));
            })
            : searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var normalizedPattern = ToLikePattern(MappingExtensions.NormalizeName(search));
                return b => b.Author != null && (
                    EF.Functions.Like(b.Author.NormalizedPrimaryName, normalizedPattern) ||
                    b.Author.Names.Any(n => EF.Functions.Like(n.NormalizedName, normalizedPattern)));
            });

        return ApplyAnyFieldMatch(query, predicates);
    }

    private IQueryable<Book> ApplyTagSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        var predicates = _supportsILike
            ? searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var pattern = ToLikePattern(search);
                return b => b.BookTags.Any(bt => EF.Functions.ILike(bt.Tag.Name, pattern));
            })
            : searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var normalizedPattern = ToLikePattern(MappingExtensions.NormalizeName(search));
                return b => b.BookTags.Any(bt => EF.Functions.Like(bt.Tag.NormalizedName, normalizedPattern));
            });

        return ApplyAnyFieldMatch(query, predicates);
    }

    private IQueryable<Book> ApplyGenreSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        var predicates = _supportsILike
            ? searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var pattern = ToLikePattern(search);
                return b => b.BookGenres.Any(bg => EF.Functions.ILike(bg.Genre.Name, pattern));
            })
            : searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var normalizedPattern = ToLikePattern(MappingExtensions.NormalizeName(search));
                return b => b.BookGenres.Any(bg => EF.Functions.Like(bg.Genre.NormalizedName, normalizedPattern));
            });

        return ApplyAnyFieldMatch(query, predicates);
    }

    private IQueryable<Book> ApplyStatusSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        var predicates = _supportsILike
            ? searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var pattern = ToLikePattern(search);
                return b => EF.Functions.ILike(b.Status.Name, pattern);
            })
            : searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var normalizedPattern = ToLikePattern(MappingExtensions.NormalizeName(search));
                return b => EF.Functions.Like(b.Status.Name.ToUpper(), normalizedPattern);
            });

        return ApplyAnyFieldMatch(query, predicates);
    }

    private IQueryable<Book> ApplyTypeSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        var predicates = _supportsILike
            ? searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var pattern = ToLikePattern(search);
                return b => EF.Functions.ILike(b.ContentType.Name, pattern);
            })
            : searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var normalizedPattern = ToLikePattern(MappingExtensions.NormalizeName(search));
                return b => EF.Functions.Like(b.ContentType.Name.ToUpper(), normalizedPattern);
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
        var parameter = Expression.Parameter(typeof(Book), "b");
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
            BookSearchOperator.GreaterThan => query.Where(b => b.Rating != null && (decimal)b.Rating.Value > value),
            BookSearchOperator.GreaterThanOrEqual => query.Where(b => b.Rating != null && (decimal)b.Rating.Value >= value),
            BookSearchOperator.LessThan => query.Where(b => b.Rating != null && (decimal)b.Rating.Value < value),
            BookSearchOperator.LessThanOrEqual => query.Where(b => b.Rating != null && (decimal)b.Rating.Value <= value),
            _ => query.Where(b => b.Rating != null && (decimal)b.Rating.Value == value)
        };
    }

    private static IQueryable<Book> ApplyPriority(IQueryable<Book> query, BookSearchOperator op, decimal value)
    {
        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(b => b.Priority != null && (decimal)b.Priority.Value > value),
            BookSearchOperator.GreaterThanOrEqual => query.Where(b => b.Priority != null && (decimal)b.Priority.Value >= value),
            BookSearchOperator.LessThan => query.Where(b => b.Priority != null && (decimal)b.Priority.Value < value),
            BookSearchOperator.LessThanOrEqual => query.Where(b => b.Priority != null && (decimal)b.Priority.Value <= value),
            _ => query.Where(b => b.Priority != null && (decimal)b.Priority.Value == value)
        };
    }

    private static IQueryable<Book> ApplyCurrentChapter(IQueryable<Book> query, BookSearchOperator op, decimal value)
    {
        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(b => b.CurrentChapterNumber != null && b.CurrentChapterNumber > value),
            BookSearchOperator.GreaterThanOrEqual => query.Where(b => b.CurrentChapterNumber != null && b.CurrentChapterNumber >= value),
            BookSearchOperator.LessThan => query.Where(b => b.CurrentChapterNumber != null && b.CurrentChapterNumber < value),
            BookSearchOperator.LessThanOrEqual => query.Where(b => b.CurrentChapterNumber != null && b.CurrentChapterNumber <= value),
            _ => query.Where(b => b.CurrentChapterNumber != null && b.CurrentChapterNumber == value)
        };
    }

    private static IQueryable<Book> ApplyTotalChapters(IQueryable<Book> query, BookSearchOperator op, decimal value)
    {
        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(b => b.TotalChapters != null && b.TotalChapters > value),
            BookSearchOperator.GreaterThanOrEqual => query.Where(b => b.TotalChapters != null && b.TotalChapters >= value),
            BookSearchOperator.LessThan => query.Where(b => b.TotalChapters != null && b.TotalChapters < value),
            BookSearchOperator.LessThanOrEqual => query.Where(b => b.TotalChapters != null && b.TotalChapters <= value),
            _ => query.Where(b => b.TotalChapters != null && b.TotalChapters == value)
        };
    }
}
