namespace Application.Common.DTOs.Book;

public sealed record BookSummaryDto
{
    public int TotalBooks { get; init; }
    public int RatedBooks { get; init; }
    public int UnratedBooks { get; init; }
    public double? AverageRating { get; init; }
    public decimal CurrentChapters { get; init; }
    public int BooksWithKnownCurrentChapter { get; init; }
    public int BooksWithoutKnownCurrentChapter { get; init; }
    public IReadOnlyList<BookSummaryStatusCountDto> StatusCounts { get; init; } = [];
    public IReadOnlyList<BookSummaryTypeCountDto> TypeCounts { get; init; } = [];
    public IReadOnlyList<BookSummaryGenreCountDto> GenreCounts { get; init; } = [];
    public IReadOnlyList<BookSummaryRatingCountDto> RatingCounts { get; init; } = [];
}

public sealed record BookSummaryStatusCountDto
{
    public required string Status { get; init; }
    public required int Count { get; init; }
}

public sealed record BookSummaryTypeCountDto
{
    public required string Type { get; init; }
    public required int BookCount { get; init; }
    public required decimal CurrentChapters { get; init; }
}

public sealed record BookSummaryGenreCountDto
{
    public required string Genre { get; init; }
    public required int BookCount { get; init; }
}

public sealed record BookSummaryRatingCountDto
{
    public required int Rating { get; init; }
    public required int BookCount { get; init; }
}
