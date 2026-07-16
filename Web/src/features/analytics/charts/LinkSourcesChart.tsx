import type { BookAnalyticsDto } from '@/api/types'
import { DrilldownLink, fieldQuery, formatCount, formatPercent } from './chartUtils'

type LinkSourcesChartProps = {
  data: BookAnalyticsDto | undefined
}

export function LinkSourcesChart({ data }: LinkSourcesChartProps) {
  const items = data?.quality.linkSources ?? []

  if (!items.length) {
    return <div className="grid min-h-56 place-items-center text-sm text-slate-600">0 link sources found for this scope.</div>
  }

  return (
    <div className="grid gap-3">
      <p className="text-sm text-slate-600">
        Link count and book coverage are separate: one book can contain multiple links from the same source.
      </p>
      {items.map((item) => (
        <div className="grid gap-1" key={item.source}>
          <div className="flex flex-wrap items-center justify-between gap-3 text-sm">
            <DrilldownLink query={fieldQuery('link', item.source)}>{item.source || 'Unknown source'}</DrilldownLink>
            <span className="text-slate-700">
              {formatCount(item.linkCount)} links · {formatCount(item.bookCount)} books · {formatPercent(item.shareOfBooks)}
            </span>
          </div>
          <div className="ui-progress-track" aria-label={`${item.source || 'Unknown source'} link coverage ${formatPercent(item.shareOfBooks)}`}>
            <div className="ui-progress-fill" style={{ width: `${Math.min(100, Math.max(0, item.shareOfBooks))}%` }} />
          </div>
        </div>
      ))}
    </div>
  )
}

export function linkSourceRows(data?: BookAnalyticsDto) {
  return (data?.quality.linkSources ?? []).map((item) => [
    item.source || 'Unknown source',
    formatCount(item.linkCount),
    formatCount(item.bookCount),
    formatPercent(item.shareOfBooks),
  ])
}
