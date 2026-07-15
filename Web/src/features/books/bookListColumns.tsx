import { ArrowDown, ArrowUp, ChevronsUpDown, RefreshCw, Settings2 } from 'lucide-react'
import { useEffect, useRef, useState, type ReactNode } from 'react'

const columnPopupWidthPx = 320
const columnPopupEdgeGapPx = 16
const columnPopupVerticalGapPx = 10

export type BookViewMode = 'table' | 'cards'
export type ColumnPreference = { id: string; visible: boolean }
export type ColumnDefinition<T> = {
  id: string
  label: ReactNode
  defaultVisible: boolean
  sortBy?: string
  widthClass?: string
  render: (item: T) => ReactNode
}

export function totalChaptersColumnLabel() {
  return (
    <>
      <span className="sm:hidden">Total</span>
      <span className="hidden sm:inline">Total chapters</span>
    </>
  )
}

export function ColumnSettingsPopup<T>({
  allowReorder = true,
  columns,
  description = 'Change table order and visibility.',
  preferences,
  title = 'Visible columns',
  onChange,
}: {
  allowReorder?: boolean
  columns: ColumnDefinition<T>[]
  description?: string
  preferences: ColumnPreference[]
  title?: string
  onChange: (next: ColumnPreference[]) => void
}) {
  const [open, setOpen] = useState(false)
  const [position, setPosition] = useState({ left: 'auto', top: 'auto' })
  const buttonRef = useRef<HTMLButtonElement>(null)
  const containerRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) {
      return
    }

    function handlePointerDown(event: PointerEvent) {
      if (!containerRef.current?.contains(event.target as Node)) {
        setOpen(false)
      }
    }

    document.addEventListener('pointerdown', handlePointerDown)
    return () => document.removeEventListener('pointerdown', handlePointerDown)
  }, [open])

  useEffect(() => {
    if (!open) {
      return
    }

    function updatePosition() {
      const rect = buttonRef.current?.getBoundingClientRect()
      if (!rect) {
        return
      }

      setPosition(getColumnPopupPosition(rect, window.innerWidth, window.innerHeight))
    }

    updatePosition()
    window.addEventListener('resize', updatePosition)
    window.addEventListener('scroll', updatePosition, true)
    return () => {
      window.removeEventListener('resize', updatePosition)
      window.removeEventListener('scroll', updatePosition, true)
    }
  }, [open])

  function toggleColumn(id: string) {
    onChange(preferences.map((preference) => (
      preference.id === id ? { ...preference, visible: !preference.visible } : preference
    )))
  }

  function moveColumn(id: string, direction: -1 | 1) {
    const index = preferences.findIndex((preference) => preference.id === id)
    const nextIndex = index + direction
    if (index < 0 || nextIndex < 0 || nextIndex >= preferences.length) {
      return
    }

    const next = [...preferences]
    const [item] = next.splice(index, 1)
    next.splice(nextIndex, 0, item)
    onChange(next)
  }

  function resetColumns() {
    onChange(defaultColumnPreferences(columns))
  }

  return (
    <div className="inline-block text-left" ref={containerRef}>
      <button
        ref={buttonRef}
        className="inline-flex h-10 items-center gap-2 rounded-md border border-slate-300 bg-white px-3 text-xs font-semibold uppercase tracking-wide text-slate-600 shadow-sm transition hover:border-slate-400 hover:bg-slate-50 hover:text-slate-950"
        type="button"
        onClick={() => setOpen(!open)}
      >
        <Settings2 className="h-4 w-4" />
        Columns
      </button>
      {open ? (
        <div
          className="fixed z-50 grid max-h-[min(34rem,calc(100vh-1rem))] w-[min(20rem,calc(100vw-1rem))] gap-3 overflow-y-auto rounded-lg border border-slate-200 bg-white p-4 text-left normal-case tracking-normal shadow-xl"
          style={position}
        >
          <div className="flex items-start justify-between gap-3 border-b border-slate-100 pb-3">
            <div>
              <div className="text-sm font-semibold text-slate-950">{title}</div>
              <div className="text-xs font-normal text-slate-500">{description}</div>
            </div>
            <button className="text-xs font-semibold text-slate-500 hover:text-slate-950" type="button" onClick={resetColumns}>Reset</button>
          </div>
          {preferences.map((preference, index) => {
            const column = columns.find((item) => item.id === preference.id)
            if (!column) {
              return null
            }

            return (
              <div className={`flex items-center justify-between gap-3 rounded-md border bg-white px-3.5 py-2.5 text-sm text-slate-700 ${preference.visible ? 'border-emerald-400' : 'border-slate-200'}`} key={preference.id}>
                <button
                  aria-pressed={preference.visible}
                  className="inline-flex min-w-0 flex-1 items-center gap-2 text-left font-medium text-inherit"
                  type="button"
                  onClick={() => toggleColumn(preference.id)}
                >
                  <span className={`relative inline-flex h-5 w-9 shrink-0 rounded-full border transition ${preference.visible ? 'border-cyan-500 bg-cyan-500' : 'border-slate-300 bg-slate-200'}`}>
                    <span className={`absolute top-0.5 h-3.5 w-3.5 rounded-full bg-white shadow transition ${preference.visible ? 'left-4' : 'left-0.5'}`} />
                  </span>
                  {column.label}
                </button>
                {allowReorder ? (
                  <div className="flex shrink-0 gap-1">
                    <button className="h-8 rounded border border-slate-200 px-2 text-xs font-semibold text-slate-600 hover:bg-slate-50 disabled:text-slate-300" disabled={index === 0} type="button" onClick={() => moveColumn(preference.id, -1)}>Up</button>
                    <button className="h-8 rounded border border-slate-200 px-2 text-xs font-semibold text-slate-600 hover:bg-slate-50 disabled:text-slate-300" disabled={index === preferences.length - 1} type="button" onClick={() => moveColumn(preference.id, 1)}>Down</button>
                  </div>
                ) : null}
              </div>
            )
          })}
        </div>
      ) : null}
    </div>
  )
}

