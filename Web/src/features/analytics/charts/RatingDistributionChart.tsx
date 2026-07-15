import { Bar, BarChart, Cell, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { BookAnalyticsDto } from '@/api/types'
import { DrilldownLink, formatCount, formatPercent, noneQuery, numberQuery, percent } from './chartUtils'

type RatingDistributionChartProps = {
  data: BookAnalyticsDto | undefined
}

export function RatingDistributionChart({ data }: RatingDistributionChartProps) {
  const ratings = data?.ratings
  const counts = ratings?.counts ?? []
  const totalBooks = (ratings?.ratedBooks ?? 0) + (ratings?.unratedBooks ?? 0)
  const coverage = percent(ratings?.ratedBooks ?? 0, totalBooks)

  if (!ratings || totalBooks === 0) {
    return <div className="grid min-h-56 place-items-center text-sm text-slate-500">No rating data for this analytics scope.</div>
  }

  return (
    <div className="grid gap-4">
      <div className="grid gap-3 sm:grid-cols-[9rem_minmax(0,1fr)]">
        <div className="grid place-items-center rounded-xl border border-slate-200 bg-white p-4 text-center">
          <div className="grid h-24 w-24 place-items-center rounded-full border-8 border-cyan-500 bg-slate-50">
            <div>
              <div className="text-xl font-semibold text-slate-950">{formatPercent(coverage)}</div>
              <div className="text-xs text-slate-500">rated</div>
            </div>
          </div>
          <div className="mt-3 text-sm text-slate-500">
            Avg {ratings.averageRating == null ? '-' : ratings.averageRating.toFixed(1)}
          </div>
        </div>
        <div className="h-64 min-w-0">
          <ResponsiveContainer>
            <BarChart data={counts} margin={{ left: 0, right: 8 }}>
              <XAxis dataKey="rating" tickLine={false} />
              <YAxis allowDecimals={false} tickLine={false} />
              <Tooltip formatter={(value, _name, item) => [`${formatCount(Number(value))} books`, `Rating ${item.payload.rating}`]} />
              <Bar dataKey="bookCount" name="Books" radius={[6, 6, 0, 0]}>
                {counts.map((item) => (
                  <Cell fill={item.bookCount > 0 ? '#7c3aed' : '#334155'} key={item.rating} />
                ))}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </div>
      </div>
      <div className="flex flex-wrap gap-2 text-sm">
        {counts.map((item) => (
          <DrilldownLink key={item.rating} query={numberQuery('rating', item.rating)}>
            {item.rating}: {formatCount(item.bookCount)}
          </DrilldownLink>
        ))}
        <DrilldownLink query={noneQuery('rating')}>Unrated: {formatCount(ratings.unratedBooks)}</DrilldownLink>
      </div>
    </div>
  )
}

export function ratingRows(data?: BookAnalyticsDto) {
  const ratings = data?.ratings
  if (!ratings) {
    return []
  }

  return [
    ...ratings.counts.map((item) => [String(item.rating), formatCount(item.bookCount), 'Rated']),
    ['Unrated', formatCount(ratings.unratedBooks), 'Missing rating'],
  ]
}
