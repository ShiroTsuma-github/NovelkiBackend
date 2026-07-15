# NB-066 Analytics performance report

Date: 2026-07-15

## Scope

Implemented performance hardening for the analytics endpoint after NB-060..NB-065.

Covered areas:

- expensive analytics aggregations in `BookAnalyticsQueryService`;
- index coverage for filtered owner-scoped analytics;
- query count and JSON payload size regression tests;
- optional large SQLite harness for 10,000 books and 250,000 progress history rows;
- attempted PostgreSQL migration/measurement using `.env`.

## Code-level findings and changes

### Expensive operations found

- `overview` used several separate `CountAsync`/`AverageAsync`/`SumAsync` round-trips over the same filtered book set.
- `quality.fieldCompleteness` used multiple separate `CountAsync` calls for fields that can be projected once.
- `activity.points` loaded all progress history for matching books, including rows after the requested `to` boundary.
- `libraryGrowth` loaded all matching books, including rows created after `to`.
- analytics relation queries rely heavily on `BookId` joins/subqueries for genres, tags, links, titles and covers.

### Optimizations implemented

- Collapsed overview into one aggregate query.
- Collapsed most quality completeness checks into one scalar projection.
- Limited activity history to `ChangedAt < to` on PostgreSQL/Npgsql.
- Limited library growth books to `Created < to` on PostgreSQL/Npgsql.
- Kept SQLite fallback filtering in memory only for `DateTimeOffset` comparisons, because SQLite provider cannot translate those comparisons.
- Preserved scalar projections only; no full book entity graph is loaded by analytics.

## Indexes added

Migration: `AddAnalyticsPerformanceIndexes`

- `Books(OwnerId, Created)` for library growth and date-windowed analytics.
- `Books(OwnerId, Priority)` for priority aggregation/filter support.
- `BookProgressHistory(BookId, ChangedAt, Id)` for ordered progress activity after baseline handling.
- `BookLinks(BookId, SourceType)` for link coverage/source aggregation.
- `BookTitles(BookId, IsPrimary)` for alternate-title completeness.
- `BookCovers(BookId, Status, Source)` for cover quality/source/status aggregation.

Rollback drops the new indexes and restores previous:

- `BookProgressHistory(BookId, ChangedAt)`
- `BookLinks(BookId)`

## Verification

Local targeted verification:

- `dotnet build --no-restore` passed.
- `dotnet test Infrastructure.IntegrationTests\Infrastructure.IntegrationTests.csproj --no-build --filter "BookAnalytics"` passed.

Regression test added:

- `BookAnalytics_ShouldKeepQueryCountAndPayloadBoundedForBroadRequest`
  - seeds 300 books with relations and progress history;
  - asserts analytics command count stays bounded;
  - asserts serialized analytics JSON remains under 100 KB;
  - checks broad categories and owner-scoped aggregates remain populated.

Large harness added:

- `BookAnalytics_LargePerformanceHarness_ShouldRunWhenExplicitlyEnabled`
  - run with `RUN_ANALYTICS_PERF_TESTS=1`;
  - seeds 10,000 books and 250,000 progress history rows;
  - asserts bounded command count and JSON below 100 KB;
  - not enabled by default to avoid making normal CI slow.

## PostgreSQL measurement status

Attempted to apply the migration to the PostgreSQL database from `.env` using `DB_CONNECTION_STRING`.

Result: connection timed out to the configured PostgreSQL host, so `EXPLAIN ANALYZE` could not be measured from this environment.

No PostgreSQL performance numbers are claimed in this report. The implemented migration and harness are ready to run once the database endpoint is reachable from the execution environment.

Recommended command once DB is reachable:

```powershell
$db = (Get-Content .env | Where-Object { $_ -like 'DB_CONNECTION_STRING=*' } | Select-Object -First 1) -replace '^DB_CONNECTION_STRING=', ''
$env:ConnectionStrings__DB = $db
dotnet ef database update --project Infrastructure --startup-project Api --no-build
$env:RUN_ANALYTICS_PERF_TESTS = '1'
dotnet test Infrastructure.IntegrationTests\Infrastructure.IntegrationTests.csproj --no-build --filter "BookAnalytics_LargePerformanceHarness"
```
