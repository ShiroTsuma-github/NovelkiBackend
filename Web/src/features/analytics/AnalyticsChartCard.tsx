import { useId, useState, type ReactNode } from 'react'
import { buttonVariants, Surface } from '@/components/app/DesignSystem'

type AnalyticsChartCardProps = {
  title: string
  description: string
  isLoading?: boolean
  isError?: boolean
  isEmpty?: boolean
  emptyMessage?: string
  onRetry?: () => void
  children: ReactNode
  columns: string[]
  rows: Array<Array<ReactNode>>
  dataTableEnabled?: boolean
}

export function AnalyticsChartCard({
  title,
  description,
  isLoading = false,
  isError = false,
  isEmpty = false,
  emptyMessage = 'No analytics data for this card.',
  onRetry,
  children,
  columns,
  rows,
  dataTableEnabled = true,
}: AnalyticsChartCardProps) {
  const [showData, setShowData] = useState(false)
  const reactId = useId()
  const titleId = `${reactId}-title`
  const descriptionId = `${reactId}-description`
  const tableId = `${reactId}-data-table`

  return (
    <Surface
      aria-describedby={descriptionId}
      aria-labelledby={titleId}
      className="grid min-w-0 gap-4 p-4"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h2 className="ui-panel-title" id={titleId}>{title}</h2>
          <p className="ui-panel-description mt-1" id={descriptionId}>{description}</p>
        </div>
        {dataTableEnabled ? (
          <button
            aria-controls={tableId}
            aria-expanded={showData}
            aria-label={`${showData ? 'Hide' : 'View'} data for ${title}`}
            className={buttonVariants.ghost}
            type="button"
            onClick={() => setShowData((current) => !current)}
          >
            {showData ? 'Hide data' : 'View data'}
          </button>
        ) : null}
      </div>

      {isLoading ? (
        <Surface aria-label={`${title} loading`} as="div" className="grid h-64 animate-pulse gap-3 p-4" data-testid="analytics-card-skeleton" role="status" tone="muted">
          <div className="h-4 w-1/3 rounded bg-slate-200" />
          <div className="mt-auto grid gap-2">
            <div className="h-8 rounded bg-slate-200" />
            <div className="h-12 rounded bg-slate-200" />
            <div className="h-20 rounded bg-slate-200" />
          </div>
        </Surface>
      ) : isError ? (
        <Surface as="div" className="grid min-h-64 place-items-center p-4 text-center" role="alert" tone="danger">
          <div>
            <p className="text-sm font-semibold text-inherit">Could not load this analytics card.</p>
            {onRetry ? (
              <button className={`${buttonVariants.destructive} mt-3`} type="button" onClick={onRetry}>
                Retry
              </button>
            ) : null}
          </div>
        </Surface>
      ) : isEmpty ? (
        <Surface as="div" className="grid min-h-64 place-items-center p-4 text-center text-sm text-slate-500" tone="muted">
          {emptyMessage}
        </Surface>
      ) : (
        <Surface as="div" className="analytics-chart-surface min-h-64 min-w-0 p-4" tone="muted">
          {children}
        </Surface>
      )}

      {dataTableEnabled && showData ? (
        <Surface as="div" className="max-h-80 overflow-auto" id={tableId} tone="elevated">
          <table className="min-w-full divide-y divide-slate-800 text-sm">
            <caption className="sr-only">{title} data table</caption>
            <thead className="bg-slate-900 text-left text-xs font-semibold uppercase tracking-wide text-slate-300">
              <tr>
                {columns.map((column) => (
                  <th className="px-3 py-2" key={column} scope="col">{column}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800 bg-slate-950 text-slate-200">
              {rows.length ? rows.map((row, rowIndex) => (
                <tr className="hover:bg-slate-900/70" key={rowIndex}>
                  {row.map((cell, cellIndex) => (
                    <td className="px-3 py-2" key={cellIndex}>{cell}</td>
                  ))}
                </tr>
              )) : (
                <tr>
                  <td className="px-3 py-4 text-center text-slate-400" colSpan={columns.length}>No rows.</td>
                </tr>
              )}
            </tbody>
          </table>
        </Surface>
      ) : null}
    </Surface>
  )
}
