import { useQuery } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, ChevronsUpDown, Edit, Eye, LayoutGrid, List, Plus, Search, Settings2, Upload } from 'lucide-react'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { api } from '@/api/client'
import type { BookDto, BookImportFinalizeResult } from '@/api/types'
import {
  buttonClass,
  inputClass,
  secondaryButtonClass,
} from '@/components/app/FormField'
import { BookCoverArtwork } from './BookCoverSection'
import { ImportBooksDialog } from './ImportBooksDialog'

const pageSizeOptions = [20, 50, 100, 500]
const bookColumnsStorageKey = 'novelki.books.columns.v1'
const bookLayoutStorageKey = 'novelki.books.layout.v1'
type SortDirection = 'asc' | 'desc'
export type BookViewMode = 'table' | 'cards'
export type ColumnPreference = { id: string; visible: boolean }
export type ColumnDefinition<T> = {
  id: string
  label: string
  defaultVisible: boolean
  sortBy?: string
  widthClass?: string
  render: (item: T) => ReactNode
}

const bookColumns: ColumnDefinition<BookDto>[] = [
  { id: 'id', label: 'Id', defaultVisible: false, widthClass: 'w-36', render: (book) => <span className="font-mono text-xs">{book.id}</span> },
  { id: 'title', label: 'Title', defaultVisible: true, sortBy: 'title', widthClass: 'w-[26%]', render: (book) => <span className="block truncate font-medium text-slate-950">{book.primaryTitle}</span> },
  { id: 'alternativeTitles', label: 'Alternative titles', defaultVisible: false, widthClass: 'w-[18%]', render: (book) => formatList(book.alternativeTitles) },
  { id: 'author', label: 'Author', defaultVisible: true, sortBy: 'author', widthClass: 'w-[13%]', render: (book) => <span className="block truncate">{book.author ?? '-'}</span> },
  { id: 'status', label: 'Status', defaultVisible: true, sortBy: 'status', widthClass: 'w-20', render: (book) => book.status },
  { id: 'type', label: 'Type', defaultVisible: true, sortBy: 'type', widthClass: 'w-20', render: (book) => book.contentType },
  { id: 'progress', label: 'Progress', defaultVisible: true, sortBy: 'progress', widthClass: 'w-24', render: formatProgress },
  { id: 'totalChapters', label: 'Chapters', defaultVisible: false, widthClass: 'w-24', render: (book) => book.totalChapters ?? '-' },
  { id: 'rating', label: 'Rating', defaultVisible: true, sortBy: 'rating', widthClass: 'w-16', render: (book) => book.rating ?? '-' },
  { id: 'priority', label: 'Priority', defaultVisible: false, sortBy: 'priority', widthClass: 'w-20', render: (book) => book.priority ?? '-' },
  { id: 'created', label: 'Created', defaultVisible: false, sortBy: 'created', widthClass: 'w-36', render: (book) => formatDate(book.created) },
  { id: 'lastModified', label: 'Updated', defaultVisible: true, sortBy: 'lastModified', widthClass: 'w-32', render: (book) => formatDate(book.lastModified || book.created) },
  { id: 'genres', label: 'Genres', defaultVisible: false, widthClass: 'w-[10%]', render: (book) => <Pills values={book.genres} /> },
  { id: 'tags', label: 'Tags', defaultVisible: true, widthClass: 'w-[10%]', render: (book) => <Pills values={book.tags} /> },
  { id: 'links', label: 'Links', defaultVisible: false, widthClass: 'w-16', render: (book) => book.links.length },
  { id: 'notes', label: 'Notes', defaultVisible: false, widthClass: 'w-[18%]', render: (book) => truncate(book.notes) },
  { id: 'description', label: 'Description', defaultVisible: false, widthClass: 'w-[18%]', render: (book) => truncate(book.description) },
]

