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


RESULTS:
1
"QUERY PLAN"
"Aggregate  (cost=104.25..104.26 rows=1 width=64) (actual time=0.288..0.289 rows=1 loops=1)"
"  Output: count(*), count(*) FILTER (WHERE (""Rating"" IS NOT NULL)), avg((""Rating"")::double precision) FILTER (WHERE (""Rating"" IS NOT NULL)), COALESCE(sum(""CurrentChapterNumber"") FILTER (WHERE (""CurrentChapterNumber"" IS NOT NULL)), '0'::numeric), count(*) FILTER (WHERE (""CurrentChapterNumber"" IS NOT NULL))"
"  Buffers: shared hit=82"
"  ->  Seq Scan on public.""Books"" b  (cost=0.00..92.11 rows=809 width=9) (actual time=0.010..0.204 rows=809 loops=1)"
"        Output: ""Rating"", ""CurrentChapterNumber"""
"        Filter: (b.""OwnerId"" = '019f31b5-b49e-78ed-828d-c6dc62a0808c'::uuid)"
"        Buffers: shared hit=82"
"Planning:"
"  Buffers: shared hit=201"
"Planning Time: 1.223 ms"
"Execution Time: 0.321 ms"

2
"QUERY PLAN"
"Sort  (cost=109.01..109.09 rows=30 width=444) (actual time=0.568..0.570 rows=10 loops=1)"
"  Output: ct.""Name"", s.""Name"", (count(*))"
"  Sort Key: ct.""Name"", (count(*)) DESC, s.""Name"""
"  Sort Method: quicksort  Memory: 25kB"
"  Buffers: shared hit=87"
"  ->  HashAggregate  (cost=107.98..108.28 rows=30 width=444) (actual time=0.533..0.535 rows=10 loops=1)"
"        Output: ct.""Name"", s.""Name"", count(*)"
"        Group Key: ct.""Name"", s.""Name"""
"        Batches: 1  Memory Usage: 24kB"
"        Buffers: shared hit=84"
"        ->  Hash Join  (cost=2.25..101.91 rows=809 width=436) (actual time=0.048..0.400 rows=809 loops=1)"
"              Output: ct.""Name"", s.""Name"""
"              Inner Unique: true"
"              Hash Cond: (b.""StatusId"" = s.""Id"")"
"              Buffers: shared hit=84"
"              ->  Hash Join  (cost=1.11..97.15 rows=809 width=234) (actual time=0.024..0.261 rows=809 loops=1)"
"                    Output: b.""StatusId"", ct.""Name"""
"                    Inner Unique: true"
"                    Hash Cond: (b.""ContentTypeId"" = ct.""Id"")"
"                    Buffers: shared hit=83"
"                    ->  Seq Scan on public.""Books"" b  (cost=0.00..92.11 rows=809 width=32) (actual time=0.003..0.122 rows=809 loops=1)"
"                          Output: b.""ContentTypeId"", b.""StatusId"""
"                          Filter: (b.""OwnerId"" = '019f31b5-b49e-78ed-828d-c6dc62a0808c'::uuid)"
"                          Buffers: shared hit=82"
"                    ->  Hash  (cost=1.05..1.05 rows=5 width=234) (actual time=0.009..0.010 rows=5 loops=1)"
"                          Output: ct.""Name"", ct.""Id"""
"                          Buckets: 1024  Batches: 1  Memory Usage: 9kB"
"                          Buffers: shared hit=1"
"                          ->  Seq Scan on public.""ContentTypes"" ct  (cost=0.00..1.05 rows=5 width=234) (actual time=0.003..0.004 rows=5 loops=1)"
"                                Output: ct.""Name"", ct.""Id"""
"                                Buffers: shared hit=1"
"              ->  Hash  (cost=1.06..1.06 rows=6 width=234) (actual time=0.015..0.016 rows=6 loops=1)"
"                    Output: s.""Name"", s.""Id"""
"                    Buckets: 1024  Batches: 1  Memory Usage: 9kB"
"                    Buffers: shared hit=1"
"                    ->  Seq Scan on public.""Statuses"" s  (cost=0.00..1.06 rows=6 width=234) (actual time=0.008..0.008 rows=6 loops=1)"
"                          Output: s.""Name"", s.""Id"""
"                          Buffers: shared hit=1"
"Planning:"
"  Buffers: shared hit=136"
"Planning Time: 0.994 ms"
"Execution Time: 0.651 ms"

