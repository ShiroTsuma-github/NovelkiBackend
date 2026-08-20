import type { BookAnalyticsRelationCountDto } from '@/api/types'
import { useState } from 'react'
import { buttonVariants } from '@/components/app/DesignSystem'
import { DrilldownLink, fieldQuery, formatCount, formatPercent } from './chartUtils'

type TopRelationsChartProps = {
  field: 'genre' | 'tag'
  items: BookAnalyticsRelationCountDto[]
  title: string
}

const topLimit = 5

export function TopRelationsChart({ field, items, title }: TopRelationsChartProps) {
  const [showAll, setShowAll] = useState(false)
  const rows = showAll ? toAllRelationRows(items) : toTopRelationRows(items)

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
          <div className="ui-progress-track">
            <div className="ui-progress-fill" style={{ width: `${Math.min(100, item.shareOfBooks)}%` }} />
          </div>
        </div>
      ))}
      {items.length > topLimit ? (
        <button
          aria-expanded={showAll}
          className={`${buttonVariants.ghost} justify-self-start`}
          type="button"
          onClick={() => setShowAll((current) => !current)}
        >
          {showAll ? `Show top ${topLimit}` : `Show all ${items.length}`}
        </button>
      ) : null}
    </div>
  )
}

export function relationRows(items: BookAnalyticsRelationCountDto[]) {
  return toAllRelationRows(items).map((item) => [
    item.name,
    formatCount(item.bookCount),
    formatPercent(item.shareOfBooks),
  ])
}

function toAllRelationRows(items: BookAnalyticsRelationCountDto[]) {
  return [...items]
    .sort((left, right) => right.bookCount - left.bookCount || left.name.localeCompare(right.name))
    .map((item) => ({ ...item, isOther: false }))
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