export function BooksPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const [columnPreferences, setColumnPreferences] = useColumnPreferences(bookColumnsStorageKey, bookColumns)
  const [viewMode, setViewMode] = useViewMode(bookLayoutStorageKey)
  const [lastImportResult, setLastImportResult] = useState<BookImportFinalizeResult | null>(null)
  const [importDialogOpen, setImportDialogOpen] = useState(false)
  const skip = Number(searchParams.get('skip') ?? 0)
  const pageSize = readPageSize(searchParams)
  const sortBy = searchParams.get('sortBy') ?? 'lastModified'
  const sortDirection = readSortDirection(searchParams)
  const query = searchParams.get('query') ?? ''
  const visibleColumns = getVisibleColumns(bookColumns, columnPreferences)
  const booksQuery = useQuery({
    queryKey: ['books', skip, pageSize, query, sortBy, sortDirection],
    queryFn: () => api.getBooks({ skip, take: pageSize, query, sortBy, sortDirection }),
  })
  function updateQuery(value: string) {
    const next = new URLSearchParams(searchParams)
    if (value) {
      next.set('query', value)
    } else {
      next.delete('query')
    }
    next.delete('skip')
    setSearchParams(next)
  }

  function setSkip(nextSkip: number) {
    const next = new URLSearchParams(searchParams)
    next.set('skip', String(Math.max(0, nextSkip)))
    setSearchParams(next)
  }

  function setPageSize(nextPageSize: string) {
    const next = new URLSearchParams(searchParams)
    next.set('take', nextPageSize)
    next.delete('skip')
    setSearchParams(next)
  }

  function setSort(nextSortBy: string) {
    const next = new URLSearchParams(searchParams)
    const defaultDirection = nextSortBy === 'lastModified' || nextSortBy === 'created' ? 'desc' : 'asc'
    const nextDirection = sortBy === nextSortBy
      ? sortDirection === 'asc' ? 'desc' : 'asc'
      : defaultDirection
    next.set('sortBy', nextSortBy)
    next.set('sortDirection', nextDirection)
    next.delete('skip')
    setSearchParams(next)
  }

  const total = booksQuery.data?.total ?? 0
  const canGoBack = skip > 0
  const canGoForward = skip + pageSize < total
  
  function handleImportComplete(result: BookImportFinalizeResult) {
    setLastImportResult(result)
    booksQuery.refetch()
    toast.success(`Imported ${result.importedCount} books. Skipped ${result.skippedCount}.`)
  }

  return (
    <div className="grid gap-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-slate-950">Books</h1>
          <p className="text-sm text-slate-500">List, search, and quick navigation through your library.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button className={secondaryButtonClass} type="button" onClick={() => setImportDialogOpen(true)}>
            <Upload className="h-4 w-4" />
            Import CSV
          </button>
          <Link className={buttonClass} to="/books/new">
            <Plus className="h-4 w-4" />
            Add book
          </Link>
        </div>
      </div>

      {lastImportResult ? (
        <section className="grid gap-2 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <div className="text-sm font-semibold text-slate-950">Last import</div>
          <p className="text-sm text-slate-600">
            Imported: {lastImportResult.importedCount}. Skipped: {lastImportResult.skippedCount}.
          </p>
          {lastImportResult.errors.length ? (
            <div className="grid gap-1 rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
              {lastImportResult.errors.slice(0, 10).map((error) => (
                <p key={error}>{error}</p>
              ))}
              {lastImportResult.errors.length > 10 ? (
                <p>More errors: {lastImportResult.errors.length - 10}</p>
              ) : null}
            </div>
          ) : null}
        </section>
      ) : null}

      <BookAdvancedSearch value={query} onChange={updateQuery} />

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
        <div className="flex flex-wrap items-center justify-end gap-2 border-b border-slate-200 px-4 py-3">
          <ViewModeToggle value={viewMode} onChange={setViewMode} />
          <ColumnSettingsPopup columns={bookColumns} preferences={columnPreferences} onChange={setColumnPreferences} />
        </div>
        {viewMode === 'table' ? (
          <div className="w-full">
            <table className="w-full table-fixed border-collapse text-left text-sm">
              <thead className="bg-slate-100 text-xs uppercase tracking-wide text-slate-500">
                <tr>
                  {visibleColumns.map((column) => (
                    <ColumnHeader
                      activeDirection={column.sortBy === sortBy ? sortDirection : null}
                      column={column}
                      key={column.id}
                      onSort={setSort}
                    />
                  ))}
                  <th className="w-24 px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {booksQuery.isLoading ? (
                  <tr><td className="px-4 py-8 text-center text-slate-500" colSpan={visibleColumns.length + 1}>Loading...</td></tr>
                ) : null}
                {booksQuery.data?.data.map((book) => (
                  <BookRow book={book} columns={visibleColumns} key={book.id} />
                ))}
                {booksQuery.data?.data.length === 0 ? (
                  <tr><td className="px-4 py-8 text-center text-slate-500" colSpan={visibleColumns.length + 1}>No books match the current filters.</td></tr>
                ) : null}
              </tbody>
            </table>
          </div>
        ) : (
          <BookCardGrid books={booksQuery.data?.data ?? []} isLoading={booksQuery.isLoading} />
        )}
        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 px-4 py-3 text-sm text-slate-600">
          <span>{total ? `${skip + 1}-${Math.min(skip + pageSize, total)} of ${total}` : '0 results'}</span>
          <div className="flex items-center gap-3">
            <label className="flex items-center gap-2">
              <span>Per page</span>
              <select className={`${inputClass} h-10 w-24`} value={pageSize} onChange={(event) => setPageSize(event.target.value)}>
                {pageSizeOptions.map((option) => <option key={option} value={option}>{option}</option>)}
              </select>
            </label>
            <button className={secondaryButtonClass} disabled={!canGoBack} type="button" onClick={() => setSkip(skip - pageSize)}>Previous</button>
            <button className={secondaryButtonClass} disabled={!canGoForward} type="button" onClick={() => setSkip(skip + pageSize)}>Next</button>
          </div>
        </div>
      </section>
      <ImportBooksDialog open={importDialogOpen} onClose={() => setImportDialogOpen(false)} onImported={handleImportComplete} />
    </div>
  )
}

