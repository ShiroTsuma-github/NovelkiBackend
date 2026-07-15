namespace Infrastructure.Persistence;

using Domain.Models;

public sealed class BookAnalyticsQueryService : IBookAnalyticsQueryService
{
    private readonly ApplicationDbContext _context;
    private readonly BookSearchCriteriaApplier _criteriaApplier;

    public BookAnalyticsQueryService(ApplicationDbContext context, BookSearchCriteriaApplier criteriaApplier)
    {
        _context = context;
        _criteriaApplier = criteriaApplier;
    }

    public async Task<BookAnalyticsSnapshot> GetAnalyticsAsync(
        Guid ownerId,
        BookSearchCriteria criteria,
        BookAnalyticsScopeSnapshot scope,
        CancellationToken cancellationToken)
    {
        var query = ApplyCriteria(_context.Books.AsNoTracking().Where(book => book.OwnerId == ownerId), criteria);
        var overview = await GetOverviewAsync(query, cancellationToken);
        var composition = await GetCompositionAsync(query, overview.TotalBooks, cancellationToken);
        var ratings = await GetRatingsAsync(query, overview, cancellationToken);
        var planning = await GetPlanningAsync(query, overview.TotalBooks, cancellationToken);
        var progress = await GetProgressAsync(query, overview.TotalBooks, cancellationToken);
        var activity = await GetActivityAsync(query, scope, overview.TotalBooks, cancellationToken);

        return new BookAnalyticsSnapshot(
            DateTimeOffset.UtcNow,
            scope,
            overview,
            composition,
            ratings,
            planning,
            progress,
            activity);
    }

    private IQueryable<Book> ApplyCriteria(IQueryable<Book> query, BookSearchCriteria criteria)
    {
        return criteria.HasFilters ? _criteriaApplier.Apply(query, criteria) : query;
    }

    private static async Task<BookAnalyticsOverviewSnapshot> GetOverviewAsync(
        IQueryable<Book> query,
        CancellationToken cancellationToken)
    {
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

        return new BookAnalyticsOverviewSnapshot(
            totalBooks,
            ratedBooks,
            totalBooks - ratedBooks,
            averageRating,
            currentChapters,
            booksWithKnownCurrentChapter,
            totalBooks - booksWithKnownCurrentChapter);
    }

    private async Task<BookAnalyticsCompositionSnapshot> GetCompositionAsync(
        IQueryable<Book> query,
        int totalBooks,
        CancellationToken cancellationToken)
    {
        if (totalBooks == 0)
        {
            return BookAnalyticsCompositionSnapshot.Empty;
        }

        var statusByType = await GetStatusByTypeAsync(query, cancellationToken);
        var bookIds = query.Select(book => book.Id);
        var genres = await GetGenreCountsAsync(bookIds, totalBooks, cancellationToken);
        var tags = await GetTagCountsAsync(bookIds, totalBooks, cancellationToken);

        return new BookAnalyticsCompositionSnapshot(statusByType, genres, tags);
    }

