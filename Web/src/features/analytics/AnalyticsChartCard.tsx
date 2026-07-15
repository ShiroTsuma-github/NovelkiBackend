import { useState, type ReactNode } from 'react'

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
}: AnalyticsChartCardProps) {
  const [showData, setShowData] = useState(false)

  return (
    <section className="grid min-w-0 gap-4 rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h2 className="text-base font-semibold text-slate-950">{title}</h2>
          <p className="mt-1 text-sm text-slate-500">{description}</p>
        </div>
        <button
          className="inline-flex min-h-10 items-center justify-center rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-semibold text-slate-700 transition hover:border-slate-400 hover:text-slate-950"
          type="button"
          onClick={() => setShowData((current) => !current)}
        >
          View data
        </button>
      </div>

      {isLoading ? (
        <div className="grid h-64 animate-pulse gap-3 rounded-lg border border-slate-200 bg-slate-50 p-4" data-testid="analytics-card-skeleton">
          <div className="h-4 w-1/3 rounded bg-slate-200" />
          <div className="mt-auto grid gap-2">
            <div className="h-8 rounded bg-slate-200" />
            <div className="h-12 rounded bg-slate-200" />
            <div className="h-20 rounded bg-slate-200" />
          </div>
        </div>
      ) : isError ? (
        <div className="grid min-h-64 place-items-center rounded-lg border border-rose-200 bg-rose-50 p-4 text-center">
          <div>
            <p className="text-sm font-semibold text-rose-950">Could not load this analytics card.</p>
            {onRetry ? (
              <button className="mt-3 rounded-md bg-rose-700 px-3 py-2 text-sm font-semibold text-white hover:bg-rose-800" type="button" onClick={onRetry}>
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

      {showData ? (
        <div className="overflow-x-auto rounded-lg border border-slate-200">
          <table className="min-w-full divide-y divide-slate-200 text-sm">
            <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
              <tr>
                {columns.map((column) => (
                  <th className="px-3 py-2" key={column} scope="col">{column}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200 bg-white text-slate-700">
              {rows.length ? rows.map((row, rowIndex) => (
                <tr key={rowIndex}>
                  {row.map((cell, cellIndex) => (
                    <td className="px-3 py-2" key={cellIndex}>{cell}</td>
                  ))}
                </tr>
              )) : (
                <tr>
                  <td className="px-3 py-4 text-center text-slate-500" colSpan={columns.length}>No rows.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      ) : null}
    </section>
  )
}
