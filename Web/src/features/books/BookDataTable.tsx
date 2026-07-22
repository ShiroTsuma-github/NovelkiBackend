import type { ReactNode } from 'react'
import { ColumnHeader, type ColumnDefinition } from './bookListColumns'

const minimumTableWidthRem = 76
const fallbackColumnWidthRem = 9
const actionsColumnWidthRem = 8

export function BookDataTable<T extends { id: string }>({
  actionHeader = 'Actions',
  columns,
  emptyMessage,
  isCyclicSort = () => false,
  isLoading,
  items,
  loadingMessage = 'Loading...',
  renderActions,
  sortBy,
  sortDirection,
  wrapperClassName = 'w-full',
  onSort,
}: {
  actionHeader?: string
  columns: ColumnDefinition<T>[]
  emptyMessage: string
  isCyclicSort?: (sortBy: string) => boolean
  isLoading: boolean
  items: T[]
  loadingMessage?: string
  renderActions: (item: T) => ReactNode
  sortBy: string | null
  sortDirection: string | null
  wrapperClassName?: string
  onSort: (sortBy: string) => void
}) {
  const tableMinWidthRem = getBookTableMinWidthRem(columns)

  return (
    <div className={`${wrapperClassName} max-w-full overflow-x-auto`}>
      <table className="w-full table-fixed border-collapse text-left text-sm" style={{ minWidth: `${tableMinWidthRem}rem` }}>
        <colgroup>
          {columns.map((column) => <col className={column.widthClass ?? ''} key={column.id} />)}
          <col className="w-32" />
        </colgroup>
        <thead className="bg-slate-100 text-xs uppercase tracking-wide text-slate-500">
          <tr>
            {columns.map((column) => (
              <ColumnHeader
                activeDirection={column.sortBy === sortBy ? sortDirection : null}
                column={column}
                isCyclic={column.sortBy ? isCyclicSort(column.sortBy) : false}
                key={column.id}
                onSort={onSort}
              />
            ))}
            <th className="sticky right-0 z-10 border-l border-slate-200 bg-slate-100 px-3 py-3 text-right">{actionHeader}</th>
          </tr>
        </thead>
        <tbody>
          {isLoading ? (
            <tr><td className="px-4 py-8 text-center text-slate-500" colSpan={columns.length + 1}>{loadingMessage}</td></tr>
          ) : null}
          {items.map((item) => (
            <tr className="book-table-row group border-t border-slate-100" data-testid={`book-table-row-${item.id}`} key={item.id}>
              {columns.map((column) => {
                const content = column.render(item)
                const title = typeof content === 'string' || typeof content === 'number'
                  ? String(content)
                  : undefined

                return (
                  <td className="book-table-cell px-3 py-3 text-slate-600" data-testid={`book-table-cell-${item.id}-${column.id}`} key={column.id}>
                    <div className="overflow-hidden" title={title}>
                      {content}
                    </div>
                  </td>
                )
              })}
              <td className="book-table-actions-cell sticky right-0 z-10 border-l border-slate-200 bg-white px-3 py-3" data-testid={`book-table-actions-cell-${item.id}`}>
                <div className="flex justify-end gap-2 whitespace-nowrap">
                  {renderActions(item)}
                </div>
              </td>
            </tr>
          ))}
          {items.length === 0 ? (
            <tr><td className="px-4 py-8 text-center text-slate-500" colSpan={columns.length + 1}>{emptyMessage}</td></tr>
          ) : null}
        </tbody>
      </table>
    </div>
  )
}

export function getBookTableMinWidthRem(columns: Array<{ widthClass?: string }>) {
  const columnWidth = columns.reduce((sum, column) => sum + getTailwindWidthRem(column.widthClass), actionsColumnWidthRem)
  return Math.max(minimumTableWidthRem, columnWidth)
}

function getTailwindWidthRem(widthClass?: string) {
  if (!widthClass) {
    return fallbackColumnWidthRem
  }

  const spacingMatch = /^w-(\d+)$/.exec(widthClass)
  if (spacingMatch) {
    return Number(spacingMatch[1]) / 4
  }

  const remMatch = /^w-\[(\d+(?:\.\d+)?)rem\]$/.exec(widthClass)
  if (remMatch) {
    return Number(remMatch[1])
  }

  return fallbackColumnWidthRem
}