3
"QUERY PLAN"
"Sort  (cost=36.00..36.02 rows=6 width=226) (actual time=0.105..0.107 rows=7 loops=1)"
"  Output: g.""Name"", (count(*))"
"  Sort Key: (count(*)) DESC, g.""Name"""
"  Sort Method: quicksort  Memory: 25kB"
"  Buffers: shared hit=11"
"  ->  GroupAggregate  (cost=35.82..35.93 rows=6 width=226) (actual time=0.087..0.091 rows=7 loops=1)"
"        Output: g.""Name"", count(*)"
"        Group Key: g.""Name"""
"        Buffers: shared hit=11"
"        ->  Sort  (cost=35.82..35.84 rows=6 width=218) (actual time=0.084..0.086 rows=12 loops=1)"
"              Output: g.""Name"""
"              Sort Key: g.""Name"""
"              Sort Method: quicksort  Memory: 25kB"
"              Buffers: shared hit=11"
"              ->  Subquery Scan on g  (cost=35.64..35.74 rows=6 width=218) (actual time=0.069..0.074 rows=12 loops=1)"
"                    Output: g.""Name"""
"                    Buffers: shared hit=11"
"                    ->  Unique  (cost=35.64..35.68 rows=6 width=234) (actual time=0.069..0.073 rows=12 loops=1)"
"                          Output: bg.""BookId"", g_1.""Name"""
"                          Buffers: shared hit=11"
"                          ->  Sort  (cost=35.64..35.65 rows=6 width=234) (actual time=0.069..0.070 rows=12 loops=1)"
"                                Output: bg.""BookId"", g_1.""Name"""
"                                Sort Key: bg.""BookId"", g_1.""Name"""
"                                Sort Method: quicksort  Memory: 25kB"
"                                Buffers: shared hit=11"
"                                ->  Nested Loop  (cost=1.42..35.56 rows=6 width=234) (actual time=0.045..0.058 rows=12 loops=1)"
"                                      Output: bg.""BookId"", g_1.""Name"""
"                                      Inner Unique: true"
"                                      Buffers: shared hit=11"
"                                      ->  Hash Join  (cost=1.14..13.29 rows=6 width=234) (actual time=0.027..0.030 rows=12 loops=1)"
"                                            Output: bg.""BookId"", g_1.""Name"""
"                                            Hash Cond: (g_1.""Id"" = bg.""GenreId"")"
"                                            Buffers: shared hit=2"
"                                            ->  Seq Scan on public.""Genres"" g_1  (cost=0.00..11.40 rows=140 width=234) (actual time=0.006..0.007 rows=8 loops=1)"
"                                                  Output: g_1.""Id"", g_1.""Name"", g_1.""NormalizedName"", g_1.""Description"", g_1.""Created"", g_1.""CreatedBy"", g_1.""LastModified"", g_1.""LastModifiedBy"""
"                                                  Buffers: shared hit=1"
"                                            ->  Hash  (cost=1.06..1.06 rows=6 width=32) (actual time=0.011..0.011 rows=12 loops=1)"
"                                                  Output: bg.""BookId"", bg.""GenreId"""
"                                                  Buckets: 1024  Batches: 1  Memory Usage: 9kB"
"                                                  Buffers: shared hit=1"
"                                                  ->  Seq Scan on public.""BookGenre"" bg  (cost=0.00..1.06 rows=6 width=32) (actual time=0.002..0.003 rows=12 loops=1)"
"                                                        Output: bg.""BookId"", bg.""GenreId"""
"                                                        Buffers: shared hit=1"
"                                      ->  Memoize  (cost=0.29..8.30 rows=1 width=16) (actual time=0.002..0.002 rows=1 loops=12)"
"                                            Output: b.""Id"""
"                                            Cache Key: bg.""BookId"""
"                                            Cache Mode: logical"
"                                            Hits: 9  Misses: 3  Evictions: 0  Overflows: 0  Memory Usage: 1kB"
"                                            Buffers: shared hit=9"
"                                            ->  Index Scan using ""PK_Books"" on public.""Books"" b  (cost=0.28..8.29 rows=1 width=16) (actual time=0.006..0.006 rows=1 loops=3)"
"                                                  Output: b.""Id"""
"                                                  Index Cond: (b.""Id"" = bg.""BookId"")"
"                                                  Filter: (b.""OwnerId"" = '019f31b5-b49e-78ed-828d-c6dc62a0808c'::uuid)"
"                                                  Buffers: shared hit=9"
"Planning Time: 0.266 ms"
"Execution Time: 0.202 ms"

