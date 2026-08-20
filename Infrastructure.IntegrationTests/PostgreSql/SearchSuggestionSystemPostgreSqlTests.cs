namespace Infrastructure.IntegrationTests.PostgreSql;

using System.Diagnostics;
using Application.Common;
using Infrastructure.Contexts;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TestSupport;
using Xunit.Abstractions;

[Collection(PostgreSqlCollection.CollectionName)]
public sealed class SearchSuggestionSystemPostgreSqlTests(PostgreSqlFixture fixture, ITestOutputHelper output)
{
    private const int Iterations = 25;

    [Fact]
    public async Task GenreSuggestions_WithTwoFilters_ShouldMeasureSingleUserLatencyOnTheConfiguredDataset()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_SEARCH_SUGGESTION_SYSTEM_TESTS"), "1",
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

        var criteria = BookSearchQueryParser.Parse("type:\"Novel\" status:\"On Hold\"");
        var warmup = new BookSearchSuggestionQueryService(seedContext);
        await warmup.GetSuggestionsAsync(
            ownerId,
            BookSearchSuggestionFields.Genre,
            null,
            criteria,
            10,
            CancellationToken.None);

        var durations = new List<double>(Iterations);
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            await using var context = fixture.CreateContext(ownerId);
            var service = new BookSearchSuggestionQueryService(context);
            var started = Stopwatch.GetTimestamp();
            var suggestions = await service.GetSuggestionsAsync(
                ownerId,
                BookSearchSuggestionFields.Genre,
                null,
                criteria,
                10,
                CancellationToken.None);
            durations.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            Assert.NotEmpty(suggestions);
        }

        var ordered = durations.Order().ToArray();
        output.WriteLine($"Dataset: {csvPath}; books: {dataset.BookCount}; scope: type:Novel status:On Hold");
        output.WriteLine($"Genre suggestion single-user latency ({Iterations} warm-cache operations): " +
                         $"p50={Percentile(ordered, 0.50):F2} ms; p95={Percentile(ordered, 0.95):F2} ms; " +
                         $"p99={Percentile(ordered, 0.99):F2} ms");
    }

    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        return sorted[(int)Math.Ceiling(percentile * sorted.Count) - 1];
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
}
