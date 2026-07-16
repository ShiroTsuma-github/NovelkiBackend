import type { BookAnalyticsDto } from '@/api/types'
import { DrilldownLink, fieldQuery, formatCount, formatPercent, noneQuery, percent } from './chartUtils'

type PriorityByStatusChartProps = {
  data: BookAnalyticsDto | undefined
}

export function PriorityByStatusChart({ data }: PriorityByStatusChartProps) {
  const rows = data?.planning.prioritiesByStatus ?? []
  const priorities = Array.from(new Set(rows.flatMap((row) => row.priorities.map((priority) => priority.priority))))

  if (!rows.length) {
    return <div className="grid min-h-56 place-items-center text-sm text-slate-500">No priority data for this analytics scope.</div>
  }

  return (
    <div className="overflow-x-auto">
      <table className="min-w-full border-separate border-spacing-2 text-sm" data-testid="priority-heatmap">
        <thead>
          <tr>
            <th className="px-2 py-1 text-left text-slate-500" scope="col">Status</th>
            {priorities.map((priority) => (
              <th className="px-2 py-1 text-center text-slate-500" key={priority} scope="col">{priority}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.status}>
              <th className="px-2 py-1 text-left" scope="row">
                <DrilldownLink query={fieldQuery('status', row.status)}>{row.status}</DrilldownLink>
              </th>
              {priorities.map((priority) => {
                const count = row.priorities.find((item) => item.priority === priority)?.bookCount ?? 0
                const share = percent(count, row.totalBooks)
                return (
                  <td className={`px-3 py-3 text-center font-semibold ${getHeatClass(share)}`} key={`${row.status}-${priority}`}>
                    <DrilldownLink
                      className="text-inherit decoration-current/70 hover:text-inherit hover:decoration-current"
                      query={`${fieldQuery('status', row.status)} ${priority.toLowerCase() === 'unset' ? noneQuery('priority') : `priority:${priority}`}`}
                    >
                      {formatCount(count)}
                    </DrilldownLink>
                    <div className="mt-1 text-xs font-semibold text-current/85">{formatPercent(share)}</div>
                  </td>
                )
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export function priorityRows(data?: BookAnalyticsDto) {
  return (data?.planning.prioritiesByStatus ?? []).flatMap((row) =>
    row.priorities.map((priority) => [
      row.status,
      priority.priority,
      formatCount(priority.bookCount),
      formatPercent(percent(priority.bookCount, row.totalBooks)),
    ]),
  )
}

function getHeatClass(share: number) {
  if (share >= 66) {
    return 'analytics-heat analytics-heat--high'
  }
  if (share >= 33) {
    return 'analytics-heat analytics-heat--medium'
  }
  if (share > 0) {
    return 'analytics-heat analytics-heat--low'
  }

  return 'analytics-heat analytics-heat--empty'
}
