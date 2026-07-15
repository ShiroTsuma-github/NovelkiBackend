import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '@/api/client'
import type { BookAnalyticsDto } from '@/api/types'
import { inputClass, secondaryButtonClass } from '@/components/app/FormField'
import { formatAverageRating, formatChapterCount } from '@/features/books/BooksPage'
import { AnalyticsChartCard } from './AnalyticsChartCard'
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
        <MetricCard label="Total books" loading={isInitialLoading} value={data ? String(data.overview.totalBooks) : '-'} />
        <MetricCard label="Rated" loading={isInitialLoading} value={data ? String(data.overview.ratedBooks) : '-'} />
        <MetricCard label="Unrated" loading={isInitialLoading} value={data ? String(data.overview.unratedBooks) : '-'} />
        <MetricCard label="Average rating" loading={isInitialLoading} value={data ? formatAverageRating(data.overview.averageRating) : '-'} />
        <MetricCard label="Current chapters" loading={isInitialLoading} value={data ? formatChapterCount(data.overview.currentChapters) : '-'} />
      </div>

      <div className="grid min-w-0 gap-5 xl:grid-cols-2">
        <AnalyticsChartCard
          columns={['Type', 'Status', 'Books']}
          description="Compact matrix of statuses inside each content type."
          emptyMessage="No status/type data for this analytics scope."
          isEmpty={!isInitialLoading && !statusRows(data).length}
          isError={analyticsQuery.isError && !!data}
          isLoading={isInitialLoading}
          rows={statusRows(data)}
          title="Status by type"
          onRetry={() => analyticsQuery.refetch()}
        >
          <StatusByTypePreview data={data} />
        </AnalyticsChartCard>
        <AnalyticsChartCard
          columns={['Date', 'Events', 'Books touched', 'Chapters advanced']}
          description="Progress activity grouped by the selected bucket."
          emptyMessage="No reading activity in the selected date range."
          isEmpty={!isInitialLoading && !activityRows(data).length}
          isError={analyticsQuery.isError && !!data}
          isLoading={isInitialLoading}
          rows={activityRows(data)}
          title="Reading activity"
          onRetry={() => analyticsQuery.refetch()}
        >
          <ActivityPreview data={data} />
        </AnalyticsChartCard>
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

function StatusByTypePreview({ data }: { data?: BookAnalyticsDto }) {
  const items = data?.composition.statusByType ?? []
  return (
    <div className="grid gap-3">
      {items.slice(0, 8).map((item) => (
        <div className="grid gap-2" key={item.type}>
          <div className="flex items-center justify-between gap-3 text-sm">
            <span className="font-semibold text-slate-950">{item.type}</span>
            <span className="text-slate-500">{item.totalBooks} books</span>
          </div>
          <div className="flex min-w-0 flex-wrap gap-2">
            {item.statuses.map((status) => (
              <span className="rounded-full bg-white px-2.5 py-1 text-xs font-semibold text-slate-700 shadow-sm" key={`${item.type}-${status.status}`}>
                {status.status}: {status.bookCount}
              </span>
            ))}
          </div>
        </div>
      ))}
    </div>
  )
}

function ActivityPreview({ data }: { data?: BookAnalyticsDto }) {
  const points = data?.activity.points ?? []
  return (
    <div className="grid gap-2">
      {points.slice(-8).map((point) => (
        <div className="grid gap-1 rounded-md bg-white px-3 py-2 text-sm shadow-sm" key={point.date}>
          <div className="font-semibold text-slate-950">{point.date}</div>
          <div className="text-slate-600">
            {point.progressEvents} events, {point.booksTouched} books, {formatChapterCount(point.chaptersAdvanced)} chapters
          </div>
        </div>
      ))}
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

function statusRows(data?: BookAnalyticsDto) {
  return (data?.composition.statusByType ?? []).flatMap((item) =>
    item.statuses.map((status) => [item.type, status.status, status.bookCount]),
  )
}

function activityRows(data?: BookAnalyticsDto) {
  return (data?.activity.points ?? []).map((point) => [
    point.date,
    point.progressEvents,
    point.booksTouched,
    formatChapterCount(point.chaptersAdvanced),
  ])
}