4
"QUERY PLAN"
"Sort  (cost=119.03..119.21 rows=74 width=226) (actual time=0.303..0.304 rows=7 loops=1)"
"  Output: t.""Name"", (count(*))"
"  Sort Key: (count(*)) DESC, t.""Name"""
"  Sort Method: quicksort  Memory: 25kB"
"  Buffers: shared hit=99"
"  ->  HashAggregate  (cost=115.99..116.73 rows=74 width=226) (actual time=0.289..0.291 rows=7 loops=1)"
"        Output: t.""Name"", count(*)"
"        Group Key: t.""Name"""
"        Batches: 1  Memory Usage: 24kB"
"        Buffers: shared hit=99"
"        ->  HashAggregate  (cost=114.14..114.88 rows=74 width=234) (actual time=0.272..0.279 rows=74 loops=1)"
"              Output: bt.""BookId"", t.""Name"""
"              Group Key: bt.""BookId"", t.""Name"""
"              Batches: 1  Memory Usage: 24kB"
"              Buffers: shared hit=99"
"              ->  Nested Loop  (cost=4.82..113.77 rows=74 width=234) (actual time=0.068..0.252 rows=74 loops=1)"
"                    Output: bt.""BookId"", t.""Name"""
"                    Inner Unique: true"
"                    Buffers: shared hit=99"
"                    ->  Hash Join  (cost=4.67..105.61 rows=74 width=32) (actual time=0.051..0.209 rows=74 loops=1)"
"                          Output: bt.""BookId"", bt.""TagId"""
"                          Hash Cond: (b.""Id"" = bt.""BookId"")"
"                          Buffers: shared hit=85"
"                          ->  Seq Scan on public.""Books"" b  (cost=0.00..92.11 rows=809 width=16) (actual time=0.007..0.118 rows=809 loops=1)"
"                                Output: b.""Id"""
"                                Filter: (b.""OwnerId"" = '019f31b5-b49e-78ed-828d-c6dc62a0808c'::uuid)"
"                                Buffers: shared hit=82"
"                          ->  Hash  (cost=3.74..3.74 rows=74 width=32) (actual time=0.025..0.025 rows=74 loops=1)"
"                                Output: bt.""BookId"", bt.""TagId"""
"                                Buckets: 1024  Batches: 1  Memory Usage: 13kB"
"                                Buffers: shared hit=3"
"                                ->  Seq Scan on public.""BookTag"" bt  (cost=0.00..3.74 rows=74 width=32) (actual time=0.005..0.010 rows=74 loops=1)"
"                                      Output: bt.""BookId"", bt.""TagId"""
"                                      Buffers: shared hit=3"
"                    ->  Memoize  (cost=0.15..0.82 rows=1 width=234) (actual time=0.000..0.000 rows=1 loops=74)"
"                          Output: t.""Name"", t.""Id"""
"                          Cache Key: bt.""TagId"""
"                          Cache Mode: logical"
"                          Hits: 67  Misses: 7  Evictions: 0  Overflows: 0  Memory Usage: 1kB"
"                          Buffers: shared hit=14"
"                          ->  Index Scan using ""PK_Tags"" on public.""Tags"" t  (cost=0.14..0.81 rows=1 width=234) (actual time=0.003..0.003 rows=1 loops=7)"
"                                Output: t.""Name"", t.""Id"""
"                                Index Cond: (t.""Id"" = bt.""TagId"")"
"                                Buffers: shared hit=14"
"Planning:"
"  Buffers: shared hit=112"
"Planning Time: 1.033 ms"
"Execution Time: 0.380 ms"

