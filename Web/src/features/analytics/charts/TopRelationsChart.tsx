import type { BookAnalyticsRelationCountDto } from '@/api/types'
import { DrilldownLink, fieldQuery, formatCount, formatPercent } from './chartUtils'

type TopRelationsChartProps = {
  field: 'genre' | 'tag'
  items: BookAnalyticsRelationCountDto[]
  title: string
}

const topLimit = 5

export function TopRelationsChart({ field, items, title }: TopRelationsChartProps) {
  const rows = toTopRelationRows(items)

  if (!items.length) {
    return <div className="grid min-h-56 place-items-center text-sm text-slate-500">No {title.toLowerCase()} data for this analytics scope.</div>
  }

  return (
    <div className="grid gap-3">
      <p className="text-sm text-slate-500">
        Multi-value books can count in more than one {title.toLowerCase()} bucket; shares are measured against matching books.
      </p>
      {rows.map((item) => (
        <div className="grid gap-1" key={item.name}>
          <div className="flex items-center justify-between gap-3 text-sm">
            {item.isOther ? (
              <span className="font-semibold text-slate-950">Other</span>
            ) : (
              <DrilldownLink query={fieldQuery(field, item.name)}>{item.name}</DrilldownLink>
            )}
            <span className="text-slate-500">{formatCount(item.bookCount)} books · {formatPercent(item.shareOfBooks)}</span>
          </div>
          <div className="h-3 overflow-hidden rounded-full bg-slate-100">
            <div className="h-full rounded-full bg-cyan-500" style={{ width: `${Math.min(100, item.shareOfBooks)}%` }} />
          </div>
        </div>
      ))}
    </div>
  )
}

export function relationRows(items: BookAnalyticsRelationCountDto[]) {
  return toTopRelationRows(items).map((item) => [
    item.name,
    formatCount(item.bookCount),
    formatPercent(item.shareOfBooks),
    item.isOther ? 'Grouped remainder' : 'Top category',
  ])
}

function toTopRelationRows(items: BookAnalyticsRelationCountDto[]) {
  const sorted = [...items].sort((left, right) => right.bookCount - left.bookCount || left.name.localeCompare(right.name))
  const top = sorted.slice(0, topLimit).map((item) => ({ ...item, isOther: false }))
  const other = sorted.slice(topLimit)
  if (!other.length) {
    return top
  }

  return [
    ...top,
    {
      name: 'Other',
      bookCount: other.reduce((sum, item) => sum + item.bookCount, 0),
      shareOfBooks: other.reduce((sum, item) => sum + item.shareOfBooks, 0),
      isOther: true,
    },
  ]
}
