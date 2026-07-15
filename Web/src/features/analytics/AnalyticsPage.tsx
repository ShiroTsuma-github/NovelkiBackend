import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '@/api/client'
import type { BookAnalyticsDto } from '@/api/types'
import { inputClass, secondaryButtonClass } from '@/components/app/FormField'
import { formatAverageRating, formatChapterCount } from '@/features/books/BooksPage'
import { AnalyticsChartCard } from './AnalyticsChartCard'
import { ChapterVolumeChart, chapterVolumeRows } from './charts/ChapterVolumeChart'
import { formatCount } from './charts/chartUtils'
import { CoverAvailabilityChart, coverAvailabilityRows } from './charts/CoverAvailabilityChart'
import { EstimatedReadingTimeChart, estimatedReadingTimeRows } from './charts/EstimatedReadingTimeChart'
import { LibraryGrowthChart, libraryGrowthRows } from './charts/LibraryGrowthChart'
import { LinkSourcesChart, linkSourceRows } from './charts/LinkSourcesChart'
import { MetadataCompletenessChart, metadataCompletenessRows } from './charts/MetadataCompletenessChart'
import { PriorityByStatusChart, priorityRows } from './charts/PriorityByStatusChart'
import { RatingDistributionChart, ratingRows } from './charts/RatingDistributionChart'
import { ReadingActivityChart, readingActivityRows } from './charts/ReadingActivityChart'
import { StatusByTypeChart, statusByTypeRows } from './charts/StatusByTypeChart'
import { relationRows, TopRelationsChart } from './charts/TopRelationsChart'
import { extractAnalyticsDateFilters } from './dateQueryFilters'

type AnalyticsBucket = 'day' | 'week' | 'month'

const analyticsBuckets: AnalyticsBucket[] = ['day', 'week', 'month']

