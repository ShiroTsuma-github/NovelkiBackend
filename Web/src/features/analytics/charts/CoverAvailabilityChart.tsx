import type { BookAnalyticsDto } from '@/api/types'
import { formatCount, formatPercent } from './chartUtils'

type CoverAvailabilityChartProps = {
  data: BookAnalyticsDto | undefined
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
            <span className="font-semibold text-slate-950">{item.source || 'Unknown provider'}</span>
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
    return `Review failed fetches: ${formatCount(bookCount)}`
  }
  if (status === 'NotFound') {
    return `Search manually: ${formatCount(bookCount)}`
  }
  if (status === 'Pending') {
    return `Queued for processing: ${formatCount(bookCount)}`
  }
  if (status === 'Found' || status === 'Uploaded') {
    return `Usable when image or thumbnail exists: ${formatCount(bookCount)}`
  }
  return `Unknown status bucket: ${formatCount(bookCount)} books`
}
