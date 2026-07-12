namespace Domain.Models;

public sealed record BookSummarySnapshot(
    int TotalBooks,
    int RatedBooks,
    double? AverageRating,
    decimal CurrentChapters,
    int BooksWithKnownCurrentChapter,
    IReadOnlyList<BookStatusCountSnapshot> StatusCounts,
    IReadOnlyList<BookTypeSummarySnapshot> TypeCounts,
    IReadOnlyList<BookGenreCountSnapshot> GenreCounts,
    IReadOnlyList<BookRatingCountSnapshot> RatingCounts);

public sealed record BookStatusCountSnapshot(string Status, int Count);

public sealed record BookTypeSummarySnapshot(string Type, int BookCount, decimal CurrentChapters);

public sealed record BookGenreCountSnapshot(string Genre, int BookCount);

public sealed record BookRatingCountSnapshot(int Rating, int BookCount);