export function AnalyticsPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const urlFilters = useMemo(() => getAnalyticsFilters(searchParams), [searchParams])
  const [draftFilters, setDraftFilters] = useState(urlFilters)

  useEffect(() => {
    setDraftFilters(urlFilters)
  }, [urlFilters])

  const analyticsQuery = useQuery({
    queryKey: ['book-analytics', urlFilters.query, urlFilters.from, urlFilters.to, urlFilters.bucket],
    queryFn: () => api.getBookAnalytics(toAnalyticsRequest(urlFilters)),
    placeholderData: keepPreviousData,
    staleTime: 30_000,
  })

  const data = analyticsQuery.data
  const isInitialLoading = analyticsQuery.isLoading && !data
  const emptyMessage = getAnalyticsEmptyMessage(data, urlFilters.query)

  function applyFilters() {
    setSearchParams(createAnalyticsSearchParams(draftFilters))
  }

  return (
    <div className="grid min-w-0 gap-5 overflow-x-hidden">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-slate-950">Analytics</h1>
          <p className="text-sm text-slate-500">Library metrics scoped by search query and date range.</p>
        </div>
        {analyticsQuery.isFetching && data ? (
          <span className="rounded-full border border-cyan-200 bg-cyan-50 px-3 py-1 text-xs font-semibold text-cyan-800">Refreshing...</span>
        ) : null}
      </div>

      <section className="grid gap-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
        <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_10rem_10rem_9rem_auto]">
          <label className="grid min-w-0 gap-1.5 text-sm font-semibold text-slate-700">
            Query
            <input
              className={`${inputClass} border-slate-300 bg-white text-slate-950 placeholder:text-slate-500 focus:border-cyan-500`}
              placeholder="author:Toika rating:>=8"
              value={draftFilters.query}
              onChange={(event) => setDraftFilters((current) => ({ ...current, query: event.target.value }))}
            />
          </label>
          <label className="grid gap-1.5 text-sm font-semibold text-slate-700">
            From
            <input
              className={`${inputClass} border-slate-300 bg-white text-slate-950 focus:border-cyan-500`}
              type="date"
              value={draftFilters.from}
              onChange={(event) => setDraftFilters((current) => ({ ...current, from: event.target.value }))}
            />
          </label>
          <label className="grid gap-1.5 text-sm font-semibold text-slate-700">
            To
            <input
              className={`${inputClass} border-slate-300 bg-white text-slate-950 focus:border-cyan-500`}
              type="date"
              value={draftFilters.to}
              onChange={(event) => setDraftFilters((current) => ({ ...current, to: event.target.value }))}
            />
          </label>
          <label className="grid gap-1.5 text-sm font-semibold text-slate-700">
            Bucket
            <select
              className={`${inputClass} border-slate-300 bg-white text-slate-950 focus:border-cyan-500`}
              value={draftFilters.bucket}
              onChange={(event) => setDraftFilters((current) => ({ ...current, bucket: normalizeBucket(event.target.value) }))}
            >
              {analyticsBuckets.map((bucket) => <option key={bucket} value={bucket}>{bucket}</option>)}
            </select>
          </label>
          <div className="flex items-end">
            <button className={`${secondaryButtonClass} w-full`} type="button" onClick={applyFilters}>Apply filters</button>
          </div>
        </div>
      </section>

      {analyticsQuery.isError && !data ? (
        <section className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-rose-900 shadow-sm">
          <h2 className="text-sm font-semibold">Could not load analytics.</h2>
          <p className="mt-1 text-sm">Check filters and retry the request.</p>
          <button className="mt-3 rounded-md bg-rose-700 px-3 py-2 text-sm font-semibold text-white hover:bg-rose-800" type="button" onClick={() => analyticsQuery.refetch()}>
            Retry
          </button>
        </section>
      ) : null}

      {emptyMessage ? (
        <section className="rounded-xl border border-slate-200 bg-white p-4 text-sm text-slate-600 shadow-sm">
          {emptyMessage}
        </section>
      ) : null}

      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
        <MetricCard label="Total books" loading={isInitialLoading} value={data ? formatCount(data.overview.totalBooks) : '-'} />
        <MetricCard label="Rated" loading={isInitialLoading} value={data ? formatCount(data.overview.ratedBooks) : '-'} />
        <MetricCard label="Unrated" loading={isInitialLoading} value={data ? formatCount(data.overview.unratedBooks) : '-'} />
        <MetricCard label="Average rating" loading={isInitialLoading} value={data ? formatAverageRating(data.overview.averageRating) : '-'} />
        <MetricCard label="Current chapters" loading={isInitialLoading} value={data ? formatChapterCount(data.overview.currentChapters) : '-'} />
      </div>

      <div className="grid min-w-0 items-start gap-5 xl:grid-cols-2">
        <div className="grid min-w-0 gap-5">
        <AnalyticsChartCard
          columns={['Type', 'Status', 'Books', 'Share of type']}
          description="100% stacked bars showing how statuses split inside each content type."
          emptyMessage="No status/type data for this analytics scope."
          isEmpty={!isInitialLoading && !statusByTypeRows(data).length}
          isError={analyticsQuery.isError && !!data}
          isLoading={isInitialLoading}
          rows={statusByTypeRows(data)}
          title="Status by type"
          onRetry={() => analyticsQuery.refetch()}
        >
          <StatusByTypeChart data={data} />
        </AnalyticsChartCard>
        <AnalyticsChartCard
          columns={['Genre', 'Books', 'Share of books']}
          description="Top genres by matching books, with smaller categories grouped into Other."
          emptyMessage="No genre data for this analytics scope."
          isEmpty={!isInitialLoading && !(data?.composition.genres.length)}
          isError={analyticsQuery.isError && !!data}
          isLoading={isInitialLoading}
          rows={relationRows(data?.composition.genres ?? [])}
          title="Top genres"
          onRetry={() => analyticsQuery.refetch()}
        >
          <TopRelationsChart field="genre" items={data?.composition.genres ?? []} title="Genres" />
        </AnalyticsChartCard>
        <AnalyticsChartCard
          columns={['Tag', 'Books', 'Share of books']}
          description="Top tags by matching books, with smaller categories grouped into Other."
          emptyMessage="No tag data for this analytics scope."
          isEmpty={!isInitialLoading && !(data?.composition.tags.length)}
          isError={analyticsQuery.isError && !!data}
          isLoading={isInitialLoading}
          rows={relationRows(data?.composition.tags ?? [])}
          title="Top tags"
          onRetry={() => analyticsQuery.refetch()}
        >
          <TopRelationsChart field="tag" items={data?.composition.tags ?? []} title="Tags" />
        </AnalyticsChartCard>
        <AnalyticsChartCard
          columns={['Rating', 'Books']}
          dataTableEnabled={false}
          description="Rated books are shown on a 1-10 axis; unrated books stay separate."
          emptyMessage="No rating data for this analytics scope."
          isEmpty={!isInitialLoading && !(data?.overview.totalBooks)}
          isError={analyticsQuery.isError && !!data}
          isLoading={isInitialLoading}
          rows={ratingRows(data)}
          title="Rating distribution"
          onRetry={() => analyticsQuery.refetch()}
        >
          <RatingDistributionChart data={data} />
        </AnalyticsChartCard>
        <AnalyticsChartCard
          columns={['Type', 'Books', 'Current chapters', 'Average current', 'Median current']}
          description="Compares title count and chapter volume with separate scales."
          emptyMessage="No chapter volume data for this analytics scope."
          isEmpty={!isInitialLoading && !(data?.progress.typeVolumes.length)}
          isError={analyticsQuery.isError && !!data}
          isLoading={isInitialLoading}
          rows={chapterVolumeRows(data)}
          title="Chapter volume by type"
          onRetry={() => analyticsQuery.refetch()}
        >
          <ChapterVolumeChart data={data} />
        </AnalyticsChartCard>
        <AnalyticsChartCard
          columns={['Status', 'Priority', 'Books', 'Share of status']}
          dataTableEnabled={false}
          description="Priority heatmap per status, including the Unset bucket."
          emptyMessage="No priority data for this analytics scope."
          isEmpty={!isInitialLoading && !(data?.planning.prioritiesByStatus.length)}
          isError={analyticsQuery.isError && !!data}
          isLoading={isInitialLoading}
          rows={priorityRows(data)}
          title="Priority by status"
          onRetry={() => analyticsQuery.refetch()}
        >
          <PriorityByStatusChart data={data} />
        </AnalyticsChartCard>
        <AnalyticsChartCard
          columns={['Type', 'Current chapters', 'Minutes / chapter', 'Estimated time']}
          description="Client-side estimate based on your minutes-per-chapter settings."
          emptyMessage="No chapter data to estimate reading time."
          isEmpty={!isInitialLoading && !(data?.progress.typeVolumes.length)}
          isError={analyticsQuery.isError && !!data}
          isLoading={isInitialLoading}
          rows={estimatedReadingTimeRows(data)}
          title="Estimated reading time"
          onRetry={() => analyticsQuery.refetch()}
        >
          <EstimatedReadingTimeChart data={data} />
        </AnalyticsChartCard>
        </div>
        <div className="grid min-w-0 gap-5">
        <AnalyticsChartCard
          columns={['Date', 'Progress events', 'Books touched', 'Chapters advanced']}
          description="Progress history grouped by the selected time bucket."
          emptyMessage="No reading activity in this time range."
          isEmpty={!isInitialLoading && !(data?.activity.points.length)}
          isError={analyticsQuery.isError && !!data}
          isLoading={isInitialLoading}
          rows={readingActivityRows(data)}
          title="Reading activity"
          onRetry={() => analyticsQuery.refetch()}
        >
          <ReadingActivityChart data={data} />
        </AnalyticsChartCard>
        <AnalyticsChartCard
          columns={['Date', 'Books added', 'Cumulative books', 'By type']}
          description="Library size over time, including empty buckets and visible import jumps."
          emptyMessage="No library growth points in this time range."
          isEmpty={!isInitialLoading && !(data?.libraryGrowth.points.length)}
          isError={analyticsQuery.isError && !!data}
          isLoading={isInitialLoading}
          rows={libraryGrowthRows(data)}
          title="Library growth"
          onRetry={() => analyticsQuery.refetch()}
        >
          <LibraryGrowthChart data={data} />
        </AnalyticsChartCard>
        <AnalyticsChartCard
          columns={['Field', 'Complete books', 'Coverage', 'Missing books']}
          description="Coverage per metadata field, intended to guide cleanup work."
          emptyMessage="0 metadata fields reported for this scope."
          isEmpty={!isInitialLoading && !(data?.quality.fieldCompleteness.length)}
          isError={analyticsQuery.isError && !!data}
          isLoading={isInitialLoading}
          rows={metadataCompletenessRows(data)}
          title="Metadata completeness"
          onRetry={() => analyticsQuery.refetch()}
        >
          <MetadataCompletenessChart data={data} />
        </AnalyticsChartCard>
        <AnalyticsChartCard
          columns={['Source', 'Links', 'Books', 'Coverage']}
          description="Ranks link sources by total links and book coverage."
          emptyMessage="0 link sources found for this scope."
          isEmpty={!isInitialLoading && !(data?.quality.linkSources.length)}
          isError={analyticsQuery.isError && !!data}
          isLoading={isInitialLoading}
          rows={linkSourceRows(data)}
          title="Link sources"
          onRetry={() => analyticsQuery.refetch()}
        >
          <LinkSourcesChart data={data} />
        </AnalyticsChartCard>
        <AnalyticsChartCard
          columns={['Kind', 'Bucket', 'Books', 'Coverage']}
          description="Separates cover statuses from provider coverage."
          emptyMessage="0 cover records reported for this scope."
          isEmpty={!isInitialLoading && !(data?.quality.coverStatuses.length || data?.quality.coverSources.length)}
          isError={analyticsQuery.isError && !!data}
          isLoading={isInitialLoading}
          rows={coverAvailabilityRows(data)}
          title="Cover availability"
          onRetry={() => analyticsQuery.refetch()}
        >
          <CoverAvailabilityChart data={data} />
        </AnalyticsChartCard>
        </div>
      </div>
    </div>
  )
}

