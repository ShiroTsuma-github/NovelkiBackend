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

        return new BookAnalyticsSnapshot(
            DateTimeOffset.UtcNow,
            scope,
            overview,
            composition);
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

    private sealed record RelationCountRow(string Name, int BookCount);
}
