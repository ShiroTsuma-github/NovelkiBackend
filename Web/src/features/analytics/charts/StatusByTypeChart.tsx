import { Bar, BarChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { BookAnalyticsDto } from '@/api/types'
import { chartColors, DrilldownLink, fieldQuery, formatCount, formatPercent, normalizedPercent, percent } from './chartUtils'

type StatusByTypeChartProps = {
  data: BookAnalyticsDto | undefined
}

export function StatusByTypeChart({ data }: StatusByTypeChartProps) {
  const items = data?.composition.statusByType ?? []
  const statuses = Array.from(new Set(items.flatMap((item) => item.statuses.map((status) => status.status))))
  const chartData = items.map((item) => {
    const row: Record<string, number | string> = { type: item.type, totalBooks: item.totalBooks }
    for (const status of statuses) {
      const count = item.statuses.find((entry) => entry.status === status)?.bookCount ?? 0
      row[status] = normalizedPercent(count, item.totalBooks)
      row[`${status}Count`] = count
    }

    return row
  })

  if (!items.length) {
    return <div className="grid min-h-56 place-items-center text-sm text-slate-500">No status/type data for this analytics scope.</div>
  }

  return (
    <div className="grid gap-4">
      <div className="h-72 w-full min-w-0" data-testid="status-by-type-chart">
        <ResponsiveContainer>
          <BarChart data={chartData} layout="vertical" margin={{ left: 8, right: 16 }}>
            <XAxis domain={[0, 100]} tickFormatter={(value) => formatPercent(Number(value))} type="number" />
            <YAxis dataKey="type" tickLine={false} type="category" width={96} />
            <Tooltip
              formatter={(value, name, item) => [
                `${formatPercent(Number(value))} (${formatCount(Number(item.payload[`${String(name)}Count`] ?? 0))} books)`,
                String(name),
              ]}
            />
            {statuses.map((status, index) => (
              <Bar dataKey={status} fill={chartColors[index % chartColors.length]} key={status} stackId="books" />
            ))}
          </BarChart>
        </ResponsiveContainer>
      </div>
      <div className="grid gap-2">
        {items.map((item) => (
          <div className="rounded-md border border-slate-200 bg-white px-3 py-2 text-sm" key={item.type}>
            <div className="flex items-center justify-between gap-3">
              <DrilldownLink query={fieldQuery('type', item.type)}>{item.type}</DrilldownLink>
              <span className="text-slate-500">{formatCount(item.totalBooks)} books</span>
            </div>
            <div className="mt-2 flex flex-wrap gap-2">
              {item.statuses.map((status) => (
                <DrilldownLink key={`${item.type}-${status.status}`} query={`${fieldQuery('type', item.type)} ${fieldQuery('status', status.status)}`}>
                  {status.status}: {formatCount(status.bookCount)}
                </DrilldownLink>
              ))}
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}

export function statusByTypeRows(data?: BookAnalyticsDto) {
  return (data?.composition.statusByType ?? []).flatMap((item) =>
    item.statuses.map((status) => [item.type, status.status, formatCount(status.bookCount), formatPercent(percent(status.bookCount, item.totalBooks))]),
  )
}
