import { useId, useState, type ReactNode } from 'react'

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
    <section
      aria-describedby={descriptionId}
      aria-labelledby={titleId}
      className="grid min-w-0 gap-4 rounded-xl border border-slate-200 bg-white p-4 shadow-sm"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h2 className="text-base font-semibold text-slate-950" id={titleId}>{title}</h2>
          <p className="mt-1 text-sm text-slate-600" id={descriptionId}>{description}</p>
        </div>
        {dataTableEnabled ? (
          <button
            aria-controls={tableId}
            aria-expanded={showData}
            aria-label={`${showData ? 'Hide' : 'View'} data for ${title}`}
            className="inline-flex min-h-11 items-center justify-center rounded-md border border-slate-500 bg-white px-4 py-2 text-sm font-semibold text-slate-950 transition hover:border-slate-700 hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyan-500 focus-visible:ring-offset-2"
            type="button"
            onClick={() => setShowData((current) => !current)}
          >
            {showData ? 'Hide data' : 'View data'}
          </button>
        ) : null}
      </div>

      {isLoading ? (
        <div aria-label={`${title} loading`} className="grid h-64 animate-pulse gap-3 rounded-lg border border-slate-200 bg-slate-50 p-4" data-testid="analytics-card-skeleton" role="status">
          <div className="h-4 w-1/3 rounded bg-slate-200" />
          <div className="mt-auto grid gap-2">
            <div className="h-8 rounded bg-slate-200" />
            <div className="h-12 rounded bg-slate-200" />
            <div className="h-20 rounded bg-slate-200" />
          </div>
        </div>
      ) : isError ? (
        <div className="grid min-h-64 place-items-center rounded-lg border border-rose-200 bg-rose-50 p-4 text-center" role="alert">
          <div>
            <p className="text-sm font-semibold text-rose-950">Could not load this analytics card.</p>
            {onRetry ? (
              <button className="mt-3 inline-flex min-h-11 items-center rounded-md bg-rose-700 px-4 py-2 text-sm font-semibold text-white hover:bg-rose-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-rose-500 focus-visible:ring-offset-2" type="button" onClick={onRetry}>
                Retry
              </button>
            ) : null}
          </div>
        </div>
      ) : isEmpty ? (
        <div className="grid min-h-64 place-items-center rounded-lg border border-slate-200 bg-slate-50 p-4 text-center text-sm text-slate-500">
          {emptyMessage}
        </div>
      ) : (
        <div className="min-h-64 min-w-0 rounded-lg border border-slate-200 bg-slate-50 p-4">
          {children}
        </div>
      )}

      {dataTableEnabled && showData ? (
        <div className="max-h-80 overflow-auto rounded-lg border border-slate-700 bg-slate-950" id={tableId}>
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
        </div>
      ) : null}
    </section>
  )
}
