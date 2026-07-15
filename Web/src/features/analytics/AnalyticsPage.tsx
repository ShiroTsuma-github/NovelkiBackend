import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { addDays, format, isSameDay, parseISO, subMonths, subYears } from 'date-fns'
import { useEffect, useMemo, useRef, useState } from 'react'
import { DayPicker, type DateRange } from 'react-day-picker'
import { useSearchParams } from 'react-router-dom'
import { api } from '@/api/client'
import { getStoredSession } from '@/api/http'
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
const analyticsRangePresets = [
  { label: 'Beginning', getFrom: (_today: Date) => null },
  { label: 'Last 2 years', getFrom: (today: Date) => subYears(today, 2) },
  { label: 'Last 1 year', getFrom: (today: Date) => subYears(today, 1) },
  { label: 'Last 6 months', getFrom: (today: Date) => subMonths(today, 6) },
  { label: 'Last 3 months', getFrom: (today: Date) => subMonths(today, 3) },
  { label: 'Last month', getFrom: (today: Date) => subMonths(today, 1) },
] as const

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
          <p className="text-sm text-slate-500">Library metrics scoped by book search and created-date range.</p>
        </div>
        {analyticsQuery.isFetching && data ? (
          <span className="rounded-full border border-cyan-200 bg-cyan-50 px-3 py-1 text-xs font-semibold text-cyan-800">Refreshing...</span>
        ) : null}
      </div>

      <section className="grid gap-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
        <p className="text-sm text-slate-600">
          Query uses the same filters as Books search. From/To filters books by created date and affects every metric and chart.
        </p>
        <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_18rem_9rem_auto]">
          <label className="grid min-w-0 gap-1.5 text-sm font-semibold text-slate-700">
            Query
            <input
              className={`${inputClass} border-slate-300 bg-white text-slate-950 placeholder:text-slate-500 focus:border-cyan-500`}
              placeholder="author:Toika rating:>=8"
              value={draftFilters.query}
              onChange={(event) => setDraftFilters((current) => ({ ...current, query: event.target.value }))}
            />
          </label>
          <DateRangeChooser filters={draftFilters} onChange={setDraftFilters} />
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
        <div className="grid min-w-0 gap-5" data-testid="analytics-left-column">
        <AnalyticsChartCard
          columns={['Type', 'Status', 'Books', 'Share of type']}
          description="See which content types are concentrated in each reading status."
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
          description="Find the genres that dominate the current filters."
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
          description="Find the tags that dominate the current filters."
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
          columns={['Status', 'Priority', 'Books', 'Share of status']}
          dataTableEnabled={false}
          description="Spot statuses where books are still unprioritized or clustered in one priority."
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
          columns={['Source', 'Links', 'Books', 'Coverage']}
          description="Check which external sources are represented in matching books."
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
          description="Review cover fetch state and provider coverage without opening each book."
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
        <div className="grid min-w-0 gap-5" data-testid="analytics-right-column">
        <AnalyticsChartCard
          columns={['Rating', 'Books']}
          dataTableEnabled={false}
          description="See the rating spread and jump directly to unrated books."
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
          description="Compare where your chapter backlog sits by content type."
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
          columns={['Type', 'Current chapters', 'Minutes / chapter', 'Estimated time']}
          description="Estimate reading effort from chapter counts and your minutes-per-chapter settings."
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
        <AnalyticsChartCard
          columns={['Date', 'Progress events', 'Books touched', 'Chapters advanced']}
          description="Track recent progress events and touched books in the selected range."
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
          description="See when books were added and which types caused growth."
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
          description="Find fields that are missing most often in the current scope."
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
        </div>
      </div>
    </div>
  )
}