export function ViewModeToggle({
  value,
  onChange,
}: {
  value: BookViewMode
  onChange: (value: BookViewMode) => void
}) {
  return (
    <div className="inline-flex items-center rounded-md border border-slate-300 bg-white p-1 shadow-sm">
      <button
        className={`inline-flex h-8 items-center gap-1 rounded-md px-2 text-xs font-semibold uppercase tracking-wide ${value === 'table' ? 'bg-slate-900 text-white' : 'text-slate-600 hover:bg-slate-100 hover:text-slate-950'}`}
        type="button"
        onClick={() => onChange('table')}
      >
        <List className="h-3.5 w-3.5" />
        Table
      </button>
      <button
        className={`inline-flex h-8 items-center gap-1 rounded-md px-2 text-xs font-semibold uppercase tracking-wide ${value === 'cards' ? 'bg-slate-900 text-white' : 'text-slate-600 hover:bg-slate-100 hover:text-slate-950'}`}
        type="button"
        onClick={() => onChange('cards')}
      >
        <LayoutGrid className="h-3.5 w-3.5" />
        Cards
      </button>
    </div>
  )
}

export function BookAdvancedSearch({
  value,
  onChange,
}: {
  value: string
  onChange: (value: string) => void
}) {
  return (
    <section className="grid gap-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
      <label className="relative">
        <Search className="pointer-events-none absolute left-3 top-3 h-4 w-4 text-slate-400" />
        <input
          className={`${inputClass} w-full pl-9`}
          placeholder={'Search: returnee author:Toika title:"Lord of Mysteries" genre:fantasy,"slice of life" rating>=8'}
          value={value}
          onChange={(event) => onChange(event.target.value)}
        />
      </label>
      <p className="text-xs text-slate-500">
        Supports filters like <code>author:John</code>, <code>tag:favorite,"to read soon"</code>, <code>genre:fantasy,"slice of life"</code>, <code>rating&gt;=8</code>, and wildcard searches like <code>title:i*</code>.
      </p>
    </section>
  )
}

