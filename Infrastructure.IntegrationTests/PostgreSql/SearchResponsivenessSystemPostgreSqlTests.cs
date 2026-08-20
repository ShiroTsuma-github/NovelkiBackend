namespace Infrastructure.IntegrationTests.PostgreSql;

using System.Diagnostics;
using Application.Common;
using Infrastructure.Contexts;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TestSupport;
using Xunit.Abstractions;

[Collection(PostgreSqlCollection.CollectionName)]
public sealed class SearchResponsivenessSystemPostgreSqlTests(PostgreSqlFixture fixture, ITestOutputHelper output)
{
    private const int Iterations = 25;

    [Fact]
    public async Task GeneralAndTitleSearch_ShouldMeasureWarmCacheSingleUserLatencyOnTheConfiguredDataset()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_SEARCH_RESPONSIVENESS_SYSTEM_TESTS"), "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var csvPath = Environment.GetEnvironmentVariable("SEARCH_BENCHMARK_CSV");
        if (string.IsNullOrWhiteSpace(csvPath))
        {
            throw new InvalidOperationException(
                "SEARCH_BENCHMARK_CSV must point to the local CSV dataset for this system benchmark.");
        }

        await fixture.ResetDatabaseAsync();
        var ownerId = Guid.NewGuid();
        await using var seedContext = fixture.CreateContext(ownerId);
        await AddUserAsync(seedContext, ownerId);
        var dataset = await BookCsvDatasetSeeder.SeedAsync(
            seedContext,
            ownerId,
            csvPath: csvPath,
            profile: BookCsvSeedProfile.PreserveSource);
        await RefreshSearchIndexAsync(seedContext, ownerId);

        var term = Environment.GetEnvironmentVariable("SEARCH_RESPONSIVENESS_QUERY") ?? "empress";
        var general = await MeasureAsync(ownerId, term);
        var title = await MeasureAsync(ownerId, $"title:\"{term}\"");

        output.WriteLine($"Dataset: {csvPath}; books: {dataset.BookCount}; search term: {term}");
        output.WriteLine($"General search ({Iterations} warm-cache single-user operations; returned {general.Results.Count}): {Format(general.Durations)}");
        output.WriteLine($"Title field search ({Iterations} warm-cache single-user operations; returned {title.Results.Count}): {Format(title.Durations)}");

        Assert.NotEmpty(general.Results);
        Assert.NotEmpty(title.Results);
    }

    private async Task<SearchMeasurement> MeasureAsync(Guid ownerId, string query)
    {
        var criteria = BookSearchQueryParser.Parse(query);
        await using (var warmupContext = fixture.CreateContext(ownerId))
        {
            var warmup = CreateReadQueryService(warmupContext);
            await warmup.GetBooksAsync(ownerId, criteria, 0, 20, null, null, CancellationToken.None);
        }

        var durations = new List<double>(Iterations);
        IReadOnlyCollection<Application.Common.DTOs.Book.BookListItemDto> results = [];
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            await using var context = fixture.CreateContext(ownerId);
            var service = CreateReadQueryService(context);
            var started = Stopwatch.GetTimestamp();
            results = await service.GetBooksAsync(ownerId, criteria, 0, 20, null, null, CancellationToken.None);
            durations.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }

        return new SearchMeasurement(durations.Order().ToArray(), results);
    }

    private static BookReadQueryService CreateReadQueryService(ApplicationDbContext context)
    {
        var criteriaApplier = new BookSearchCriteriaApplier(context);
        var sortBuilder = new BookSortBuilder(context);
        return new BookReadQueryService(context, criteriaApplier, sortBuilder,
            new BookListProjectionQuery(context, sortBuilder));
    }

    private static async Task RefreshSearchIndexAsync(ApplicationDbContext context, Guid ownerId)
    {
        var bookIds = await context.Books
            .Where(book => book.OwnerId == ownerId)
            .Select(book => book.Id)
            .ToArrayAsync();
        foreach (var bookId in bookIds)
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT refresh_book_search_index({bookId})");
        }
    }

    private static async Task AddUserAsync(ApplicationDbContext context, Guid ownerId)
    {
        context.Users.Add(new Infrastructure.Identity.User
        {
            Id = ownerId,
            UserName = $"reader-{ownerId:N}",
            NormalizedUserName = $"READER-{ownerId:N}",
            Email = $"{ownerId:N}@example.com",
            NormalizedEmail = $"{ownerId:N}@EXAMPLE.COM"
        });
        await context.SaveChangesAsync();
    }

    private static string Format(IReadOnlyList<double> sorted) =>
        $"p50={Percentile(sorted, 0.50):F2} ms; p95={Percentile(sorted, 0.95):F2} ms; p99={Percentile(sorted, 0.99):F2} ms";

    private static double Percentile(IReadOnlyList<double> sorted, double percentile) =>
        sorted[(int)Math.Ceiling(percentile * sorted.Count) - 1];

    private sealed record SearchMeasurement(
        IReadOnlyList<double> Durations,
        IReadOnlyCollection<Application.Common.DTOs.Book.BookListItemDto> Results);
}
