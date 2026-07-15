import { Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { BookAnalyticsDto } from '@/api/types'
import { DrilldownLink, formatCount } from './chartUtils'

type ReadingActivityChartProps = {
  data: BookAnalyticsDto | undefined
}

export function ReadingActivityChart({ data }: ReadingActivityChartProps) {
  const points = data?.activity.points ?? []

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
        {points.map((point) => (
          <div className="rounded-md border border-slate-200 bg-white px-3 py-2 text-sm" key={point.date}>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <DrilldownLink query={`updated:=${point.date}`}>{point.date}</DrilldownLink>
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
  return (data?.activity.points ?? []).map((point) => [
    point.date,
    formatCount(point.progressEvents),
    formatCount(point.booksTouched),
    formatCount(point.chaptersAdvanced),
  ])
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