5
"QUERY PLAN"
"GroupAggregate  (cost=0.28..6.36 rows=6 width=12) (actual time=0.023..0.034 rows=6 loops=1)"
"  Output: ""Rating"", count(*)"
"  Group Key: b.""Rating"""
"  Buffers: shared hit=3"
"  ->  Index Only Scan using ""IX_Books_OwnerId_Rating"" on public.""Books"" b  (cost=0.28..5.90 rows=81 width=4) (actual time=0.018..0.022 rows=81 loops=1)"
"        Output: ""OwnerId"", ""Rating"""
"        Index Cond: ((b.""OwnerId"" = '019f31b5-b49e-78ed-828d-c6dc62a0808c'::uuid) AND (b.""Rating"" IS NOT NULL))"
"        Heap Fetches: 0"
"        Buffers: shared hit=3"
"Planning Time: 0.085 ms"
"Execution Time: 0.052 ms"

6
"QUERY PLAN"
"HashAggregate  (cost=102.94..103.12 rows=18 width=230) (actual time=0.433..0.435 rows=10 loops=1)"
"  Output: s.""Name"", b.""Priority"", count(*)"
"  Group Key: s.""Name"", b.""Priority"""
"  Batches: 1  Memory Usage: 24kB"
"  Buffers: shared hit=83"
"  ->  Hash Join  (cost=1.14..96.87 rows=809 width=222) (actual time=0.055..0.318 rows=809 loops=1)"
"        Output: s.""Name"", b.""Priority"""
"        Inner Unique: true"
"        Hash Cond: (b.""StatusId"" = s.""Id"")"
"        Buffers: shared hit=83"
"        ->  Seq Scan on public.""Books"" b  (cost=0.00..92.11 rows=809 width=20) (actual time=0.007..0.143 rows=809 loops=1)"
"              Output: b.""Priority"", b.""StatusId"""
"              Filter: (b.""OwnerId"" = '019f31b5-b49e-78ed-828d-c6dc62a0808c'::uuid)"
"              Buffers: shared hit=82"
"        ->  Hash  (cost=1.06..1.06 rows=6 width=234) (actual time=0.038..0.038 rows=6 loops=1)"
"              Output: s.""Name"", s.""Id"""
"              Buckets: 1024  Batches: 1  Memory Usage: 9kB"
"              Buffers: shared hit=1"
"              ->  Seq Scan on public.""Statuses"" s  (cost=0.00..1.06 rows=6 width=234) (actual time=0.004..0.005 rows=6 loops=1)"
"                    Output: s.""Name"", s.""Id"""
"                    Buffers: shared hit=1"
"Planning:"
"  Buffers: shared hit=3"
"Planning Time: 0.196 ms"
"Execution Time: 0.483 ms"

7
"QUERY PLAN"
"Sort  (cost=136.23..138.25 rows=809 width=223) (actual time=0.425..0.450 rows=809 loops=1)"
"  Output: ct.""Name"", b.""CurrentChapterNumber"""
"  Sort Key: ct.""Name"""
"  Sort Method: quicksort  Memory: 50kB"
"  Buffers: shared hit=83"
"  ->  Hash Join  (cost=1.11..97.15 rows=809 width=223) (actual time=0.029..0.268 rows=809 loops=1)"
"        Output: ct.""Name"", b.""CurrentChapterNumber"""
"        Inner Unique: true"
"        Hash Cond: (b.""ContentTypeId"" = ct.""Id"")"
"        Buffers: shared hit=83"
"        ->  Seq Scan on public.""Books"" b  (cost=0.00..92.11 rows=809 width=21) (actual time=0.010..0.149 rows=809 loops=1)"
"              Output: b.""CurrentChapterNumber"", b.""ContentTypeId"""
"              Filter: (b.""OwnerId"" = '019f31b5-b49e-78ed-828d-c6dc62a0808c'::uuid)"
"              Buffers: shared hit=82"
"        ->  Hash  (cost=1.05..1.05 rows=5 width=234) (actual time=0.010..0.010 rows=5 loops=1)"
"              Output: ct.""Name"", ct.""Id"""
"              Buckets: 1024  Batches: 1  Memory Usage: 9kB"
"              Buffers: shared hit=1"
"              ->  Seq Scan on public.""ContentTypes"" ct  (cost=0.00..1.05 rows=5 width=234) (actual time=0.002..0.003 rows=5 loops=1)"
"                    Output: ct.""Name"", ct.""Id"""
"                    Buffers: shared hit=1"
"Planning Time: 0.159 ms"
"Execution Time: 0.520 ms"

