import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, ChevronDown, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, ChevronsUpDown, Edit, Eye, LayoutGrid, List, Plus, Search, Settings2, Upload } from 'lucide-react'
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
const cardsPerRowOptions = [2, 3, 4, 5, 6, 7, 8] as const
const bookColumnsStorageKey = 'novelki.books.columns.v1'
const bookLayoutStorageKey = 'novelki.books.layout.v1'
const cardsPerRowStorageKey = 'novelki.books.cards-per-row.v1'
const columnPopupWidthPx = 320
const columnPopupEdgeGapPx = 16
const columnPopupVerticalGapPx = 10
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
  const [cardsPerRow, setCardsPerRow] = useCardsPerRow(cardsPerRowStorageKey)
  const [lastImportResult, setLastImportResult] = useState<BookImportFinalizeResult | null>(null)
  const [importDialogOpen, setImportDialogOpen] = useState(false)
  const [showBackToTop, setShowBackToTop] = useState(false)
  const [activePageGapId, setActivePageGapId] = useState<string | null>(null)
  const pendingBottomAnchorRef = useRef<number | null>(null)
  const skip = Number(searchParams.get('skip') ?? 0)
  const pageSize = readPageSize(searchParams)
  const sortBy = searchParams.get('sortBy') ?? 'lastModified'
  const sortDirection = readSortDirection(searchParams)
  const query = searchParams.get('query') ?? ''
  const visibleColumns = getVisibleColumns(bookColumns, columnPreferences)
  const booksQuery = useQuery({
    queryKey: ['books', skip, pageSize, query, sortBy, sortDirection],
    queryFn: () => api.getBooks({ skip, take: pageSize, query, sortBy, sortDirection }),
    placeholderData: keepPreviousData,
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
  const totalPages = Math.max(1, Math.ceil(total / pageSize))
  const currentPage = Math.min(totalPages, Math.floor(skip / pageSize) + 1)
  const visiblePages = getVisiblePageNumbers(currentPage, totalPages)

  useEffect(() => {
    function updateBackToTopVisibility() {
      setShowBackToTop(window.scrollY > 480)
    }

    updateBackToTopVisibility()
    window.addEventListener('scroll', updateBackToTopVisibility, { passive: true })
    return () => window.removeEventListener('scroll', updateBackToTopVisibility)
  }, [])

  useEffect(() => {
    if (booksQuery.isFetching || pendingBottomAnchorRef.current == null) {
      return
    }

    const distanceFromBottom = pendingBottomAnchorRef.current
    pendingBottomAnchorRef.current = null

    requestAnimationFrame(() => {
      const target = Math.max(0, document.documentElement.scrollHeight - window.innerHeight - distanceFromBottom)
      window.scrollTo({ top: target })
    })
  }, [booksQuery.isFetching, booksQuery.data?.data.length, skip])

  function handleImportComplete(result: BookImportFinalizeResult) {
    setLastImportResult(result)
    booksQuery.refetch()
    toast.success(`Imported ${result.importedCount} books. Skipped ${result.skippedCount}.`)
  }

  function scrollBackToTop() {
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  function goToPage(page: number) {
    const nextPage = Math.min(Math.max(1, page), totalPages)
    prepareBottomAnchorForPageChange()
    setActivePageGapId(null)
    setSkip((nextPage - 1) * pageSize)
  }

  function prepareBottomAnchorForPageChange() {
    const distanceFromBottom = document.documentElement.scrollHeight - (window.scrollY + window.innerHeight)
    if (distanceFromBottom <= 24) {
      pendingBottomAnchorRef.current = Math.max(0, distanceFromBottom)
    } else {
      pendingBottomAnchorRef.current = null
    }
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

      <section className="rounded-lg border border-slate-200 bg-white shadow-sm">
        <div className="flex flex-wrap items-center justify-end gap-2 border-b border-slate-200 px-4 py-3">
          {booksQuery.isFetching && !booksQuery.isLoading ? (
            <span className="mr-auto text-xs font-medium text-slate-500">Searching...</span>
          ) : null}
          {viewMode === 'cards' ? <CardsPerRowControl value={cardsPerRow} onChange={setCardsPerRow} /> : null}
          <ViewModeToggle value={viewMode} onChange={setViewMode} />
          <ColumnSettingsPopup columns={bookColumns} preferences={columnPreferences} onChange={setColumnPreferences} />
        </div>
        <BooksListFooter
          activePageGapId={activePageGapId}
          canGoBack={canGoBack}
          canGoForward={canGoForward}
          currentPage={currentPage}
          pageSize={pageSize}
          setActivePageGapId={setActivePageGapId}
          setPageSize={setPageSize}
          skip={skip}
          total={total}
          totalPages={totalPages}
          visiblePages={visiblePages}
          onGoToPage={goToPage}
        />
        {viewMode === 'table' ? (
          <div className="w-full overflow-x-auto pb-24">
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
          <BookCardGrid books={booksQuery.data?.data ?? []} cardsPerRow={cardsPerRow} isLoading={booksQuery.isLoading} />
        )}
      </section>
      <ImportBooksDialog open={importDialogOpen} onClose={() => setImportDialogOpen(false)} onImported={handleImportComplete} />
      {showBackToTop ? (
        <button
          aria-label="Back to top"
          className="fixed bottom-6 right-6 z-40 inline-flex h-11 w-11 items-center justify-center rounded-full border border-slate-300 bg-white text-slate-700 shadow-xl transition hover:border-slate-400 hover:bg-slate-50 hover:text-slate-950"
          type="button"
          onClick={scrollBackToTop}
        >
          <ArrowUp className="h-4 w-4" />
        </button>
      ) : null}
    </div>
  )
}

function CardsPerRowControl({
  value,
  onChange,
}: {
  value: number
  onChange: (value: number) => void
}) {
  return (
    <label className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-slate-500">
      <span>Cards / row</span>
      <span className="relative inline-flex">
        <select
          aria-label="Cards per row"
          className={`${inputClass} h-10 w-20 appearance-none bg-white pr-9 text-sm font-semibold text-slate-700 transition hover:border-slate-400 hover:bg-white focus:bg-white`}
          value={value}
          onChange={(event) => onChange(Number(event.target.value))}
        >
          {cardsPerRowOptions.map((option) => <option key={option} value={option}>{option}</option>)}
        </select>
        <ChevronDown className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
      </span>
    </label>
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

const compactPaginationButtonClass =
  'inline-flex min-h-10 min-w-10 items-center justify-center rounded-full px-3 text-xl font-medium text-slate-700 transition hover:bg-slate-100 hover:text-slate-950'

const compactActivePaginationButtonClass =
  'inline-flex min-h-10 min-w-10 items-center justify-center rounded-full bg-slate-900 px-3 text-xl font-medium text-white shadow-sm'

function PageGapJump({
  gapId,
  isOpen,
  totalPages,
  onGoToPage,
  onOpen,
  onClose,
}: {
  gapId: string
  isOpen: boolean
  totalPages: number
  onGoToPage: (page: number) => void
  onOpen: () => void
  onClose: () => void
}) {
  const [value, setValue] = useState('')
  const containerRef = useRef<HTMLDivElement | null>(null)

  const parsed = Number(value)
  const isValid = Number.isInteger(parsed) && parsed >= 1 && parsed <= totalPages

  function submit() {
    if (!isValid) {
      return false
    }

    onGoToPage(parsed)
    setValue('')
    return true
  }

  useEffect(() => {
    if (!isOpen) {
      return
    }

    function handlePointerDown(event: PointerEvent) {
      if (containerRef.current?.contains(event.target as Node)) {
        return
      }

      if (submit()) {
        return
      }

      onClose()
    }

    document.addEventListener('pointerdown', handlePointerDown)
    return () => document.removeEventListener('pointerdown', handlePointerDown)
  }, [isOpen, isValid, onClose, parsed, totalPages])

  useEffect(() => {
    if (!isOpen) {
      setValue('')
    }
  }, [isOpen])

  return (
    <div className="relative" ref={containerRef}>
      <button
        aria-label="Jump between pages"
        aria-expanded={isOpen}
        className={compactPaginationButtonClass}
        type="button"
        onClick={() => {
          if (isOpen) {
            onClose()
            return
          }

          onOpen()
        }}
      >
        ...
      </button>
      {isOpen ? (
        <div className="absolute bottom-full left-1/2 z-40 mb-2 -translate-x-1/2 rounded-xl border border-slate-200 bg-white p-3 shadow-xl">
          <input
            aria-invalid={!isValid && value.length > 0 ? 'true' : undefined}
            data-gap-id={gapId}
            aria-label="Page number"
            className={`${inputClass} h-10 w-24 bg-white text-center ${!isValid && value.length > 0 ? '!border-rose-500 focus:!border-rose-400 focus:ring-rose-400/20' : ''}`}
            inputMode="numeric"
            max={totalPages}
            min={1}
            placeholder={`1-${totalPages}`}
            value={value}
            onChange={(event) => setValue(event.target.value.replace(/[^\d]/g, ''))}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                submit()
              }
              if (event.key === 'Escape') {
                onClose()
              }
            }}
          />
        </div>
      ) : null}
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
        Supports filters like <code>author:John</code>, <code>tag:favorite,"to read soon"</code>, <code>genre:fantasy,"slice of life"</code>, <code>rating&gt;=8</code>, <code>rating:8</code>, and wildcard searches like <code>title:i*</code>.
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

export function useCardsPerRow(storageKey: string) {
  const [cardsPerRow, setCardsPerRowState] = useState<number>(() => readCardsPerRow(storageKey))

  function setCardsPerRow(next: number) {
    const normalized = normalizeCardsPerRow(next)
    setCardsPerRowState(normalized)
    window.localStorage.setItem(storageKey, String(normalized))
  }

  return [cardsPerRow, setCardsPerRow] as const
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

function BookCardGrid({
  books,
  cardsPerRow,
  isLoading,
}: {
  books: BookDto[]
  cardsPerRow: number
  isLoading: boolean
}) {
  if (isLoading) {
    return <div className="px-4 py-8 text-center text-slate-500">Loading...</div>
  }

  if (!books.length) {
    return <div className="px-4 py-8 text-center text-slate-500">No books match the current filters.</div>
  }

  return (
    <div className={`grid gap-4 p-4 pb-24 sm:grid-cols-2 ${getDesktopCardsPerRowClass(cardsPerRow)}`}>
      {books.map((book) => (
        <article className="rounded-2xl border border-slate-200 bg-slate-50 p-3 shadow-sm" key={book.id}>
          <Link className="grid gap-3" to={`/books/${book.id}`}>
            <div className="relative">
              <BookCoverArtwork
                className="w-full"
                cover={book.cover}
                emptyLabel="No cover"
                title={book.primaryTitle}
              />
              {book.rating != null ? (
                <span
                  className={`absolute right-3 top-3 inline-flex min-h-10 min-w-10 items-center justify-center rounded-full px-3 text-sm font-bold shadow-lg ${getRatingBadgeClass(book.rating)}`}
                >
                  {book.rating}
                </span>
              ) : null}
              <span className={`absolute bottom-3 right-3 inline-flex max-w-[calc(100%-1.5rem)] items-center rounded-full border px-3 py-1 text-[11px] font-semibold uppercase tracking-wide shadow-lg ${getStatusBadgeClass(book.status)}`}>
                <span className="truncate">{book.status}</span>
              </span>
            </div>
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

function BooksListFooter({
  activePageGapId,
  canGoBack,
  canGoForward,
  currentPage,
  pageSize,
  setActivePageGapId,
  setPageSize,
  skip,
  total,
  totalPages,
  visiblePages,
  onGoToPage,
}: {
  activePageGapId: string | null
  canGoBack: boolean
  canGoForward: boolean
  currentPage: number
  pageSize: number
  setActivePageGapId: (value: string | null | ((current: string | null) => string | null)) => void
  setPageSize: (nextPageSize: string) => void
  skip: number
  total: number
  totalPages: number
  visiblePages: Array<number | 'ellipsis'>
  onGoToPage: (page: number) => void
}) {
  return (
    <div className="sticky bottom-3 z-20 mx-4 mt-3 rounded-2xl border border-slate-200 bg-white/95 px-4 py-3 text-sm text-slate-600 shadow-lg backdrop-blur supports-[backdrop-filter]:bg-white/85">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <span>{total ? `${skip + 1}-${Math.min(skip + pageSize, total)} of ${total}` : '0 results'}</span>
        <div className="flex flex-wrap items-center justify-end gap-3">
          <label className="flex items-center gap-2">
            <span>Per page</span>
            <span className="relative inline-flex">
              <select
                className={`${inputClass} h-10 w-24 appearance-none bg-white pr-9 transition hover:border-slate-400 hover:bg-white focus:bg-white`}
                value={pageSize}
                onChange={(event) => setPageSize(event.target.value)}
              >
                {pageSizeOptions.map((option) => <option key={option} value={option}>{option}</option>)}
              </select>
              <ChevronDown className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
            </span>
          </label>
          <div className="flex flex-wrap items-center gap-1.5">
            {canGoBack ? (
              <button aria-label="First page" className={compactPaginationButtonClass} type="button" onClick={() => onGoToPage(1)}>
                <ChevronsLeft className="h-4 w-4" />
              </button>
            ) : null}
            {canGoBack ? (
              <button aria-label="Previous page" className={compactPaginationButtonClass} type="button" onClick={() => onGoToPage(currentPage - 1)}>
                <ChevronLeft className="h-4 w-4" />
              </button>
            ) : null}
            {visiblePages.map((item, index) => item === 'ellipsis'
              ? (
                <PageGapJump
                  gapId={`ellipsis-${index}`}
                  isOpen={activePageGapId === `ellipsis-${index}`}
                  key={`ellipsis-${index}`}
                  totalPages={totalPages}
                  onGoToPage={onGoToPage}
                  onOpen={() => setActivePageGapId(`ellipsis-${index}`)}
                  onClose={() => setActivePageGapId((current) => current === `ellipsis-${index}` ? null : current)}
                />
              )
              : (
                <button
                  aria-current={item === currentPage ? 'page' : undefined}
                  className={item === currentPage ? compactActivePaginationButtonClass : compactPaginationButtonClass}
                  key={item}
                  type="button"
                  onClick={() => onGoToPage(item)}
                >
                  {item}
                </button>
              ))}
            {canGoForward ? (
              <button aria-label="Next page" className={compactPaginationButtonClass} type="button" onClick={() => onGoToPage(currentPage + 1)}>
                <ChevronRight className="h-4 w-4" />
              </button>
            ) : null}
            {canGoForward ? (
              <button aria-label="Last page" className={compactPaginationButtonClass} type="button" onClick={() => onGoToPage(totalPages)}>
                <ChevronsRight className="h-4 w-4" />
              </button>
            ) : null}
          </div>
        </div>
      </div>
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

export function readCardsPerRow(storageKey: string) {
  const stored = Number(window.localStorage.getItem(storageKey) ?? 4)
  return normalizeCardsPerRow(stored)
}

function readSortDirection(searchParams: URLSearchParams): SortDirection {
  return searchParams.get('sortDirection') === 'asc' ? 'asc' : 'desc'
}

function normalizeCardsPerRow(value: number) {
  return cardsPerRowOptions.includes(value as typeof cardsPerRowOptions[number]) ? value : 4
}

function getDesktopCardsPerRowClass(cardsPerRow: number) {
  const normalized = normalizeCardsPerRow(cardsPerRow)
  const classMap: Record<number, string> = {
    2: 'lg:grid-cols-2',
    3: 'lg:grid-cols-3',
    4: 'lg:grid-cols-4',
    5: 'lg:grid-cols-5',
    6: 'lg:grid-cols-6',
    7: 'lg:grid-cols-7',
    8: 'lg:grid-cols-8',
  }

  return classMap[normalized]
}

function getVisiblePageNumbers(currentPage: number, totalPages: number): Array<number | 'ellipsis'> {
  if (totalPages <= 7) {
    return Array.from({ length: totalPages }, (_, index) => index + 1)
  }

  if (currentPage <= 4) {
    return [1, 2, 3, 4, 5, 6, 'ellipsis', totalPages]
  }

  if (currentPage >= totalPages - 3) {
    return [1, 'ellipsis', totalPages - 5, totalPages - 4, totalPages - 3, totalPages - 2, totalPages - 1, totalPages]
  }

  return [1, 'ellipsis', currentPage - 1, currentPage, currentPage + 1, 'ellipsis', totalPages]
}

function getRatingBadgeClass(rating: number) {
  if (rating <= 2) {
    return 'bg-rose-600/95 text-white'
  }
  if (rating <= 4) {
    return 'bg-orange-500/95 text-white'
  }
  if (rating <= 6) {
    return 'bg-amber-400/95 text-slate-950'
  }
  if (rating <= 8) {
    return 'bg-lime-500/95 text-slate-950'
  }
  return 'bg-emerald-500/95 text-white'
}

function getStatusBadgeClass(status: string) {
  const normalized = status.trim().toLowerCase()
  if (normalized === 'reading') {
    return 'border-indigo-900/80 bg-indigo-600/95 text-white'
  }
  if (normalized === 'completed') {
    return 'border-emerald-900/80 bg-emerald-600/95 text-white'
  }
  if (normalized === 'plan to read') {
    return 'border-amber-900/80 bg-amber-400/95 text-slate-950'
  }
  if (normalized === 'on hold') {
    return 'border-violet-900/80 bg-violet-600/95 text-white'
  }
  if (normalized === 'dropped') {
    return 'border-rose-900/80 bg-rose-600/95 text-white'
  }

  return 'border-slate-700/80 bg-slate-300/95 text-slate-950'
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
