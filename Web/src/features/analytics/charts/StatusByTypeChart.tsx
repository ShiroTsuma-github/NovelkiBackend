import { Bar, BarChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { BookAnalyticsDto } from '@/api/types'
import { analyticsTooltipProps, chartColors, DrilldownLink, fieldQuery, formatCount, formatPercent, getActiveChartLabel, normalizedPercent, percent, useBooksDrilldown } from './chartUtils'

type StatusByTypeChartProps = {
  data: BookAnalyticsDto | undefined
}

type StatusByTypeTooltipEntry = {
  name?: unknown
  value?: unknown
  color?: string
  payload?: Record<string, unknown>
}

type StatusByTypeTooltipProps = {
  active?: boolean
  label?: unknown
  payload?: StatusByTypeTooltipEntry[]
}

export function StatusByTypeChart({ data }: StatusByTypeChartProps) {
  const items = data?.composition.statusByType ?? []
  const openBooks = useBooksDrilldown()
  const statuses = Array.from(new Set(items.flatMap((item) => item.statuses.map((status) => status.status))))
  const chartData = items.map((item) => {
    const row: Record<string, number | string> = { type: item.type, totalBooks: item.totalBooks }
    const countsByStatus = Object.fromEntries(item.statuses.map((status) => [status.status, status.bookCount]))
    const displayPercents = distributeStackedPercents(statuses.map((status) => countsByStatus[status] ?? 0), item.totalBooks)
    for (const status of statuses) {
      const statusIndex = statuses.indexOf(status)
      const count = countsByStatus[status] ?? 0
      row[status] = displayPercents[statusIndex]
      row[`${status}Percent`] = normalizedPercent(count, item.totalBooks)
      row[`${status}Count`] = count
    }

    return row
  })

  if (!items.length) {
    return <div className="grid min-h-56 place-items-center text-sm text-slate-500">No status/type data for this analytics scope.</div>
  }

  return (
    <div className="grid gap-4">
      <div className="analytics-drilldown-chart h-72 w-full min-w-0" data-testid="status-by-type-chart">
        <ResponsiveContainer>
          <BarChart
            data={chartData}
            layout="vertical"
            margin={{ left: 8, right: 16 }}
            onClick={(event) => {
              const type = getActiveChartLabel(event)
              if (typeof type === 'string') {
                openBooks(fieldQuery('type', type))
              }
            }}
          >
            <XAxis domain={[0, 100]} tickFormatter={(value) => formatPercent(Number(value))} type="number" />
            <YAxis dataKey="type" tickLine={false} type="category" width={96} />
            <Tooltip
              {...analyticsTooltipProps}
              content={<StatusByTypeTooltip />}
            />
            {statuses.map((status, index) => (
              <Bar
                className="analytics-drilldown-shape"
                dataKey={status}
                fill={chartColors[index % chartColors.length]}
                key={status}
                stackId="books"
                onClick={(entry, _index, event) => {
                  event.stopPropagation()
                  const payload = getChartPayload(entry)
                  const type = payload.type
                  const count = payload[`${status}Count`]
                  if (typeof type === 'string' && Number(count) > 0) {
                    openBooks(`${fieldQuery('type', type)} ${fieldQuery('status', status)}`)
                  }
                }}
              />
            ))}
          </BarChart>
        </ResponsiveContainer>
        <div className="sr-only">
          {items.map((item) => (
            <DrilldownLink key={item.type} query={fieldQuery('type', item.type)}>
              Open {item.type} books
            </DrilldownLink>
          ))}
          {items.flatMap((item) => item.statuses.map((status) => (
            <DrilldownLink key={`${item.type}-${status.status}`} query={`${fieldQuery('type', item.type)} ${fieldQuery('status', status.status)}`}>
              Open {item.type} {status.status} books
            </DrilldownLink>
          )))}
        </div>
      </div>
      <div className="grid gap-2">
        {items.map((item) => (
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-slate-200 bg-white px-3 py-2 text-sm" key={item.type}>
            <span className="font-semibold text-slate-950">{item.type}</span>
            <span className="text-slate-500">{formatCount(item.totalBooks)} books</span>
          </div>
        ))}
      </div>
    </div>
  )
}

function StatusByTypeTooltip({ active, label, payload }: StatusByTypeTooltipProps) {
  const rows = statusByTypeTooltipRows(payload)

  if (!active || !rows.length) {
    return null
  }

  return (
    <div className="min-w-56 max-w-80 rounded-xl border border-slate-700 bg-slate-900 px-3 py-2 text-sm text-slate-200 shadow-xl">
      <p className="font-bold text-slate-50">{String(label)}</p>
      <div className="mt-2 grid gap-1.5">
        {rows.map((row) => (
          <div className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-3" key={row.status}>
            <span className="flex min-w-0 items-center gap-2">
              <span aria-hidden="true" className="h-2.5 w-2.5 shrink-0 rounded-full" style={{ backgroundColor: row.color }} />
              <span className="truncate text-slate-200">{row.status}</span>
            </span>
            <span className="whitespace-nowrap font-semibold text-slate-50">
              {formatPercent(row.percent)} ({formatCount(row.count)} books)
            </span>
          </div>
        ))}
      </div>
    </div>
  )
}

function getChartPayload(entry: unknown): Record<string, unknown> {
  if (entry && typeof entry === 'object' && 'payload' in entry && entry.payload && typeof entry.payload === 'object') {
    return entry.payload as Record<string, unknown>
  }

  return {}
}

export function distributeStackedPercents(counts: number[], total: number, minimumPositivePercent = 3) {
  if (total <= 0) {
    return counts.map(() => 0)
  }

  const rawPercents = counts.map((count) => normalizedPercent(count, total))
  const positiveIndexes = rawPercents
    .map((value, index) => ({ value, index }))
    .filter((item) => item.value > 0)
  if (!positiveIndexes.length) {
    return rawPercents
  }

  const minimum = positiveIndexes.length * minimumPositivePercent >= 100
    ? 100 / positiveIndexes.length
    : minimumPositivePercent
  const small = positiveIndexes.filter((item) => item.value < minimum)
  const large = positiveIndexes.filter((item) => item.value >= minimum)
  const fixedSmallTotal = small.length * minimum
  const largeRawTotal = large.reduce((sum, item) => sum + item.value, 0)
  const remainingForLarge = Math.max(0, 100 - fixedSmallTotal)
  const result = rawPercents.map((value) => value > 0 ? value : 0)

  for (const item of small) {
    result[item.index] = minimum
  }

  if (large.length && largeRawTotal > 0) {
    for (const item of large) {
      result[item.index] = (item.value / largeRawTotal) * remainingForLarge
    }
  } else if (small.length) {
    const equal = 100 / small.length
    for (const item of small) {
      result[item.index] = equal
    }
  }

  return result
}

export function statusByTypeTooltipRows(payload: readonly StatusByTypeTooltipEntry[] | undefined) {
  return (payload ?? [])
    .map((entry) => {
      const status = String(entry.name ?? '')
      const count = finiteNumber(entry.payload?.[`${status}Count`])
      const percentValue = entry.payload?.[`${status}Percent`] ?? entry.value

      return {
        color: entry.color ?? '#94a3b8',
        count,
        percent: finiteNumber(percentValue),
        status,
      }
    })
    .filter((row) => row.status && row.count > 0)
    .sort((first, second) =>
      second.percent - first.percent
      || second.count - first.count
      || first.status.localeCompare(second.status),
    )
}

export function statusByTypeRows(data?: BookAnalyticsDto) {
  return (data?.composition.statusByType ?? []).flatMap((item) =>
    item.statuses.map((status) => [item.type, status.status, formatCount(status.bookCount), formatPercent(percent(status.bookCount, item.totalBooks))]),
  )
}

function finiteNumber(value: unknown) {
  const number = Number(value)
  return Number.isFinite(number) ? number : 0
}