8
"QUERY PLAN"
"Sort  (cost=218.25..220.30 rows=819 width=48) (actual time=0.658..0.686 rows=819 loops=1)"
"  Output: h.""BookId"", h.""Id"", h.""ChangedAt"", h.""ChapterNumber"", h.""ChapterLabel"""
"  Sort Key: h.""BookId"", h.""ChangedAt"", h.""Id"""
"  Sort Method: quicksort  Memory: 76kB"
"  Buffers: shared hit=149"
"  ->  Hash Join  (cost=102.22..178.62 rows=819 width=48) (actual time=0.238..0.449 rows=819 loops=1)"
"        Output: h.""BookId"", h.""Id"", h.""ChangedAt"", h.""ChapterNumber"", h.""ChapterLabel"""
"        Inner Unique: true"
"        Hash Cond: (h.""BookId"" = b.""Id"")"
"        Buffers: shared hit=146"
"        ->  Seq Scan on public.""BookProgressHistory"" h  (cost=0.00..74.24 rows=819 width=48) (actual time=0.006..0.112 rows=819 loops=1)"
"              Output: h.""Id"", h.""BookId"", h.""ChapterNumber"", h.""ChapterLabel"", h.""ChangedAt"", h.""Comment"", h.""Created"", h.""CreatedBy"", h.""LastModified"", h.""LastModifiedBy"""
"              Filter: (h.""ChangedAt"" < '2026-08-01 00:00:00+00'::timestamp with time zone)"
"              Buffers: shared hit=64"
"        ->  Hash  (cost=92.11..92.11 rows=809 width=16) (actual time=0.222..0.222 rows=809 loops=1)"
"              Output: b.""Id"""
"              Buckets: 1024  Batches: 1  Memory Usage: 46kB"
"              Buffers: shared hit=82"
"              ->  Seq Scan on public.""Books"" b  (cost=0.00..92.11 rows=809 width=16) (actual time=0.004..0.113 rows=809 loops=1)"
"                    Output: b.""Id"""
"                    Filter: (b.""OwnerId"" = '019f31b5-b49e-78ed-828d-c6dc62a0808c'::uuid)"
"                    Buffers: shared hit=82"
"Planning:"
"  Buffers: shared hit=68 read=4 dirtied=6"
"Planning Time: 0.733 ms"
"Execution Time: 0.858 ms"

9
"QUERY PLAN"
"Hash Join  (cost=1.11..99.17 rows=809 width=226) (actual time=0.047..0.298 rows=809 loops=1)"
"  Output: ct.""Name"", b.""Created"""
"  Inner Unique: true"
"  Hash Cond: (b.""ContentTypeId"" = ct.""Id"")"
"  Buffers: shared hit=83"
"  ->  Seq Scan on public.""Books"" b  (cost=0.00..94.14 rows=809 width=24) (actual time=0.010..0.165 rows=809 loops=1)"
"        Output: b.""Created"", b.""ContentTypeId"""
"        Filter: ((b.""Created"" < '2026-08-01 00:00:00+00'::timestamp with time zone) AND (b.""OwnerId"" = '019f31b5-b49e-78ed-828d-c6dc62a0808c'::uuid))"
"        Buffers: shared hit=82"
"  ->  Hash  (cost=1.05..1.05 rows=5 width=234) (actual time=0.011..0.012 rows=5 loops=1)"
"        Output: ct.""Name"", ct.""Id"""
"        Buckets: 1024  Batches: 1  Memory Usage: 9kB"
"        Buffers: shared hit=1"
"        ->  Seq Scan on public.""ContentTypes"" ct  (cost=0.00..1.05 rows=5 width=234) (actual time=0.003..0.003 rows=5 loops=1)"
"              Output: ct.""Name"", ct.""Id"""
"              Buffers: shared hit=1"
"Planning Time: 0.201 ms"
"Execution Time: 0.377 ms"

