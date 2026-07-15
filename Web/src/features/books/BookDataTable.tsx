import type { ReactNode } from 'react'
import { ColumnHeader, type ColumnDefinition } from './bookListColumns'

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
  return (
    <div className={wrapperClassName}>
      <table className="w-full table-fixed border-collapse text-left text-sm">
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
            <th className="w-24 px-4 py-3 text-right">{actionHeader}</th>
          </tr>
        </thead>
        <tbody>
          {isLoading ? (
            <tr><td className="px-4 py-8 text-center text-slate-500" colSpan={columns.length + 1}>{loadingMessage}</td></tr>
          ) : null}
          {items.map((item) => (
            <tr className="border-t border-slate-100 hover:bg-slate-50" key={item.id}>
              {columns.map((column) => (
                <td className="px-4 py-3 text-slate-600" key={column.id}>
                  <div className="overflow-hidden">
                    {column.render(item)}
                  </div>
                </td>
              ))}
              <td className="w-24 px-4 py-3">
                {renderActions(item)}
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
