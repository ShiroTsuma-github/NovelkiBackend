import { Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { BookAnalyticsActivityPointDto, BookAnalyticsDto } from '@/api/types'
import { Surface } from '@/components/app/DesignSystem'
import { analyticsTooltipProps, dateRangeForBucket, formatCount, formatDateRange } from './chartUtils'

type ReadingActivityChartProps = {
  data: BookAnalyticsDto | undefined
}

export function ReadingActivityChart({ data }: ReadingActivityChartProps) {
  const points = data?.activity.points ?? []
  const bucket = data?.scope.bucket ?? 'day'
  const chartPoints = readingActivityChartPoints(points, bucket)
  const displayPoints = compactActivityPoints(points, bucket)
  const newestDisplayPoints = [...displayPoints].reverse()

  if (!points.length) {
    return <div className="grid min-h-56 place-items-center text-sm text-slate-600">No reading activity in this time range.</div>
  }

  return (
    <div className="grid gap-4">
      <div className="h-56 min-w-0" aria-label="Reading activity trend">
        <ResponsiveContainer>
          <LineChart data={chartPoints}>
            <XAxis dataKey="date" tickLine={false} />
            <YAxis allowDecimals={false} tickLine={false} />
            <Tooltip
              {...analyticsTooltipProps}
              formatter={(value, name) => [`${formatCount(Number(value))}`, activityLabel(name)]}
              labelFormatter={(label) => `Bucket ${label}`}
            />
            <Line dataKey="progressEvents" name="progressEvents" stroke="#8b92d8" strokeWidth={2} dot={{ r: 3 }} />
            <Line dataKey="booksTouched" name="booksTouched" stroke="#75b69c" strokeWidth={2} dot={{ r: 3 }} />
          </LineChart>
        </ResponsiveContainer>
      </div>
      <div className="grid gap-2">
        {newestDisplayPoints.map((point) => (
          <Surface as="div" className="px-3 py-2 text-sm" key={point.label}>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <span className="font-semibold text-slate-950">{point.label}</span>
              <span className="text-slate-700">
                {formatCount(point.progressEvents)} events · {formatCount(point.booksTouched)} books touched
              </span>
            </div>
            <div className="mt-1 text-slate-600">Chapters advanced: {formatCount(point.chaptersAdvanced)}</div>
          </Surface>
        ))}
      </div>
    </div>
  )
}

export function readingActivityRows(data?: BookAnalyticsDto) {
  return compactActivityPoints(data?.activity.points ?? [], data?.scope.bucket ?? 'day').map((point) => [
    point.label,
    formatCount(point.progressEvents),
    formatCount(point.booksTouched),
    formatCount(point.chaptersAdvanced),
  ])
}

export function readingActivityChartPoints(points: BookAnalyticsActivityPointDto[], bucket: string) {
  return points.map((point) => {
    const period = dateRangeForBucket(point.date, bucket)
    return { ...point, label: formatDateRange(period.start, period.end), endExclusive: period.end }
  })
}

function compactActivityPoints(points: BookAnalyticsActivityPointDto[], bucket: string) {
  return readingActivityChartPoints(points, bucket).reduce<Array<BookAnalyticsActivityPointDto & { label: string; endExclusive: string }>>((rows, point) => {
    const isEmpty = point.progressEvents === 0 && point.booksTouched === 0 && point.chaptersAdvanced === 0
    const previous = rows.at(-1)
    if (isEmpty && previous && previous.progressEvents === 0 && previous.booksTouched === 0 && previous.chaptersAdvanced === 0) {
      previous.endExclusive = point.endExclusive
      previous.label = formatDateRange(previous.date, previous.endExclusive)
      return rows
    }

    rows.push(point)
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
