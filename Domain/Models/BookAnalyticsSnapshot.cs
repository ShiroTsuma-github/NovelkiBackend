namespace Domain.Models;

public sealed record BookAnalyticsSnapshot(
    DateTimeOffset GeneratedAt,
    BookAnalyticsScopeSnapshot Scope,
    BookAnalyticsOverviewSnapshot Overview,
    BookAnalyticsCompositionSnapshot Composition);

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