function MetricCard({ label, loading, value }: { label: string; loading: boolean; value: string }) {
  return (
    <div className="rounded-lg border border-slate-200 bg-white px-4 py-3 shadow-sm">
      <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">{label}</div>
      <div className="mt-1 text-2xl font-semibold text-slate-950">{loading ? <span className="inline-block h-8 w-16 animate-pulse rounded bg-slate-200" /> : value}</div>
    </div>
  )
}

function getAnalyticsFilters(searchParams: URLSearchParams) {
  const extracted = extractAnalyticsDateFilters(searchParams.get('query') ?? '')
  return {
    query: extracted.query,
    from: searchParams.get('from') ?? extracted.from ?? defaultFromDate(),
    to: searchParams.get('to') ?? extracted.to ?? defaultToDate(),
    bucket: normalizeBucket(searchParams.get('bucket')),
  }
}

function toAnalyticsRequest(filters: ReturnType<typeof getAnalyticsFilters>) {
  return {
    query: filters.query.trim(),
    from: filters.from,
    to: filters.to,
    bucket: filters.bucket,
  }
}

function createAnalyticsSearchParams(filters: ReturnType<typeof getAnalyticsFilters>) {
  const next = new URLSearchParams()
  if (filters.query.trim()) {
    next.set('query', filters.query.trim())
  }
  next.set('from', filters.from)
  next.set('to', filters.to)
  next.set('bucket', filters.bucket)
  return next
}

function normalizeBucket(value?: string | null): AnalyticsBucket {
  return analyticsBuckets.includes(value as AnalyticsBucket) ? value as AnalyticsBucket : 'week'
}

function defaultToDate() {
  const date = new Date()
  date.setDate(date.getDate() + 1)
  return toDateInputValue(date)
}

function defaultFromDate() {
  const date = new Date()
  date.setDate(date.getDate() - 83)
  return toDateInputValue(date)
}

function toDateInputValue(date: Date) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function getAnalyticsEmptyMessage(data: BookAnalyticsDto | undefined, query: string) {
  if (!data || data.overview.totalBooks > 0) {
    return null
  }

  return query.trim()
    ? 'No books match the current analytics filters.'
    : 'Your library is empty. Add books to populate analytics.'
}
