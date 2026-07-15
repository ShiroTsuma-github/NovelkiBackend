import type { BookAnalyticsDto } from '@/api/types'
import { DrilldownLink, fieldQuery, formatCount, formatPercent, noneQuery } from './chartUtils'

type CoverAvailabilityChartProps = {
  data: BookAnalyticsDto | undefined
}

const statusQueries: Record<string, string> = {
  Failed: fieldQuery('coverStatus', 'Failed'),
  NotFound: fieldQuery('coverStatus', 'NotFound'),
  Pending: fieldQuery('coverStatus', 'Pending'),
  Found: fieldQuery('coverStatus', 'Found'),
  Uploaded: fieldQuery('coverStatus', 'Uploaded'),
}

export function CoverAvailabilityChart({ data }: CoverAvailabilityChartProps) {
  const statuses = data?.quality.coverStatuses ?? []
  const sources = data?.quality.coverSources ?? []

  if (!statuses.length && !sources.length) {
    return <div className="grid min-h-56 place-items-center text-sm text-slate-600">0 cover records reported for this scope.</div>
  }

  return (
    <div className="grid gap-4">
      <p className="text-sm text-slate-600">
        Usable covers are counted only when the backend has a Found or Uploaded cover with an image or thumbnail.
      </p>
      <div className="grid gap-2">
        <h3 className="text-sm font-semibold text-slate-950">Cover status actions</h3>
        {statuses.map((item) => (
          <div className="rounded-md border border-slate-200 bg-white px-3 py-2 text-sm" key={item.status}>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <span className="font-semibold text-slate-950">{item.status || 'Unknown status'}</span>
              <span className="text-slate-700">{formatCount(item.bookCount)} books · {formatPercent(item.shareOfBooks)}</span>
            </div>
            <div className="mt-1 text-slate-600">
              {coverStatusAction(item.status, item.bookCount)}
            </div>
          </div>
        ))}
      </div>
      <div className="grid gap-2">
        <h3 className="text-sm font-semibold text-slate-950">Top cover providers</h3>
        {sources.length ? sources.map((item) => (
          <div className="flex flex-wrap items-center justify-between gap-3 text-sm" key={item.source}>
            <DrilldownLink query={fieldQuery('coverSource', item.source)}>{item.source || 'Unknown provider'}</DrilldownLink>
            <span className="text-slate-700">{formatCount(item.bookCount)} books · {formatPercent(item.shareOfBooks)}</span>
          </div>
        )) : <div className="text-sm text-slate-600">No cover providers reported.</div>}
      </div>
    </div>
  )
}

export function coverAvailabilityRows(data?: BookAnalyticsDto) {
  const statusRows = (data?.quality.coverStatuses ?? []).map((item) => [
    'Status',
    item.status || 'Unknown status',
    formatCount(item.bookCount),
    formatPercent(item.shareOfBooks),
  ])
  const sourceRows = (data?.quality.coverSources ?? []).map((item) => [
    'Provider',
    item.source || 'Unknown provider',
    formatCount(item.bookCount),
    formatPercent(item.shareOfBooks),
  ])
  return [...statusRows, ...sourceRows]
}

function coverStatusAction(status: string, bookCount: number) {
  if (status === 'Failed') {
    return <DrilldownLink query={statusQueries.Failed}>Review failed fetches: {formatCount(bookCount)}</DrilldownLink>
  }
  if (status === 'NotFound') {
    return <DrilldownLink query={statusQueries.NotFound}>Search manually: {formatCount(bookCount)}</DrilldownLink>
  }
  if (status === 'Pending') {
    return <DrilldownLink query={statusQueries.Pending}>Queued for processing: {formatCount(bookCount)}</DrilldownLink>
  }
  if (status === 'Found' || status === 'Uploaded') {
    return <DrilldownLink query={statusQueries[status] ?? noneQuery('cover')}>Usable when image or thumbnail exists: {formatCount(bookCount)}</DrilldownLink>
  }
  return `Unknown status bucket: ${formatCount(bookCount)} books`
}
