namespace Application.Common.DTOs.Book;

public sealed record BookAnalyticsDto
{
    public DateTimeOffset GeneratedAt { get; init; }
    public required BookAnalyticsScopeDto Scope { get; init; }
    public required BookAnalyticsOverviewDto Overview { get; init; }
    public BookAnalyticsCompositionDto Composition { get; init; } = new();
}

public sealed record BookAnalyticsScopeDto
{
    public string? Query { get; init; }
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public required string Bucket { get; init; }
}

public sealed record BookAnalyticsOverviewDto
{
    public int TotalBooks { get; init; }
    public int RatedBooks { get; init; }
    public int UnratedBooks { get; init; }
    public double? AverageRating { get; init; }
    public decimal CurrentChapters { get; init; }
    public int BooksWithKnownCurrentChapter { get; init; }
    public int BooksWithoutKnownCurrentChapter { get; init; }
}

public sealed record BookAnalyticsCompositionDto
{
    public IReadOnlyList<BookAnalyticsStatusByTypeDto> StatusByType { get; init; } = [];
    public IReadOnlyList<BookAnalyticsRelationCountDto> Genres { get; init; } = [];
    public IReadOnlyList<BookAnalyticsRelationCountDto> Tags { get; init; } = [];
}

public sealed record BookAnalyticsStatusByTypeDto
{
    public required string Type { get; init; }
    public int TotalBooks { get; init; }
    public IReadOnlyList<BookAnalyticsStatusCountDto> Statuses { get; init; } = [];
}

public sealed record BookAnalyticsStatusCountDto
{
    public required string Status { get; init; }
    public int BookCount { get; init; }
}

public sealed record BookAnalyticsRelationCountDto
{
    public required string Name { get; init; }
    public int BookCount { get; init; }
    public double ShareOfBooks { get; init; }
}