10
"QUERY PLAN"
"Hash Right Join  (cost=102.22..4520.12 rows=809 width=368) (actual time=0.326..0.728 rows=809 loops=1)"
"  Output: b.""Id"", (b.""AuthorId"" IS NOT NULL), b.""Description"", (ANY (b.""Id"" = (hashed SubPlan 2).col1)), (ANY (b.""Id"" = (hashed SubPlan 4).col1)), (b.""Rating"" IS NOT NULL), (b.""Priority"" IS NOT NULL), (b.""TotalChapters"" IS NOT NULL), (ANY (b.""Id"" = (hashed SubPlan 6).col1)), bc.""Status"", bc.""StoragePath"", bc.""ThumbnailStoragePath"""
"  Inner Unique: true"
"  Hash Cond: (bc.""BookId"" = b.""Id"")"
"  Buffers: shared hit=180"
"  ->  Seq Scan on public.""BookCovers"" bc  (cost=0.00..101.09 rows=809 width=170) (actual time=0.004..0.065 rows=809 loops=1)"
"        Output: bc.""Id"", bc.""BookId"", bc.""Status"", bc.""Source"", bc.""StoragePath"", bc.""OriginalImageUrl"", bc.""MimeType"", bc.""SizeBytes"", bc.""Width"", bc.""Height"", bc.""FailureReason"", bc.""LastAttemptAt"", bc.""Created"", bc.""CreatedBy"", bc.""LastModified"", bc.""LastModifiedBy"", bc.""ThumbnailHeight"", bc.""ThumbnailMimeType"", bc.""ThumbnailSizeBytes"", bc.""ThumbnailStoragePath"", bc.""ThumbnailWidth"""
"        Buffers: shared hit=93"
"  ->  Hash  (cost=92.11..92.11 rows=809 width=236) (actual time=0.273..0.274 rows=809 loops=1)"
"        Output: b.""Id"", b.""AuthorId"", b.""Description"", b.""Rating"", b.""Priority"", b.""TotalChapters"""
"        Buckets: 1024  Batches: 1  Memory Usage: 48kB"
"        Buffers: shared hit=82"
"        ->  Seq Scan on public.""Books"" b  (cost=0.00..92.11 rows=809 width=236) (actual time=0.006..0.144 rows=809 loops=1)"
"              Output: b.""Id"", b.""AuthorId"", b.""Description"", b.""Rating"", b.""Priority"", b.""TotalChapters"""
"              Filter: (b.""OwnerId"" = '019f31b5-b49e-78ed-828d-c6dc62a0808c'::uuid)"
"              Buffers: shared hit=82"
"  SubPlan 2"
"    ->  Seq Scan on public.""BookGenre"" bg  (cost=0.00..1.06 rows=6 width=16) (actual time=0.003..0.004 rows=12 loops=1)"
"          Output: bg.""BookId"""
"          Buffers: shared hit=1"
"  SubPlan 4"
"    ->  Seq Scan on public.""BookTag"" bt  (cost=0.00..3.74 rows=74 width=16) (actual time=0.002..0.007 rows=74 loops=1)"
"          Output: bt.""BookId"""
"          Buffers: shared hit=3"
"  SubPlan 6"
"    ->  Seq Scan on public.""BookLinks"" bl  (cost=0.00..1.04 rows=4 width=16) (actual time=0.004..0.004 rows=4 loops=1)"
"          Output: bl.""BookId"""
"          Buffers: shared hit=1"
"Planning:"
"  Buffers: shared hit=58 read=1 dirtied=1"
"Planning Time: 0.893 ms"
"Execution Time: 0.833 ms"


11
"QUERY PLAN"
"Nested Loop  (cost=30.65..76.84 rows=4 width=44) (actual time=0.042..0.049 rows=4 loops=1)"
"  Output: bt.""BookId"", bt.""Title"""
"  Inner Unique: true"
"  Buffers: shared hit=20"
"  ->  Bitmap Heap Scan on public.""BookTitles"" bt  (cost=30.37..43.64 rows=4 width=44) (actual time=0.030..0.033 rows=4 loops=1)"
"        Output: bt.""Id"", bt.""BookId"", bt.""Title"", bt.""NormalizedTitle"", bt.""Language"", bt.""IsPrimary"", bt.""Source"", bt.""Created"", bt.""CreatedBy"", bt.""LastModified"", bt.""LastModifiedBy"""
"        Recheck Cond: (NOT bt.""IsPrimary"")"
"        Heap Blocks: exact=3"
"        Buffers: shared hit=8"
"        ->  Bitmap Index Scan on ""IX_BookTitles_BookId_IsPrimary""  (cost=0.00..30.37 rows=4 width=0) (actual time=0.025..0.025 rows=4 loops=1)"
"              Index Cond: (bt.""IsPrimary"" = false)"
"              Buffers: shared hit=5"
"  ->  Index Scan using ""PK_Books"" on public.""Books"" b  (cost=0.28..8.29 rows=1 width=16) (actual time=0.003..0.003 rows=1 loops=4)"
"        Output: b.""Id"""
"        Index Cond: (b.""Id"" = bt.""BookId"")"
"        Filter: (b.""OwnerId"" = '019f31b5-b49e-78ed-828d-c6dc62a0808c'::uuid)"
"        Buffers: shared hit=12"
"Planning:"
"  Buffers: shared hit=12"
"Planning Time: 0.330 ms"
"Execution Time: 0.104 ms"


