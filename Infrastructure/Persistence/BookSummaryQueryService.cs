namespace Infrastructure.Persistence;

using Application.Common;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Models;

public sealed class BookSummaryQueryService : IBookSummaryQueryService
{
    private readonly ApplicationDbContext _context;
    private readonly BookSearchCriteriaApplier _criteriaApplier;

    public BookSummaryQueryService(ApplicationDbContext context, BookSearchCriteriaApplier criteriaApplier)
    {
        _context = context;
        _criteriaApplier = criteriaApplier;
    }

    public async Task<BookSummarySnapshot> GetSummaryAsync(Guid ownerId, BookSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        IQueryable<Book> query =
            ApplyCriteria(_context.Books.AsNoTracking().Where(book => book.OwnerId == ownerId), criteria);

        int totalBooks = await query.CountAsync(cancellationToken);
        int ratedBooks = await query.CountAsync(book => book.Rating != null, cancellationToken);
        double? averageRating = ratedBooks == 0
            ? null
            : await query
                .Where(book => book.Rating != null)
                .Select(book => (double?)book.Rating)
                .AverageAsync(cancellationToken);
        decimal currentChapters = await query
            .Where(book => book.CurrentChapterNumber != null)
            .Select(book => book.CurrentChapterNumber ?? 0)
            .SumAsync(cancellationToken);
        int booksWithKnownCurrentChapter =
            await query.CountAsync(book => book.CurrentChapterNumber != null, cancellationToken);
        IReadOnlyList<BookStatusCountSnapshot> statusCounts = await GetStatusCountsAsync(query, cancellationToken);
        IReadOnlyList<BookTypeSummarySnapshot> typeCounts = await GetTypeCountsAsync(query, cancellationToken);
        IReadOnlyList<BookGenreCountSnapshot> genreCounts = await GetGenreCountsAsync(query, cancellationToken);
        IReadOnlyList<BookRatingCountSnapshot> ratingCounts = await GetRatingCountsAsync(query, cancellationToken);

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

    private IQueryable<Book> ApplyCriteria(IQueryable<Book> query, BookSearchCriteria criteria)
    {
        return criteria.HasFilters ? _criteriaApplier.Apply(query, criteria) : query;
    }

    private static async Task<IReadOnlyList<BookStatusCountSnapshot>> GetStatusCountsAsync(
        IQueryable<Book> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .Select(book => book.Status.Name)
            .GroupBy(status => status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.Status)
            .ToListAsync(cancellationToken);

        return rows.Select(group => new BookStatusCountSnapshot(group.Status, group.Count)).ToList();
    }

    private static async Task<IReadOnlyList<BookTypeSummarySnapshot>> GetTypeCountsAsync(
        IQueryable<Book> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(book => book.ContentType.Name)
            .Select(group => new
            {
                Type = group.Key,
                BookCount = group.Count(),
                CurrentChapters = group.Sum(book => book.CurrentChapterNumber ?? 0)
            })
            .OrderByDescending(group => group.BookCount)
            .ThenBy(group => group.Type)
            .ToListAsync(cancellationToken);

        return rows.Select(group => new BookTypeSummarySnapshot(group.Type, group.BookCount, group.CurrentChapters))
            .ToList();
    }

    private static async Task<IReadOnlyList<BookGenreCountSnapshot>> GetGenreCountsAsync(
        IQueryable<Book> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .SelectMany(book => book.BookGenres.Select(bookGenre => bookGenre.Genre.Name))
            .GroupBy(genre => genre)
            .Select(group => new { Genre = group.Key, BookCount = group.Count() })
            .OrderByDescending(group => group.BookCount)
            .ThenBy(group => group.Genre)
            .ToListAsync(cancellationToken);

        return rows.Select(group => new BookGenreCountSnapshot(group.Genre, group.BookCount)).ToList();
    }

    private static async Task<IReadOnlyList<BookRatingCountSnapshot>> GetRatingCountsAsync(
        IQueryable<Book> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .Where(book => book.Rating != null)
            .GroupBy(book => book.Rating!.Value)
            .Select(group => new { Rating = group.Key, BookCount = group.Count() })
            .OrderBy(group => group.Rating)
            .ToListAsync(cancellationToken);

        return rows.Select(group => new BookRatingCountSnapshot(group.Rating, group.BookCount)).ToList();
    }
}