export function ColumnSettingsPopup<T>({
  columns,
  preferences,
  onChange,
}: {
  columns: ColumnDefinition<T>[]
  preferences: ColumnPreference[]
  onChange: (preferences: ColumnPreference[]) => void
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

      const panelWidthVw = 20
      const horizontalGapVw = 1.25
      const verticalLiftVh = 1.25
      const edgeGapVw = 1
      const edgeGapVh = 1
      const preferredLeftVw = (rect.right / window.innerWidth) * 100 + horizontalGapVw
      const preferredTopVh = (rect.top / window.innerHeight) * 100 - verticalLiftVh
      setPosition({
        left: `clamp(${edgeGapVw}vw, ${preferredLeftVw}vw, ${100 - panelWidthVw - edgeGapVw}vw)`,
        top: `clamp(${edgeGapVh}vh, ${preferredTopVh}vh, calc(100vh - ${edgeGapVh}vh))`,
      })
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
        className="inline-flex h-8 items-center gap-1 rounded-md border border-slate-300 bg-white px-2 text-xs font-semibold uppercase tracking-wide text-slate-600 shadow-sm transition hover:border-slate-400 hover:bg-slate-50 hover:text-slate-950"
        type="button"
        onClick={() => setOpen(!open)}
      >
        <Settings2 className="h-3.5 w-3.5" />
        Columns
      </button>
      {open ? (
        <div
          className="fixed z-50 grid max-h-[min(34rem,calc(100vh-1rem))] w-80 gap-2 overflow-y-auto rounded-lg border border-slate-200 bg-white p-3 text-left normal-case tracking-normal shadow-xl"
          style={position}
        >
          <div className="flex items-center justify-between gap-3 border-b border-slate-100 pb-2">
            <div>
              <div className="text-sm font-semibold text-slate-950">Visible columns</div>
              <div className="text-xs font-normal text-slate-500">Change table order and visibility.</div>
            </div>
            <button className="text-xs font-semibold text-slate-500 hover:text-slate-950" type="button" onClick={resetColumns}>Reset</button>
          </div>
          {preferences.map((preference, index) => {
            const column = columns.find((item) => item.id === preference.id)
            if (!column) {
              return null
            }

            return (
              <div className={`flex items-center justify-between gap-3 rounded-md border bg-white px-3 py-2 text-sm text-slate-700 ${preference.visible ? 'border-emerald-400' : 'border-slate-200'}`} key={preference.id}>
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
                <div className="flex shrink-0 gap-1">
                  <button className="h-8 rounded border border-slate-200 px-2 text-xs font-semibold text-slate-600 hover:bg-slate-50 disabled:text-slate-300" disabled={index === 0} type="button" onClick={() => moveColumn(preference.id, -1)}>Up</button>
                  <button className="h-8 rounded border border-slate-200 px-2 text-xs font-semibold text-slate-600 hover:bg-slate-50 disabled:text-slate-300" disabled={index === preferences.length - 1} type="button" onClick={() => moveColumn(preference.id, 1)}>Down</button>
                </div>
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

export function ColumnHeader<T>({
  activeDirection,
  column,
  onSort,
}: {
  activeDirection: SortDirection | null
  column: ColumnDefinition<T>
  onSort: (sortBy: string) => void
}) {
  const Icon = activeDirection === 'asc' ? ArrowUp : activeDirection === 'desc' ? ArrowDown : ChevronsUpDown

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

function BookRow({ book, columns }: { book: BookDto; columns: ColumnDefinition<BookDto>[] }) {
  return (
    <tr className="border-t border-slate-100 hover:bg-slate-50">
      {columns.map((column) => (
        <td className="px-4 py-3 text-slate-600" key={column.id}>
          <div className="overflow-hidden">
            {column.render(book)}
          </div>
        </td>
      ))}
      <td className="w-24 px-4 py-3">
        <div className="flex justify-end gap-2">
          <Link className={secondaryButtonClass} to={`/books/${book.id}`}><Eye className="h-4 w-4" /></Link>
          <Link className={secondaryButtonClass} to={`/books/${book.id}/edit`}><Edit className="h-4 w-4" /></Link>
        </div>
      </td>
    </tr>
  )
}

function BookCardGrid({ books, isLoading }: { books: BookDto[]; isLoading: boolean }) {
  if (isLoading) {
    return <div className="px-4 py-8 text-center text-slate-500">Loading...</div>
  }

  if (!books.length) {
    return <div className="px-4 py-8 text-center text-slate-500">No books match the current filters.</div>
  }

  return (
    <div className="grid gap-4 p-4 sm:grid-cols-2 xl:grid-cols-4">
      {books.map((book) => (
        <article className="rounded-2xl border border-slate-200 bg-slate-50 p-3 shadow-sm" key={book.id}>
          <Link className="grid gap-3" to={`/books/${book.id}`}>
            <BookCoverArtwork
              className="w-full"
              cover={book.cover}
              emptyLabel="No cover"
              title={book.primaryTitle}
            />
            <div className="grid gap-1">
              <h2 className="line-clamp-2 text-base font-semibold text-slate-950">{book.primaryTitle}</h2>
              <p className="text-sm text-slate-500">{book.author ?? 'Unknown author'}</p>
              <p className="text-sm font-medium text-slate-700">{formatProgress(book)}</p>
            </div>
          </Link>
        </article>
      ))}
    </div>
  )
}

function Pills({ values }: { values: string[] }) {
  if (!values.length) {
    return '-'
  }

  const visibleValues = values.slice(0, 3)
  const hiddenCount = values.length - visibleValues.length

  return (
    <div className="flex flex-wrap gap-1">
      {visibleValues.map((value) => (
        <span className="rounded bg-slate-100 px-2 py-1 text-xs text-slate-600" key={value}>{value}</span>
      ))}
      {hiddenCount > 0 ? (
        <span className="rounded bg-slate-100 px-2 py-1 text-xs font-medium text-slate-500">+{hiddenCount} more</span>
      ) : null}
    </div>
  )
}

function readPageSize(searchParams: URLSearchParams) {
  const value = Number(searchParams.get('take') ?? 20)
  return pageSizeOptions.includes(value) ? value : 20
}

function readSortDirection(searchParams: URLSearchParams): SortDirection {
  return searchParams.get('sortDirection') === 'asc' ? 'asc' : 'desc'
}

export function formatProgress(book: BookDto) {
  const isCompleted = book.status.trim().toLowerCase() === 'completed'
  const current = isCompleted && book.currentChapterNumber != null
    ? book.currentChapterNumber
    : book.currentChapterLabel || book.currentChapterNumber
  if (!current && !book.totalChapters) {
    return '-'
  }
  return `${current ?? '?'}${book.totalChapters ? ` / ${book.totalChapters}` : ''}`
}

export function formatDate(value?: string | null) {
  if (!value) {
    return '-'
  }

  return new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}

function formatList(values: string[]) {
  return values.length ? values.slice(0, 3).join(', ') : '-'
}

function truncate(value?: string | null) {
  if (!value) {
    return '-'
  }

  return value.length > 80 ? `${value.slice(0, 77)}...` : value
}