    private static async Task<IReadOnlyList<BookAnalyticsStatusByTypeSnapshot>> GetStatusByTypeAsync(
        IQueryable<Book> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(book => new { Type = book.ContentType.Name, Status = book.Status.Name })
            .Select(group => new
            {
                group.Key.Type,
                group.Key.Status,
                BookCount = group.Count(),
            })
            .OrderBy(group => group.Type)
            .ThenByDescending(group => group.BookCount)
            .ThenBy(group => group.Status)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.Type)
            .Select(group => new BookAnalyticsStatusByTypeSnapshot(
                group.Key,
                group.Sum(row => row.BookCount),
                group
                    .Select(row => new BookAnalyticsStatusCountSnapshot(row.Status, row.BookCount))
                    .ToList()))
            .OrderByDescending(group => group.TotalBooks)
            .ThenBy(group => group.Type)
            .ToList();
    }

    private async Task<IReadOnlyList<BookAnalyticsRelationCountSnapshot>> GetGenreCountsAsync(
        IQueryable<Guid> bookIds,
        int totalBooks,
        CancellationToken cancellationToken)
    {
        var rows = await _context.Set<BookGenre>()
            .AsNoTracking()
            .Where(bookGenre => bookIds.Contains(bookGenre.BookId))
            .Select(bookGenre => new { Id = bookGenre.BookId, Name = bookGenre.Genre.Name })
            .Distinct()
            .GroupBy(row => row.Name)
            .Select(group => new
            {
                Name = group.Key,
                BookCount = group.Count(),
            })
            .OrderByDescending(group => group.BookCount)
            .ThenBy(group => group.Name)
            .ToListAsync(cancellationToken);

        return ToRelationSnapshots(rows.Select(row => new RelationCountRow(row.Name, row.BookCount)), totalBooks);
    }

    private async Task<IReadOnlyList<BookAnalyticsRelationCountSnapshot>> GetTagCountsAsync(
        IQueryable<Guid> bookIds,
        int totalBooks,
        CancellationToken cancellationToken)
    {
        var rows = await _context.Set<BookTag>()
            .AsNoTracking()
            .Where(bookTag => bookIds.Contains(bookTag.BookId))
            .Select(bookTag => new { Id = bookTag.BookId, Name = bookTag.Tag.Name })
            .Distinct()
            .GroupBy(row => row.Name)
            .Select(group => new
            {
                Name = group.Key,
                BookCount = group.Count(),
            })
            .OrderByDescending(group => group.BookCount)
            .ThenBy(group => group.Name)
            .ToListAsync(cancellationToken);

        return ToRelationSnapshots(rows.Select(row => new RelationCountRow(row.Name, row.BookCount)), totalBooks);
    }

    private static IReadOnlyList<BookAnalyticsRelationCountSnapshot> ToRelationSnapshots(
        IEnumerable<RelationCountRow> rows,
        int totalBooks)
    {
        return rows
            .Select(row => new BookAnalyticsRelationCountSnapshot(
                row.Name,
                row.BookCount,
                totalBooks == 0 ? 0 : (double)row.BookCount / totalBooks))
            .ToList();
    }

    private static async Task<BookAnalyticsRatingsSnapshot> GetRatingsAsync(
        IQueryable<Book> query,
        BookAnalyticsOverviewSnapshot overview,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .Where(book => book.Rating != null)
            .GroupBy(book => book.Rating!.Value)
            .Select(group => new
            {
                Rating = group.Key,
                BookCount = group.Count(),
            })
            .ToListAsync(cancellationToken);

        var countsByRating = rows.ToDictionary(row => row.Rating, row => row.BookCount);
        var counts = Enumerable.Range(1, 10)
            .Select(rating => new BookAnalyticsRatingCountSnapshot(
                rating,
                countsByRating.GetValueOrDefault(rating)))
            .ToList();

        return new BookAnalyticsRatingsSnapshot(
            overview.RatedBooks,
            overview.UnratedBooks,
            overview.AverageRating,
            counts);
    }

    private static async Task<BookAnalyticsPlanningSnapshot> GetPlanningAsync(
        IQueryable<Book> query,
        int totalBooks,
        CancellationToken cancellationToken)
    {
        if (totalBooks == 0)
        {
            return BookAnalyticsPlanningSnapshot.Empty;
        }

        var rows = await query
            .GroupBy(book => new
            {
                Status = book.Status.Name,
                Priority = book.Priority,
            })
            .Select(group => new
            {
                group.Key.Status,
                group.Key.Priority,
                BookCount = group.Count(),
            })
            .ToListAsync(cancellationToken);

        var prioritiesByStatus = rows
            .GroupBy(row => row.Status)
            .Select(group =>
            {
                var countsByPriority = group.ToDictionary(
                    row => row.Priority?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "Unset",
                    row => row.BookCount);
                var priorities = Enumerable.Range(1, 5)
                    .Select(priority => new BookAnalyticsPriorityCountSnapshot(
                        priority.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        countsByPriority.GetValueOrDefault(priority.ToString(System.Globalization.CultureInfo.InvariantCulture))))
                    .Append(new BookAnalyticsPriorityCountSnapshot("Unset", countsByPriority.GetValueOrDefault("Unset")))
                    .ToList();

                return new BookAnalyticsPrioritiesByStatusSnapshot(
                    group.Key,
                    group.Sum(row => row.BookCount),
                    priorities);
            })
            .OrderByDescending(group => group.TotalBooks)
            .ThenBy(group => group.Status)
            .ToList();

        return new BookAnalyticsPlanningSnapshot(prioritiesByStatus);
    }

    private static async Task<BookAnalyticsProgressSnapshot> GetProgressAsync(
        IQueryable<Book> query,
        int totalBooks,
        CancellationToken cancellationToken)
    {
        if (totalBooks == 0)
        {
            return BookAnalyticsProgressSnapshot.Empty;
        }

        // Portable median projection: EF providers differ on percentile/median support,
        // so keep the database work to the filtered scalar projection and compute
        // per-type medians from chapter values only.
        var rows = await query
            .Select(book => new
            {
                Type = book.ContentType.Name,
                book.CurrentChapterNumber,
            })
            .OrderBy(row => row.Type)
            .ToListAsync(cancellationToken);

        return new BookAnalyticsProgressSnapshot(rows
            .GroupBy(row => row.Type)
            .Select(group =>
            {
                var knownChapters = group
                    .Where(row => row.CurrentChapterNumber != null)
                    .Select(row => row.CurrentChapterNumber!.Value)
                    .OrderBy(value => value)
                    .ToList();
                var sum = knownChapters.Sum();

                return new BookAnalyticsTypeVolumeSnapshot(
                    group.Key,
                    group.Count(),
                    sum,
                    knownChapters.Count == 0 ? null : sum / knownChapters.Count,
                    CalculateMedian(knownChapters));
            })
            .OrderByDescending(item => item.BookCount)
            .ThenBy(item => item.Type)
            .ToList());
    }

    private static decimal? CalculateMedian(IReadOnlyList<decimal> sortedValues)
    {
        if (sortedValues.Count == 0)
        {
            return null;
        }

        var middle = sortedValues.Count / 2;
        if (sortedValues.Count % 2 == 1)
        {
            return sortedValues[middle];
        }

        return (sortedValues[middle - 1] + sortedValues[middle]) / 2m;
    }

    private async Task<BookAnalyticsActivitySnapshot> GetActivityAsync(
        IQueryable<Book> query,
        BookAnalyticsScopeSnapshot scope,
        int totalBooks,
        CancellationToken cancellationToken)
    {
        if (totalBooks == 0)
        {
            return BookAnalyticsActivitySnapshot.Empty;
        }

        var bookIds = query.Select(book => book.Id);
        var rows = await _context.BookProgressHistory
            .AsNoTracking()
            .Where(history => bookIds.Contains(history.BookId))
            .Select(history => new ProgressHistoryRow(
                history.BookId,
                history.Id,
                history.ChangedAt,
                history.ChapterNumber,
                history.ChapterLabel))
            .ToListAsync(cancellationToken);

        var points = CreateEmptyActivityPoints(scope);
        foreach (var group in rows
            .OrderBy(row => row.BookId)
            .ThenBy(row => row.ChangedAt)
            .ThenBy(row => row.Id)
            .GroupBy(row => row.BookId))
        {
            ProgressHistoryRow? previous = null;
            foreach (var row in group)
            {
                if (previous is null)
                {
                    previous = row;
                    continue;
                }

                var changedDate = DateOnly.FromDateTime(row.ChangedAt.UtcDateTime);
                if (changedDate >= scope.From && changedDate < scope.To)
                {
                    var bucket = ResolveBucketDate(changedDate, scope.From, scope.Bucket);
                    if (points.TryGetValue(bucket, out var point))
                    {
                        point.ProgressEvents++;
                        point.BookIds.Add(row.BookId);
                        point.ChaptersAdvanced += CalculatePositiveChapterAdvance(previous.ChapterNumber, row.ChapterNumber);
                    }
                }

                previous = row;
            }
        }

        return new BookAnalyticsActivitySnapshot(points
            .OrderBy(point => point.Key)
            .Select(point => new BookAnalyticsActivityPointSnapshot(
                point.Key,
                point.Value.ProgressEvents,
                point.Value.BookIds.Count,
                point.Value.ChaptersAdvanced))
            .ToList());
    }

    private static SortedDictionary<DateOnly, ActivityPointAccumulator> CreateEmptyActivityPoints(
        BookAnalyticsScopeSnapshot scope)
    {
        var points = new SortedDictionary<DateOnly, ActivityPointAccumulator>();
        for (var bucket = ResolveBucketDate(scope.From, scope.From, scope.Bucket);
             bucket < scope.To;
             bucket = NextBucketDate(bucket, scope.Bucket))
        {
            points[bucket] = new ActivityPointAccumulator();
        }

        return points;
    }

    private static DateOnly ResolveBucketDate(DateOnly date, DateOnly from, string bucket)
    {
        var bucketDate = bucket switch
        {
            "day" => date,
            "week" => StartOfWeek(date),
            "month" => new DateOnly(date.Year, date.Month, 1),
            _ => date,
        };

        return bucketDate < from ? from : bucketDate;
    }

    private static DateOnly NextBucketDate(DateOnly bucket, string bucketSize)
    {
        return bucketSize switch
        {
            "day" => bucket.AddDays(1),
            "week" => StartOfWeek(bucket).AddDays(7),
            "month" => new DateOnly(bucket.Year, bucket.Month, 1).AddMonths(1),
            _ => bucket.AddDays(1),
        };
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-daysSinceMonday);
    }

    private static decimal CalculatePositiveChapterAdvance(decimal? previous, decimal? current)
    {
        if (previous is null || current is null || current <= previous)
        {
            return 0m;
        }

        return current.Value - previous.Value;
    }

    private sealed record RelationCountRow(string Name, int BookCount);

    private sealed record ProgressHistoryRow(
        Guid BookId,
        Guid Id,
        DateTimeOffset ChangedAt,
        decimal? ChapterNumber,
        string? ChapterLabel);

    private sealed class ActivityPointAccumulator
    {
        public int ProgressEvents { get; set; }
        public HashSet<Guid> BookIds { get; } = [];
        public decimal ChaptersAdvanced { get; set; }
    }
}
