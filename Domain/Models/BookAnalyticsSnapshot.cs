namespace Domain.Models;

public sealed record BookAnalyticsSnapshot(
    DateTimeOffset GeneratedAt,
    BookAnalyticsScopeSnapshot Scope,
    BookAnalyticsOverviewSnapshot Overview,
    BookAnalyticsCompositionSnapshot Composition,
    BookAnalyticsRatingsSnapshot Ratings,
    BookAnalyticsPlanningSnapshot Planning);

public sealed record BookAnalyticsScopeSnapshot(
    string? Query,
    DateOnly From,
    DateOnly To,
    string Bucket);

public sealed record BookAnalyticsOverviewSnapshot(
    int TotalBooks,
    int RatedBooks,
    int UnratedBooks,
    double? AverageRating,
    decimal CurrentChapters,
    int BooksWithKnownCurrentChapter,
    int BooksWithoutKnownCurrentChapter);

public sealed record BookAnalyticsCompositionSnapshot(
    IReadOnlyList<BookAnalyticsStatusByTypeSnapshot> StatusByType,
    IReadOnlyList<BookAnalyticsRelationCountSnapshot> Genres,
    IReadOnlyList<BookAnalyticsRelationCountSnapshot> Tags)
{
    public static BookAnalyticsCompositionSnapshot Empty { get; } = new([], [], []);
}

public sealed record BookAnalyticsStatusByTypeSnapshot(
    string Type,
    int TotalBooks,
    IReadOnlyList<BookAnalyticsStatusCountSnapshot> Statuses);

public sealed record BookAnalyticsStatusCountSnapshot(string Status, int BookCount);

public sealed record BookAnalyticsRelationCountSnapshot(string Name, int BookCount, double ShareOfBooks);

public sealed record BookAnalyticsRatingsSnapshot(
    int RatedBooks,
    int UnratedBooks,
    double? AverageRating,
    IReadOnlyList<BookAnalyticsRatingCountSnapshot> Counts)
{
    public static BookAnalyticsRatingsSnapshot Empty { get; } = new(0, 0, null, CreateEmptyCounts());

    public static IReadOnlyList<BookAnalyticsRatingCountSnapshot> CreateEmptyCounts()
    {
        return Enumerable.Range(1, 10)
            .Select(rating => new BookAnalyticsRatingCountSnapshot(rating, 0))
            .ToList();
    }
}

public sealed record BookAnalyticsRatingCountSnapshot(int Rating, int BookCount);

public sealed record BookAnalyticsPlanningSnapshot(
    IReadOnlyList<BookAnalyticsPrioritiesByStatusSnapshot> PrioritiesByStatus)
{
    public static BookAnalyticsPlanningSnapshot Empty { get; } = new([]);
}

public sealed record BookAnalyticsPrioritiesByStatusSnapshot(
    string Status,
    int TotalBooks,
    IReadOnlyList<BookAnalyticsPriorityCountSnapshot> Priorities);

public sealed record BookAnalyticsPriorityCountSnapshot(string Priority, int BookCount);
