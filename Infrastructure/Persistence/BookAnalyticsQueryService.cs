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

        return new BookAnalyticsSnapshot(
            DateTimeOffset.UtcNow,
            scope,
            overview,
            BookAnalyticsCompositionSnapshot.Empty);
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
}
