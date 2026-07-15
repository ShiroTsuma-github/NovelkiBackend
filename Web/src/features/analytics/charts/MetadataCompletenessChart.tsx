import type { BookAnalyticsDto } from '@/api/types'
import { DrilldownLink, formatCount, formatPercent, noneQuery } from './chartUtils'

type MetadataCompletenessChartProps = {
  data: BookAnalyticsDto | undefined
}

const missingFieldQueries: Record<string, string> = {
  author: noneQuery('author'),
  genre: noneQuery('genre'),
  tag: noneQuery('tag'),
  rating: noneQuery('rating'),
  priority: noneQuery('priority'),
  totalChapters: noneQuery('total'),
  link: noneQuery('link'),
  usableCover: noneQuery('cover'),
}

export function MetadataCompletenessChart({ data }: MetadataCompletenessChartProps) {
  const items = data?.quality.fieldCompleteness ?? []
  const totalBooks = data?.overview.totalBooks ?? 0

  if (!items.length) {
    return <div className="grid min-h-56 place-items-center text-sm text-slate-600">0 metadata fields reported for this scope.</div>
  }

  return (
    <div className="grid gap-3">
      <p className="text-sm text-slate-600">
        Cleanup queue: fields with lower coverage are good candidates for metadata review.
      </p>
      {items.map((item) => {
        const missingCount = Math.max(0, totalBooks - item.bookCount)
        const query = missingFieldQueries[item.field]
        return (
          <div className="grid gap-1" key={item.field}>
            <div className="flex flex-wrap items-center justify-between gap-3 text-sm">
              <span className="font-semibold text-slate-950">{formatFieldName(item.field)}</span>
              <span className="text-slate-700">
                {formatCount(item.bookCount)} complete · {formatPercent(item.shareOfBooks)}
              </span>
            </div>
            <div className="h-3 overflow-hidden rounded-full bg-slate-100" aria-label={`${formatFieldName(item.field)} completeness ${formatPercent(item.shareOfBooks)}`}>
              <div className="h-full rounded-full bg-cyan-600" style={{ width: `${Math.min(100, Math.max(0, item.shareOfBooks))}%` }} />
            </div>
            <div className="text-sm text-slate-600">
              Missing: {query && missingCount > 0 ? (
                <DrilldownLink query={query}>{formatCount(missingCount)} books</DrilldownLink>
              ) : `${formatCount(missingCount)} books`}
            </div>
          </div>
        )
      })}
    </div>
  )
}

export function metadataCompletenessRows(data?: BookAnalyticsDto) {
  const totalBooks = data?.overview.totalBooks ?? 0
  return (data?.quality.fieldCompleteness ?? []).map((item) => [
    formatFieldName(item.field),
    formatCount(item.bookCount),
    formatPercent(item.shareOfBooks),
    formatCount(Math.max(0, totalBooks - item.bookCount)),
  ])
}

export function formatFieldName(field: string) {
  return field
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/^./, (first) => first.toUpperCase())
}