12
"QUERY PLAN"
"Nested Loop  (cost=0.28..34.24 rows=4 width=22) (actual time=0.021..0.027 rows=4 loops=1)"
"  Output: bl.""BookId"", bl.""SourceType"""
"  Inner Unique: true"
"  Buffers: shared hit=13"
"  ->  Seq Scan on public.""BookLinks"" bl  (cost=0.00..1.04 rows=4 width=22) (actual time=0.004..0.005 rows=4 loops=1)"
"        Output: bl.""Id"", bl.""BookId"", bl.""Url"", bl.""Label"", bl.""SourceType"", bl.""IsPrimary"", bl.""LastReadHere"", bl.""Created"", bl.""CreatedBy"", bl.""LastModified"", bl.""LastModifiedBy"""
"        Buffers: shared hit=1"
"  ->  Index Scan using ""PK_Books"" on public.""Books"" b  (cost=0.28..8.29 rows=1 width=16) (actual time=0.005..0.005 rows=1 loops=4)"
"        Output: b.""Id"""
"        Index Cond: (b.""Id"" = bl.""BookId"")"
"        Filter: (b.""OwnerId"" = '019f31b5-b49e-78ed-828d-c6dc62a0808c'::uuid)"
"        Buffers: shared hit=12"
"Planning:"
"  Buffers: shared hit=11 read=1"
"Planning Time: 0.508 ms"
"Execution Time: 0.058 ms"


13
"QUERY PLAN"
"Hash Join  (cost=102.22..205.45 rows=809 width=180) (actual time=0.265..0.556 rows=809 loops=1)"
"  Output: bc.""BookId"", bc.""Status"", bc.""Source"", bc.""StoragePath"", bc.""ThumbnailStoragePath"""
"  Inner Unique: true"
"  Hash Cond: (bc.""BookId"" = b.""Id"")"
"  Buffers: shared hit=175"
"  ->  Seq Scan on public.""BookCovers"" bc  (cost=0.00..101.09 rows=809 width=180) (actual time=0.005..0.080 rows=809 loops=1)"
"        Output: bc.""Id"", bc.""BookId"", bc.""Status"", bc.""Source"", bc.""StoragePath"", bc.""OriginalImageUrl"", bc.""MimeType"", bc.""SizeBytes"", bc.""Width"", bc.""Height"", bc.""FailureReason"", bc.""LastAttemptAt"", bc.""Created"", bc.""CreatedBy"", bc.""LastModified"", bc.""LastModifiedBy"", bc.""ThumbnailHeight"", bc.""ThumbnailMimeType"", bc.""ThumbnailSizeBytes"", bc.""ThumbnailStoragePath"", bc.""ThumbnailWidth"""
"        Buffers: shared hit=93"
"  ->  Hash  (cost=92.11..92.11 rows=809 width=16) (actual time=0.250..0.250 rows=809 loops=1)"
"        Output: b.""Id"""
"        Buckets: 1024  Batches: 1  Memory Usage: 46kB"
"        Buffers: shared hit=82"
"        ->  Seq Scan on public.""Books"" b  (cost=0.00..92.11 rows=809 width=16) (actual time=0.005..0.128 rows=809 loops=1)"
"              Output: b.""Id"""
"              Filter: (b.""OwnerId"" = '019f31b5-b49e-78ed-828d-c6dc62a0808c'::uuid)"
"              Buffers: shared hit=82"
"Planning:"
"  Buffers: shared hit=12"
"Planning Time: 0.386 ms"
"Execution Time: 0.622 ms"