function DateRangeChooser({
  filters,
  onChange,
}: {
  filters: ReturnType<typeof getAnalyticsFilters>
  onChange: React.Dispatch<React.SetStateAction<ReturnType<typeof getAnalyticsFilters>>>
}) {
  const [isOpen, setIsOpen] = useState(false)
  const rootRef = useRef<HTMLDivElement>(null)
  const accountStartDate = getAccountStartDate()
  const fromDate = parseDateInput(filters.from)
  const toInclusiveDate = getInclusiveToDate(filters.to)
  const selectedRange: DateRange = { from: fromDate, to: toInclusiveDate }
  const [calendarRange, setCalendarRange] = useState<DateRange | undefined>(selectedRange)
  const [rangeSelectionAnchor, setRangeSelectionAnchor] = useState<Date | null>(null)
  const display = getDateRangeDisplay(filters, accountStartDate)

  useEffect(() => {
    setCalendarRange(selectedRange)
  }, [filters.from, filters.to])

  useEffect(() => {
    if (!isOpen) {
      return undefined
    }

    function closeOnOutsidePointerDown(event: PointerEvent) {
      const target = event.target
      if (target instanceof Node && !rootRef.current?.contains(target)) {
        setIsOpen(false)
        setRangeSelectionAnchor(null)
        setCalendarRange(selectedRange)
      }
    }

    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setIsOpen(false)
        setRangeSelectionAnchor(null)
        setCalendarRange(selectedRange)
      }
    }

    document.addEventListener('pointerdown', closeOnOutsidePointerDown)
    document.addEventListener('keydown', closeOnEscape)

    return () => {
      document.removeEventListener('pointerdown', closeOnOutsidePointerDown)
      document.removeEventListener('keydown', closeOnEscape)
    }
  }, [isOpen, filters.from, filters.to])

  function applyPreset(preset: typeof analyticsRangePresets[number]) {
    const today = getToday()
    if (isPresetDisabled(preset, today, accountStartDate)) {
      return
    }

    const nextRange = {
      from: getPresetFromDate(preset, today, accountStartDate),
      to: today,
    }
    setCalendarRange(nextRange)
    setRangeSelectionAnchor(null)
    setIsOpen(false)
    onChange((current) => ({
      ...current,
      from: toDateInputValue(nextRange.from),
      to: toDateInputValue(addDays(today, 1)),
    }))
  }

  function applyCalendarDay(day: Date) {
    if (day < accountStartDate) {
      return
    }

    if (!rangeSelectionAnchor) {
      setRangeSelectionAnchor(day)
      setCalendarRange({ from: day })
      return
    }

    const from = day < rangeSelectionAnchor ? day : rangeSelectionAnchor
    const to = day < rangeSelectionAnchor ? rangeSelectionAnchor : day
    setRangeSelectionAnchor(null)
    setCalendarRange({ from, to })
    setIsOpen(false)
    onChange((current) => ({
      ...current,
      from: toDateInputValue(from),
      to: toDateInputValue(addDays(to, 1)),
    }))
  }

  return (
    <div className="relative grid gap-1.5 text-sm font-semibold text-slate-700" ref={rootRef}>
      Date range
      <button
        aria-expanded={isOpen}
        aria-label={`Date range: ${display.label}. ${display.title}`}
        className="flex min-h-10 w-full items-center justify-between gap-3 rounded-md border border-slate-300 bg-white px-3 py-2 text-left text-sm text-slate-950 shadow-sm hover:border-cyan-500 focus:border-cyan-500 focus:outline-none focus:ring-2 focus:ring-cyan-500/30"
        title={display.title}
        type="button"
        onClick={() => {
          setIsOpen((current) => {
            const next = !current
            setRangeSelectionAnchor(null)
            setCalendarRange(selectedRange)
            return next
          })
        }}
      >
        <span className="truncate">{display.label}</span>
        <span className="shrink-0 rounded-full bg-cyan-500/15 px-2 py-0.5 text-xs font-semibold text-cyan-300">
          {display.toLabel}
        </span>
      </button>
      {isOpen ? (
        <div className="absolute right-0 top-full z-30 mt-2 w-[min(44rem,calc(100vw-2rem))] rounded-2xl border border-slate-700 bg-slate-950 p-3 text-slate-100 shadow-xl">
          <div className="mb-3 grid gap-2 sm:grid-cols-3 lg:grid-cols-6">
            {analyticsRangePresets.map((preset) => {
              const today = getToday()
              const isActive = display.presetLabel === preset.label
              const isDisabled = isPresetDisabled(preset, today, accountStartDate)
              return (
                <button
                  aria-disabled={isDisabled}
                  className={`rounded-lg border px-3 py-2 text-xs font-semibold transition ${
                    isActive
                      ? 'border-cyan-400 bg-cyan-500/20 text-cyan-100'
                      : isDisabled
                        ? 'cursor-not-allowed border-slate-800 bg-slate-950 text-slate-600'
                      : 'border-slate-700 bg-slate-900 text-slate-300 hover:border-cyan-500 hover:bg-slate-800 hover:text-white'
                  }`}
                  disabled={isDisabled}
                  key={preset.label}
                  type="button"
                  onClick={() => applyPreset(preset)}
                >
                  {preset.label}
                </button>
              )
            })}
          </div>
          <DayPicker
            classNames={dayPickerClassNames}
            defaultMonth={calendarRange?.from ?? fromDate}
            disabled={{ before: accountStartDate }}
            mode="range"
            numberOfMonths={2}
            selected={calendarRange}
            onDayClick={applyCalendarDay}
          />
        </div>
      ) : null}
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
  return toDateInputValue(addDays(getToday(), 1))
}

