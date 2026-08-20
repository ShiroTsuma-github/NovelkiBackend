import { Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { BookAnalyticsActivityPointDto, BookAnalyticsDto } from '@/api/types'
import { Surface } from '@/components/app/DesignSystem'
import { analyticsTooltipProps, dateRangeForBucket, formatCount, formatDateRange } from './chartUtils'

type ReadingActivityChartProps = {
  data: BookAnalyticsDto | undefined
}

export function ReadingActivityChart({ data }: ReadingActivityChartProps) {
  const points = data?.activity.points ?? []
  const baselineChapters = data?.activity.baselineChapters ?? 0
  const bucket = data?.scope.bucket ?? 'day'
  const chartPoints = readingActivityChartPoints(points, bucket, baselineChapters)
  const displayPoints = compactActivityPoints(points, bucket, baselineChapters)
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
            <YAxis allowDecimals={false} tickLine={false} yAxisId="daily" />
            <YAxis allowDecimals={false} orientation="right" tickLine={false} yAxisId="cumulative" />
            <Tooltip
              {...analyticsTooltipProps}
              formatter={(value, name) => [`${formatCount(Number(value))}`, activityLabel(name)]}
              labelFormatter={(label) => `Bucket ${label}`}
            />
            <Line dataKey="dailyChapters" name="dailyChapters" stroke="#75b69c" strokeWidth={2} yAxisId="daily" dot={{ r: 3 }} />
            <Line dataKey="cumulativeChapters" name="cumulativeChapters" stroke="#8b92d8" strokeWidth={2} yAxisId="cumulative" dot={{ r: 3 }} />
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
            <div className="mt-1 text-slate-600">
              Daily: {formatCount(point.dailyChapters)} chapters. Cumulative: {formatCount(point.cumulativeChapters)} chapters.
            </div>
          </Surface>
        ))}
      </div>
    </div>
  )
}

export function readingActivityRows(data?: BookAnalyticsDto) {
  return compactActivityPoints(
    data?.activity.points ?? [],
    data?.scope.bucket ?? 'day',
    data?.activity.baselineChapters ?? 0,
  ).map((point) => [
    point.label,
    formatCount(point.dailyChapters),
    formatCount(point.cumulativeChapters),
    formatCount(point.progressEvents),
    formatCount(point.booksTouched),
  ])
}

export function readingActivityChartPoints(
  points: BookAnalyticsActivityPointDto[],
  bucket: string,
  baselineChapters = 0,
) {
  let cumulativeChapters = baselineChapters
  return points.map((point) => {
    const period = dateRangeForBucket(point.date, bucket)
    const dailyChapters = point.chaptersAdvanced
    cumulativeChapters += dailyChapters
    return {
      ...point,
      dailyChapters,
      cumulativeChapters,
      label: formatDateRange(period.start, period.end),
      endExclusive: period.end,
    }
  })
}

function compactActivityPoints(points: BookAnalyticsActivityPointDto[], bucket: string, baselineChapters = 0) {
  return readingActivityChartPoints(points, bucket, baselineChapters).reduce<Array<BookAnalyticsActivityPointDto & {
    dailyChapters: number
    cumulativeChapters: number
    label: string
    endExclusive: string
  }>>((rows, point) => {
    const isEmpty = point.progressEvents === 0 && point.booksTouched === 0 && point.dailyChapters === 0
    const previous = rows.at(-1)
    if (isEmpty && previous && previous.progressEvents === 0 && previous.booksTouched === 0 && previous.dailyChapters === 0) {
      previous.endExclusive = point.endExclusive
      previous.label = formatDateRange(previous.date, previous.endExclusive)
      previous.cumulativeChapters = point.cumulativeChapters
      return rows
    }

    rows.push(point)
    return rows
  }, [])
}

function activityLabel(name: unknown) {
  if (name === 'dailyChapters') {
    return 'Daily chapters advanced'
  }
  if (name === 'cumulativeChapters') {
    return 'Cumulative tracked chapters'
  }
  return String(name)
}
