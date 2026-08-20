namespace Infrastructure.IntegrationTests.PostgreSql;

using System.Collections.Concurrent;
using System.Diagnostics;
using Application.Common;
using Domain.Entities;
using Infrastructure.Contexts;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TestSupport;
using Xunit.Abstractions;

[Collection(PostgreSqlCollection.CollectionName)]
public sealed class SearchSystemBenchmarkPostgreSqlTests(PostgreSqlFixture fixture, ITestOutputHelper output)
{
    private const int TotalOperations = 10_000;
    private const int DefaultParallelism = 8;

    [Fact]
    public async Task BackendSearchWorkload_ShouldMeasureTenThousandRealPostgreSqlOperations()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_SEARCH_SYSTEM_TESTS"), "1",
                StringComparison.Ordinal))
        {
            return;
        }

        await fixture.ResetDatabaseAsync();
        var ownerId = Guid.NewGuid();
        await using var context = fixture.CreateContext(ownerId);
        await AddUserAsync(context, ownerId);
        var csvPath = Environment.GetEnvironmentVariable("SEARCH_BENCHMARK_CSV");
        var dataset = await BookCsvDatasetSeeder.SeedAsync(context, ownerId, csvPath: csvPath);

        var wildcardTarget = TestData.Book(ownerId, "Devil Sword King");
        context.Books.Add(wildcardTarget);
        await context.SaveChangesAsync();
        await RefreshSearchIndexAsync(context, ownerId);

        var workload = CreateWorkload(dataset.Samples);
        var parallelism = GetParallelism();
        var durations = new ConcurrentBag<double>();
        var resultCount = 0;
        var elapsed = Stopwatch.StartNew();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, workload.Count),
            new ParallelOptions { MaxDegreeOfParallelism = parallelism },
            async (operation, cancellationToken) =>
        {
            var item = workload[operation];
            var started = Stopwatch.GetTimestamp();
            try
            {
                await using var requestContext = fixture.CreateContext(ownerId);
                var service = CreateReadQueryService(requestContext);
                var result = await service.GetBooksAsync(
                    ownerId,
                    BookSearchQueryParser.Parse(item.Query),
                    0,
                    20,
                    null,
                    null,
                    cancellationToken);
                durations.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                Interlocked.Add(ref resultCount, result.Count);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Search workload failed at operation {operation + 1}/{TotalOperations} ({item.Kind}): {item.Query}",
                    exception);
            }
        });
        elapsed.Stop();

        var ordered = durations.OrderBy(duration => duration).ToArray();
        var plans = await GetRepresentativePlansAsync(context, ownerId);
        output.WriteLine($"Search workload: {TotalOperations} backend operations across {dataset.BookCount} books, results: {resultCount}");
        output.WriteLine($"Parallelism: {parallelism}; wall-clock: {elapsed.Elapsed.TotalSeconds:F2} s; throughput: {TotalOperations / elapsed.Elapsed.TotalSeconds:F2} requests/s");
        output.WriteLine($"Query mix: {string.Join(", ", workload.GroupBy(item => item.Kind).OrderBy(group => group.Key).Select(group => $"{group.Key}={group.Count()}"))}");
        output.WriteLine($"p50: {Percentile(ordered, 0.50):F2} ms; p95: {Percentile(ordered, 0.95):F2} ms; p99: {Percentile(ordered, 0.99):F2} ms; aggregate: {durations.Sum():F2} ms");
        foreach (var (name, plan) in plans)
        {
            output.WriteLine($"{name} EXPLAIN (ANALYZE, BUFFERS):{Environment.NewLine}{plan}");
        }

        Assert.Equal(TotalOperations, durations.Count);
        Assert.NotEmpty(plans);
        Assert.All(plans.Values, plan => Assert.Contains("Execution Time", plan, StringComparison.Ordinal));
    }

    private static int GetParallelism()
    {
        return int.TryParse(Environment.GetEnvironmentVariable("SEARCH_SYSTEM_PARALLELISM"), out var value)
            ? Math.Clamp(value, 1, 64)
            : DefaultParallelism;
    }

    private static BookReadQueryService CreateReadQueryService(ApplicationDbContext context)
    {
        var criteriaApplier = new BookSearchCriteriaApplier(context);
        var sortBuilder = new BookSortBuilder(context);
        return new BookReadQueryService(context, criteriaApplier, sortBuilder,
            new BookListProjectionQuery(context, sortBuilder));
    }

    private static IReadOnlyList<SearchWorkloadItem> CreateWorkload(
        IReadOnlyList<BookCsvDatasetSample> samples)
    {
        Assert.NotEmpty(samples);

        const int generalCount = TotalOperations / 4; // Same 25% default as tools/generate.py (5 of 20).
        const int wildcardCount = 100;
        var generatorOperationCount = TotalOperations - wildcardCount;
        var random = new Random(20260820);
        var usesGeneral = Enumerable.Repeat(true, generalCount - wildcardCount)
            .Concat(Enumerable.Repeat(false, generatorOperationCount - (generalCount - wildcardCount)))
            .OrderBy(_ => random.Next())
            .ToArray();
        var workload = new List<SearchWorkloadItem>(TotalOperations);

        for (var index = 0; index < generatorOperationCount; index++)
        {
            workload.Add(CreateGeneratorCompatibleQuery(samples, usesGeneral[index], random));
        }

        workload.AddRange(Enumerable.Repeat(new SearchWorkloadItem("general-wildcard", "devi*king"), wildcardCount));

        return workload.OrderBy(_ => random.Next()).ToArray();
    }

    private static SearchWorkloadItem CreateGeneratorCompatibleQuery(
        IReadOnlyList<BookCsvDatasetSample> samples,
        bool hasGeneral,
        Random random)
    {
        var book = samples[random.Next(samples.Count)];
        var fields = new List<(string Name, string Value)>
        {
            ("title", book.PrimaryTitle),
            ("type", book.ContentType),
            ("status", book.Status)
        };
        if (!string.IsNullOrWhiteSpace(book.Author))
        {
            fields.Add(("author", book.Author));
        }

        if (book.Genres.Count > 0)
        {
            fields.Add(("genre", book.Genres[random.Next(book.Genres.Count)]));
        }

        if (book.Tags.Count > 0)
        {
            fields.Add(("tag", book.Tags[random.Next(book.Tags.Count)]));
        }

        Shuffle(fields, random);
        var attributeCount = RollAttributeCount(random);
        var components = new List<string>();
        if (hasGeneral && fields.Count > 0)
        {
            var value = RandomizeValue(fields[^1].Value, random);
            fields.RemoveAt(fields.Count - 1);
            if (value.Length > 0)
            {
                components.Add(value);
                attributeCount--;
            }
        }

        foreach (var (name, value) in fields.Take(Math.Min(attributeCount, fields.Count)))
        {
            var randomized = RandomizeValue(value, random);
            if (randomized.Length > 0)
            {
                components.Add($"{name}:{randomized}");
            }
        }

        if (random.NextDouble() < 0.10)
        {
            var other = samples[random.Next(samples.Count)];
            var tag = other.Tags.Count == 0 ? null : other.Tags[random.Next(other.Tags.Count)];
            if (!string.IsNullOrWhiteSpace(tag) && !book.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                components.Add($"-tag:{RandomizeValue(tag, random)}");
            }
        }

        Shuffle(components, random);
        return new SearchWorkloadItem(hasGeneral ? "generator-general" : "generator-split", string.Join(' ', components));
    }

    private static int RollAttributeCount(Random random)
    {
        var roll = random.Next(100);
        return roll < 50 ? 1 : roll < 80 ? 2 : 3;
    }

    private static string RandomizeValue(string value, Random random)
    {
        if (value.Length > 6 && random.NextDouble() < 0.20)
        {
            var start = random.Next(value.Length - 4);
            var end = random.Next(start + 4, value.Length + 1);
            value = value[start..end];
        }

        var caseRoll = random.NextDouble();
        if (caseRoll < 0.15)
        {
            value = value.ToLowerInvariant();
        }
        else if (caseRoll < 0.30)
        {
            value = value.ToUpperInvariant();
        }

        return value.Contains(' ') || random.NextDouble() < 0.20
            ? $"\"{value}\""
            : value;
    }

    private static void Shuffle<T>(IList<T> values, Random random)
    {
        for (var index = values.Count - 1; index > 0; index--)
        {
            var other = random.Next(index + 1);
            (values[index], values[other]) = (values[other], values[index]);
        }
    }

    private static async Task<Dictionary<string, string>> GetRepresentativePlansAsync(
        ApplicationDbContext context,
        Guid ownerId)
    {
        return new Dictionary<string, string>
        {
            ["fts-fuzzy"] = await ExplainAsync(context, ownerId, """
                "SearchVector" @@ plainto_tsquery('simple', 'devil')
                OR ('devil' <% "SearchDocument" AND public.book_search_has_close_lexeme("SearchVector", 'devil'))
                """),
            ["wildcard"] = await ExplainAsync(context, ownerId, """
                "SearchDocument" ILIKE '%devi%king%' ESCAPE '\'
                """),
            ["structured"] = await ExplainAsync(context, ownerId, """
                "ContentTypeId" = '10000000-0000-0000-0000-000000000001'
                AND "StatusId" = '20000000-0000-0000-0000-000000000001'
                """)
        };
    }

    private static async Task<string> ExplainAsync(ApplicationDbContext context, Guid ownerId, string predicate)
    {
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                EXPLAIN (ANALYZE, BUFFERS)
                SELECT "Id"
                FROM "Books"
                WHERE "OwnerId" = @ownerId
                  AND ({predicate});
                """;
            var ownerParameter = command.CreateParameter();
            ownerParameter.ParameterName = "ownerId";
            ownerParameter.Value = ownerId;
            command.Parameters.Add(ownerParameter);

            var lines = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lines.Add(reader.GetString(0));
            }

            return string.Join(Environment.NewLine, lines);
        }
        finally
        {
            await connection.CloseAsync();
        }
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

    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        return sorted[(int)Math.Ceiling(percentile * sorted.Count) - 1];
    }

    private sealed record SearchWorkloadItem(string Kind, string Query);
}
