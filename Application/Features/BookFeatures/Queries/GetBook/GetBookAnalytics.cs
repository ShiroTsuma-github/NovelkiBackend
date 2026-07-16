namespace Application.Features.BookFeatures.Queries.GetBook;

using Application.Common.DTOs.Book;
using Domain.Models;
using FluentValidation.Results;

public sealed record GetBookAnalyticsQuery(
    string? Query = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Bucket = null) : IRequest<BookAnalyticsDto>;

public sealed class GetBookAnalyticsHandler : IRequestHandler<GetBookAnalyticsQuery, BookAnalyticsDto>
{
    private const int DefaultRangeDays = 84;
    private const int MaxDailyBucketRangeDays = 366;
    private const int MaxWeeklyBucketRangeDays = 3660;

    private static readonly HashSet<string> SupportedBuckets = new(StringComparer.OrdinalIgnoreCase)
    {
        "day", "week", "month"
    };

    private readonly IBookAnalyticsQueryService _queryService;
    private readonly IUser _user;

    public GetBookAnalyticsHandler(IBookAnalyticsQueryService queryService, IUser user)
    {
        _queryService = queryService;
        _user = user;
    }

    public async Task<BookAnalyticsDto> Handle(GetBookAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var bucket = string.IsNullOrWhiteSpace(request.Bucket) ? "week" : request.Bucket.Trim().ToLowerInvariant();
        if (!SupportedBuckets.Contains(bucket))
        {
            throw ValidationError(nameof(request.Bucket), "Bucket must be one of: day, week, month.");
        }

        var todayExclusive = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var to = request.To ?? todayExclusive;
        var from = request.From ?? to.AddDays(-DefaultRangeDays);
        if (_user.CreatedAt is { } userCreatedAt)
        {
            var accountStart = DateOnly.FromDateTime(userCreatedAt.UtcDateTime);
            if (from < accountStart)
            {
                from = accountStart;
            }
        }

        if (from >= to)
        {
            throw ValidationError(nameof(request.From), "From must be earlier than to.");
        }

        bucket = ResolveEffectiveBucket(bucket, from, to);
        var scope = new BookAnalyticsScopeSnapshot(request.Query, from, to, bucket);
        var criteria = BookSearchQueryParser.Parse(request.Query);
        var snapshot =
            await _queryService.GetAnalyticsAsync(_user.RequiredId, criteria, scope, cancellationToken);

        return snapshot.ToDto();
    }

    private static ValidationException ValidationError(string propertyName, string message)
    {
        return new ValidationException([new ValidationFailure(propertyName, message)]);
    }

    private static string ResolveEffectiveBucket(string requestedBucket, DateOnly from, DateOnly to)
    {
        var rangeDays = to.DayNumber - from.DayNumber;
        if (rangeDays > MaxWeeklyBucketRangeDays)
        {
            return "month";
        }

        if (requestedBucket == "day" && rangeDays > MaxDailyBucketRangeDays)
        {
            return "week";
        }

        return requestedBucket;
    }
}
