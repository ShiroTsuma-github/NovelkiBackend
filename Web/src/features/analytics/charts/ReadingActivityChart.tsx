import { Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { BookAnalyticsActivityPointDto, BookAnalyticsDto } from '@/api/types'
import { DrilldownLink, formatCount } from './chartUtils'

type ReadingActivityChartProps = {
  data: BookAnalyticsDto | undefined
}

export function ReadingActivityChart({ data }: ReadingActivityChartProps) {
  const points = data?.activity.points ?? []
  const displayPoints = compactActivityPoints(points)

  if (!points.length) {
    return <div className="grid min-h-56 place-items-center text-sm text-slate-600">No reading activity in this time range.</div>
  }

  return (
    <div className="grid gap-4">
      <p className="text-sm text-slate-600">
        Progress events and touched books use separate lines; chapters advanced are listed below for exact bucket context.
      </p>
      <div className="h-56 min-w-0" aria-label="Reading activity trend">
        <ResponsiveContainer>
          <LineChart data={points}>
            <XAxis dataKey="date" tickLine={false} />
            <YAxis allowDecimals={false} tickLine={false} />
            <Tooltip
              formatter={(value, name) => [`${formatCount(Number(value))}`, activityLabel(name)]}
              labelFormatter={(label) => `Bucket ${label}`}
            />
            <Line dataKey="progressEvents" name="progressEvents" stroke="#0891b2" strokeWidth={2} dot={{ r: 3 }} />
            <Line dataKey="booksTouched" name="booksTouched" stroke="#7c3aed" strokeWidth={2} dot={{ r: 3 }} />
          </LineChart>
        </ResponsiveContainer>
      </div>
      <div className="grid gap-2">
        {displayPoints.map((point) => (
          <div className="rounded-md border border-slate-200 bg-white px-3 py-2 text-sm" key={point.label}>
            <div className="flex flex-wrap items-center justify-between gap-3">
              {point.queryDate ? <DrilldownLink query={`updated:=${point.queryDate}`}>{point.label}</DrilldownLink> : <span className="font-semibold text-slate-950">{point.label}</span>}
              <span className="text-slate-700">
                {formatCount(point.progressEvents)} events · {formatCount(point.booksTouched)} books touched
              </span>
            </div>
            <div className="mt-1 text-slate-600">Chapters advanced: {formatCount(point.chaptersAdvanced)}</div>
          </div>
        ))}
      </div>
    </div>
  )
}

export function readingActivityRows(data?: BookAnalyticsDto) {
  return compactActivityPoints(data?.activity.points ?? []).map((point) => [
    point.label,
    formatCount(point.progressEvents),
    formatCount(point.booksTouched),
    formatCount(point.chaptersAdvanced),
  ])
}

function compactActivityPoints(points: BookAnalyticsActivityPointDto[]) {
  return points.reduce<Array<BookAnalyticsActivityPointDto & { label: string; queryDate?: string }>>((rows, point) => {
    const isEmpty = point.progressEvents === 0 && point.booksTouched === 0 && point.chaptersAdvanced === 0
    const previous = rows.at(-1)
    if (isEmpty && previous && previous.progressEvents === 0 && previous.booksTouched === 0 && previous.chaptersAdvanced === 0) {
      previous.label = `${previous.label.split(' - ')[0]} - ${point.date}`
      previous.queryDate = undefined
      return rows
    }

    rows.push({ ...point, label: point.date, queryDate: isEmpty ? undefined : point.date })
    return rows
  }, [])
}

function activityLabel(name: unknown) {
  if (name === 'progressEvents') {
    return 'Progress events'
  }
  if (name === 'booksTouched') {
    return 'Books touched'
  }
  return String(name)
}
