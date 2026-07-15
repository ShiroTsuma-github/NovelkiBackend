import { Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { BookAnalyticsDto, BookAnalyticsLibraryGrowthPointDto } from '@/api/types'
import { dateRangeForBucket, DrilldownLink, fieldQuery, formatCount, formatDateRange } from './chartUtils'

type LibraryGrowthChartProps = {
  data: BookAnalyticsDto | undefined
}

export function LibraryGrowthChart({ data }: LibraryGrowthChartProps) {
  const points = data?.libraryGrowth.points ?? []
  const openingCount = data?.libraryGrowth.openingCount ?? 0
  const bucket = data?.scope.bucket ?? 'day'
  const displayPoints = compactGrowthPoints(points, bucket)

  if (!points.length) {
    return (
      <div className="grid min-h-56 place-items-center text-center text-sm text-slate-600">
        <div>
          <div>No library growth points in this time range.</div>
          <div className="mt-1">Opening count: {formatCount(openingCount)} books.</div>
        </div>
      </div>
    )
  }

  return (
    <div className="grid gap-4">
      <p className="text-sm text-slate-600">
        Opening count: {formatCount(openingCount)} books. Buckets with 0 additions stay in the series so import jumps remain visible.
      </p>
      <div className="h-56 min-w-0" aria-label="Library growth trend">
        <ResponsiveContainer>
          <LineChart data={points}>
            <XAxis dataKey="date" tickLine={false} />
            <YAxis allowDecimals={false} tickLine={false} />
            <Tooltip
              formatter={(value, name) => [`${formatCount(Number(value))}`, growthLabel(name)]}
              labelFormatter={(label) => `Bucket ${label}`}
            />
            <Line dataKey="cumulativeBooks" name="cumulativeBooks" stroke="#0891b2" strokeWidth={2} dot={{ r: 3 }} />
            <Line dataKey="booksAdded" name="booksAdded" stroke="#ea580c" strokeWidth={2} dot={{ r: 3 }} />
          </LineChart>
        </ResponsiveContainer>
      </div>
      <div className="grid gap-2">
        {displayPoints.map((point) => (
          <div className="rounded-md border border-slate-200 bg-white px-3 py-2 text-sm" key={point.label}>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <span className="font-semibold text-slate-950">{point.label}</span>
              <span className="text-slate-700">
                +{formatCount(point.booksAdded)} added · {formatCount(point.cumulativeBooks)} cumulative
              </span>
            </div>
            <div className="mt-1 flex flex-wrap gap-2 text-slate-600">
              {typeSummary(point)}
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}

export function libraryGrowthRows(data?: BookAnalyticsDto) {
  return compactGrowthPoints(data?.libraryGrowth.points ?? [], data?.scope.bucket ?? 'day').map((point) => [
    point.label,
    formatCount(point.booksAdded),
    formatCount(point.cumulativeBooks),
    point.byType.length ? point.byType.map((item) => `${item.type}: ${formatCount(item.bookCount)}`).join(', ') : 'No additions',
  ])
}

function compactGrowthPoints(points: BookAnalyticsLibraryGrowthPointDto[], bucket: string) {
  return points.reduce<Array<BookAnalyticsLibraryGrowthPointDto & { label: string; endExclusive: string }>>((rows, point) => {
    const period = dateRangeForBucket(point.date, bucket)
    const isEmpty = point.booksAdded === 0 && point.byType.length === 0
    const previous = rows.at(-1)
    if (isEmpty && previous && previous.booksAdded === 0 && previous.byType.length === 0 && previous.cumulativeBooks === point.cumulativeBooks) {
      previous.endExclusive = period.end
      previous.label = formatDateRange(previous.date, previous.endExclusive)
      return rows
    }

    rows.push({ ...point, label: formatDateRange(period.start, period.end), endExclusive: period.end })
    return rows
  }, [])
}

function typeSummary(point: BookAnalyticsLibraryGrowthPointDto & { endExclusive?: string }) {
  if (!point.byType.length) {
    return <span>No additions by type.</span>
  }

  return point.byType.map((item) => (
    <DrilldownLink key={item.type} query={`${fieldQuery('type', item.type)} created:>=${point.date} created:<${point.endExclusive ?? point.date}`}>
      {item.type}: {formatCount(item.bookCount)}
    </DrilldownLink>
  ))
}

function growthLabel(name: unknown) {
  if (name === 'cumulativeBooks') {
    return 'Cumulative books'
  }
  if (name === 'booksAdded') {
    return 'Books added'
  }
  return String(name)
}