export function useColumnPreferences<T>(storageKey: string, columns: ColumnDefinition<T>[]) {
  const [preferences, setPreferences] = useState<ColumnPreference[]>(() => readColumnPreferences(storageKey, columns))

  function updatePreferences(next: ColumnPreference[]) {
    setPreferences(next)
    window.localStorage.setItem(storageKey, JSON.stringify(next))
  }

  return [preferences, updatePreferences] as const
}

export function getColumnPopupPosition(
  rect: Pick<DOMRect, 'left' | 'right' | 'bottom'>,
  viewportWidth: number,
  viewportHeight: number,
) {
  const maxWidth = Math.min(columnPopupWidthPx, viewportWidth - (columnPopupEdgeGapPx * 2))
  const left = viewportWidth < 640
    ? columnPopupEdgeGapPx
    : Math.min(
      Math.max(columnPopupEdgeGapPx, rect.right - maxWidth),
      viewportWidth - maxWidth - columnPopupEdgeGapPx,
    )
  const top = Math.min(
    Math.max(columnPopupEdgeGapPx, rect.bottom + columnPopupVerticalGapPx),
    viewportHeight - columnPopupEdgeGapPx,
  )

  return {
    left: `${left}px`,
    top: `${top}px`,
  }
}

export function useViewMode(storageKey: string) {
  const [viewMode, setViewModeState] = useState<BookViewMode>(() => {
    const stored = window.localStorage.getItem(storageKey)
    return stored === 'cards' ? 'cards' : 'table'
  })

  function setViewMode(next: BookViewMode) {
    setViewModeState(next)
    window.localStorage.setItem(storageKey, next)
  }

  return [viewMode, setViewMode] as const
}

export function getVisibleColumns<T>(columns: ColumnDefinition<T>[], preferences: ColumnPreference[]) {
  return preferences
    .filter((preference) => preference.visible)
    .map((preference) => columns.find((column) => column.id === preference.id))
    .filter((column): column is ColumnDefinition<T> => Boolean(column))
}

export function defaultColumnPreferences<T>(columns: ColumnDefinition<T>[]): ColumnPreference[] {
  return columns.map((column) => ({ id: column.id, visible: column.defaultVisible }))
}

export function ColumnHeader<T>({
  activeDirection,
  isCyclic = false,
  column,
  onSort,
}: {
  activeDirection: string | null
  isCyclic?: boolean
  column: ColumnDefinition<T>
  onSort: (sortBy: string) => void
}) {
  const Icon = isCyclic && activeDirection
    ? RefreshCw
    : activeDirection === 'asc'
      ? ArrowUp
      : activeDirection === 'desc'
        ? ArrowDown
        : ChevronsUpDown

  if (!column.sortBy) {
    return <th className={`${column.widthClass ?? ''} px-4 py-3`}>{column.label}</th>
  }

  return (
    <th className={`${column.widthClass ?? ''} px-4 py-3`}>
      <button
        className="inline-flex h-8 items-center gap-1 rounded-md px-2 text-xs font-semibold uppercase tracking-wide text-slate-500 hover:bg-slate-200 hover:text-slate-950"
        type="button"
        onClick={() => onSort(column.sortBy!)}
      >
        {column.label}
        <Icon className="h-3.5 w-3.5" />
      </button>
    </th>
  )
}

function readColumnPreferences<T>(storageKey: string, columns: ColumnDefinition<T>[]) {
  const defaults = defaultColumnPreferences(columns)
  const stored = window.localStorage.getItem(storageKey)
  if (!stored) {
    return defaults
  }

  try {
    const parsed = JSON.parse(stored) as ColumnPreference[]
    const knownIds = new Set(columns.map((column) => column.id))
    const storedKnown = parsed.filter((preference) => knownIds.has(preference.id))
    const missing = defaults.filter((preference) => !storedKnown.some((item) => item.id === preference.id))
    return [...storedKnown, ...missing]
  } catch {
    return defaults
  }
}