function defaultFromDate() {
  return toDateInputValue(maxDate(subMonths(getToday(), 3), getAccountStartDate()))
}

function toDateInputValue(date: Date) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function getToday() {
  const date = new Date()
  date.setHours(0, 0, 0, 0)
  return date
}

function getAccountStartDate() {
  const createdAt = getStoredSession()?.createdAt
  if (!createdAt) {
    return new Date(1900, 0, 1)
  }

  const date = parseISO(createdAt)
  if (Number.isNaN(date.getTime())) {
    return new Date(1900, 0, 1)
  }

  date.setHours(0, 0, 0, 0)
  return date
}

function maxDate(left: Date, right: Date) {
  return left > right ? left : right
}

function getPresetFromDate(
  preset: typeof analyticsRangePresets[number],
  today: Date,
  accountStartDate: Date,
) {
  return preset.label === 'Beginning' ? accountStartDate : preset.getFrom(today) ?? accountStartDate
}

function isPresetDisabled(
  preset: typeof analyticsRangePresets[number],
  today: Date,
  accountStartDate: Date,
) {
  const from = preset.getFrom(today)
  return from !== null && from < accountStartDate
}

function parseDateInput(value: string) {
  return parseISO(value)
}

function getInclusiveToDate(exclusiveToValue: string) {
  return addDays(parseDateInput(exclusiveToValue), -1)
}

function getDateRangeDisplay(filters: ReturnType<typeof getAnalyticsFilters>, accountStartDate: Date) {
  const from = parseDateInput(filters.from)
  const toInclusive = getInclusiveToDate(filters.to)
  const today = getToday()
  const preset = analyticsRangePresets.find((item) =>
    !isPresetDisabled(item, today, accountStartDate) &&
    isSameDay(from, getPresetFromDate(item, today, accountStartDate)) &&
    isSameDay(toInclusive, today))
  const toLabel = isSameDay(toInclusive, today) ? 'Today' : format(toInclusive, 'MMM d, yyyy')
  const label = preset ? preset.label : `${format(from, 'MMM d, yyyy')} – ${toLabel}`

  return {
    label,
    presetLabel: preset?.label,
    toLabel,
    title: `${format(from, 'MMM d, yyyy')} – ${format(toInclusive, 'MMM d, yyyy')}`,
  }
}

const dayPickerClassNames = {
  root: 'text-slate-100',
  months: 'grid gap-4 md:grid-cols-2',
  month: 'space-y-3',
  month_caption: 'flex items-center justify-center px-8 py-1 text-sm font-semibold text-slate-100',
  caption_label: 'rounded-md px-2 py-1',
  nav: 'absolute right-3 top-[4.75rem] flex gap-2',
  button_previous: 'grid h-8 w-8 place-items-center rounded-md border border-slate-700 bg-slate-900 text-slate-200 hover:border-cyan-500 hover:text-cyan-100',
  button_next: 'grid h-8 w-8 place-items-center rounded-md border border-slate-700 bg-slate-900 text-slate-200 hover:border-cyan-500 hover:text-cyan-100',
  chevron: 'h-4 w-4 fill-current',
  month_grid: 'w-full border-separate border-spacing-1',
  weekdays: 'text-xs uppercase tracking-wide text-slate-500',
  weekday: 'h-8 text-center font-semibold',
  week: '',
  day: 'h-9 w-9 rounded-md text-center text-sm text-slate-200',
  day_button: 'h-9 w-9 rounded-md font-medium hover:bg-cyan-500/20 hover:text-cyan-100 focus:outline-none focus:ring-2 focus:ring-cyan-400/60',
  today: 'text-cyan-200',
  selected: 'bg-cyan-500 text-slate-950',
  range_start: 'rounded-l-full bg-cyan-400 text-slate-950',
  range_middle: 'rounded-none bg-cyan-500/20 text-cyan-100',
  range_end: 'rounded-r-full bg-cyan-400 text-slate-950',
  outside: 'text-slate-600',
  disabled: 'text-slate-700',
}

function getAnalyticsEmptyMessage(data: BookAnalyticsDto | undefined, query: string) {
  if (!data || data.overview.totalBooks > 0) {
    return null
  }

  return query.trim()
    ? 'No books match the current analytics filters.'
    : 'Your library is empty. Add books to populate analytics.'
}
