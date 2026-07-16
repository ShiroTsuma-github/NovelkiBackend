import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { addDays, format, isSameDay, parseISO, subMonths, subYears } from 'date-fns'
import { useEffect, useMemo, useRef, useState } from 'react'
import { DayPicker, type DateRange } from 'react-day-picker'
import { useSearchParams } from 'react-router-dom'
import { api } from '@/api/client'
import { getStoredSession } from '@/api/http'
import type { BookAnalyticsDto } from '@/api/types'
import { Badge, buttonVariants, controlClass, PageHeader, Surface } from '@/components/app/DesignSystem'
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
      <PageHeader
        actions={analyticsQuery.isFetching && data ? <Badge tone="accent">Refreshing...</Badge> : undefined}
        description="Library metrics scoped by book search and created-date range."
        eyebrow="Library intelligence"
        title="Analytics"
      />

      <Surface className="grid gap-3 p-4">
        <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_18rem_9rem_auto]">
          <label className="ui-form-field min-w-0">
            <span className="ui-field-label">Query</span>
            <input
              className={controlClass}
              placeholder="author:Toika rating:>=8"
              value={draftFilters.query}
              onChange={(event) => setDraftFilters((current) => ({ ...current, query: event.target.value }))}
            />
          </label>
          <DateRangeChooser filters={draftFilters} onChange={setDraftFilters} />
          <label className="ui-form-field">
            <span className="ui-field-label">Bucket</span>
            <select
              className={controlClass}
              value={draftFilters.bucket}
              onChange={(event) => setDraftFilters((current) => ({ ...current, bucket: normalizeBucket(event.target.value) }))}
            >
              {analyticsBuckets.map((bucket) => <option key={bucket} value={bucket}>{bucket}</option>)}
            </select>
          </label>
          <div className="flex items-end">
            <button className={`${buttonVariants.secondary} w-full`} type="button" onClick={applyFilters}>Apply filters</button>
          </div>
        </div>
      </Surface>

      {analyticsQuery.isError && !data ? (
        <Surface className="p-4" tone="danger">
          <h2 className="ui-panel-title text-inherit">Could not load analytics.</h2>
          <p className="mt-1 text-sm text-inherit opacity-85">Check filters and retry the request.</p>
          <button className={`${buttonVariants.destructive} mt-3`} type="button" onClick={() => analyticsQuery.refetch()}>
            Retry
          </button>
        </Surface>
      ) : null}

      {emptyMessage ? (
        <Surface className="p-4 text-sm text-slate-600" tone="muted">
          {emptyMessage}
        </Surface>
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
          dataTableEnabled={false}
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
          dataTableEnabled={false}
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
          dataTableEnabled={false}
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
  const today = getToday()
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
    if (day < accountStartDate || day > today) {
      return
    }

    if (!rangeSelectionAnchor) {
      setRangeSelectionAnchor(day)
      setCalendarRange({ from: day, to: day })
      onChange((current) => ({
        ...current,
        from: toDateInputValue(day),
        to: toDateInputValue(addDays(day, 1)),
      }))
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
    <div className="ui-form-field relative" ref={rootRef}>
      <span className="ui-field-label">Date range</span>
      <button
        aria-expanded={isOpen}
        aria-label={`Date range: ${display.label}. ${display.title}`}
        className={`${controlClass} flex items-center justify-between gap-3 text-left`}
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
        {display.toLabel ? (
          <Badge className="shrink-0" tone="accent">{display.toLabel}</Badge>
        ) : null}
      </button>
      {isOpen ? (
        <div className="ui-popover absolute right-0 top-full mt-2 w-[min(44rem,calc(100vw-2rem))] p-3">
          <div className="mb-3 grid gap-2 sm:grid-cols-3 lg:grid-cols-6">
            {analyticsRangePresets.map((preset) => {
              const today = getToday()
              const isActive = display.presetLabel === preset.label
              const isDisabled = isPresetDisabled(preset, today, accountStartDate)
              return (
                <button
                  aria-disabled={isDisabled}
                  className={`ui-filter-chip ${isActive ? 'ui-filter-chip--active' : ''}`}
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
            defaultMonth={subMonths(today, 1)}
            disabled={{ before: accountStartDate, after: today }}
            mode="range"
            modifiers={{ rangeSelectionAnchor: rangeSelectionAnchor ?? undefined }}
            modifiersClassNames={{ rangeSelectionAnchor: dayPickerClassNames.range_start }}
            numberOfMonths={2}
            selected={rangeSelectionAnchor ? { from: undefined, to: undefined } : calendarRange}
            onDayClick={applyCalendarDay}
            onSelect={() => undefined}
          />
        </div>
      ) : null}
    </div>
  )
}

function MetricCard({ label, loading, value }: { label: string; loading: boolean; value: string }) {
  return (
    <Surface as="div" className="px-4 py-3" tone="elevated">
      <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">{label}</div>
      <div className="mt-1 text-2xl font-semibold text-slate-950">{loading ? <span className="inline-block h-8 w-16 animate-pulse rounded bg-slate-200" /> : value}</div>
    </Surface>
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
  return toDateInputValue(getDefaultFromDate(getToday(), getAccountStartDate()))
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

function getDefaultFromDate(today: Date, accountStartDate: Date) {
  const lastThreeMonths = subMonths(today, 3)
  return accountStartDate > lastThreeMonths ? accountStartDate : lastThreeMonths
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
  if (isSameDay(from, toInclusive)) {
    const label = format(from, 'MMM d, yyyy')
    return {
      label,
      presetLabel: undefined,
      toLabel: null,
      title: label,
    }
  }

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
  root: 'text-[var(--qs-text)]',
  months: 'grid gap-4 md:grid-cols-2',
  month: 'space-y-3',
  month_caption: 'flex items-center justify-center px-8 py-1 text-sm font-semibold text-[var(--qs-text)]',
  caption_label: 'px-2 py-1',
  nav: 'absolute right-3 top-[4.75rem] flex gap-2',
  button_previous: 'grid h-11 w-11 place-items-center rounded-[var(--qs-control-radius)] border border-[var(--qs-border)] bg-[var(--qs-surface-muted)] text-[var(--qs-muted)] hover:border-[var(--qs-accent)] hover:text-[var(--qs-text)]',
  button_next: 'grid h-11 w-11 place-items-center rounded-[var(--qs-control-radius)] border border-[var(--qs-border)] bg-[var(--qs-surface-muted)] text-[var(--qs-muted)] hover:border-[var(--qs-accent)] hover:text-[var(--qs-text)]',
  chevron: 'h-4 w-4 fill-current',
  month_grid: 'w-full border-separate border-spacing-1',
  weekdays: 'text-xs uppercase tracking-wide text-[var(--qs-subtle)]',
  weekday: 'h-8 text-center font-semibold',
  week: '',
  day: 'h-11 w-11 text-center text-sm text-[var(--qs-muted)]',
  day_button: 'h-11 w-11 rounded-[var(--qs-control-radius)] font-medium hover:bg-[var(--qs-surface-hover)] hover:text-[var(--qs-text)]',
  today: 'text-[var(--qs-accent-strong)]',
  selected: 'bg-[var(--qs-accent)] text-[var(--qs-bg)]',
  range_start: 'rounded-l-full bg-[var(--qs-accent)] text-[var(--qs-bg)]',
  range_middle: 'rounded-none bg-[#262b4e] text-[#dfe2ff]',
  range_end: 'rounded-r-full bg-[var(--qs-accent)] text-[var(--qs-bg)]',
  outside: 'text-[var(--qs-subtle)]',
  disabled: 'text-[var(--qs-subtle)] opacity-40 line-through',
}

function getAnalyticsEmptyMessage(data: BookAnalyticsDto | undefined, query: string) {
  if (!data || data.overview.totalBooks > 0) {
    return null
  }

  return query.trim()
    ? 'No books match the current analytics filters.'
    : 'Your library is empty. Add books to populate analytics.'
}
