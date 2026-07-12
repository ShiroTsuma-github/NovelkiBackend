import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, ChevronDown, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, ChevronsUpDown, Download, Edit, Eye, LayoutGrid, List, Plus, RefreshCw, Search, Settings2, Upload } from 'lucide-react'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Bar, BarChart, Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { Link, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { api } from '@/api/client'
import type { BookDto, BookImportFinalizeResult, BookSummaryDto, BookSummaryTypeCountDto } from '@/api/types'
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
const bookCardFieldsStorageKey = 'novelki.books.card-fields.v1'
const bookLayoutStorageKey = 'novelki.books.layout.v1'
const cardsPerRowStorageKey = 'novelki.books.cards-per-row.v1'
const summaryReadingTimeStorageKey = 'novelki.books.summary.time-per-chapter.v1'
const columnPopupWidthPx = 320
const columnPopupEdgeGapPx = 16
const columnPopupVerticalGapPx = 10
const topActionButtonSpacingClass = 'gap-2.5 pl-3.5 pr-4'
const summaryChartColors = ['#0f766e', '#0891b2', '#2563eb', '#7c3aed', '#db2777', '#ea580c', '#ca8a04', '#65a30d']
type SortDirection = string
type SummaryTabId = 'status' | 'types' | 'ratings' | 'genres' | 'time'
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

const bookCardFields: ColumnDefinition<BookDto>[] = [
  { id: 'title', label: 'Title', defaultVisible: true, render: (book) => <span>{book.primaryTitle}</span> },
  { id: 'alternativeTitles', label: 'Alternative titles', defaultVisible: false, render: (book) => <span>{formatList(book.alternativeTitles)}</span> },
  { id: 'author', label: 'Author', defaultVisible: true, render: (book) => <span>{book.author ?? 'Unknown author'}</span> },
  { id: 'status', label: 'Status', defaultVisible: true, render: (book) => <span>{book.status}</span> },
  { id: 'type', label: 'Type', defaultVisible: false, render: (book) => <span>{book.contentType}</span> },
  { id: 'progress', label: 'Progress', defaultVisible: true, render: (book) => <span>{formatProgress(book)}</span> },
  { id: 'rating', label: 'Rating', defaultVisible: true, render: (book) => <span>{book.rating ?? '-'}</span> },
]

export function BooksPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const queryClient = useQueryClient()
  const [columnPreferences, setColumnPreferences] = useColumnPreferences(bookColumnsStorageKey, bookColumns)
  const [cardFieldPreferences, setCardFieldPreferences] = useColumnPreferences(bookCardFieldsStorageKey, bookCardFields)
  const [viewMode, setViewMode] = useViewMode(bookLayoutStorageKey)
  const [cardsPerRow, setCardsPerRow] = useCardsPerRow(cardsPerRowStorageKey)
  const [lastImportResult, setLastImportResult] = useState<BookImportFinalizeResult | null>(null)
  const [importDialogOpen, setImportDialogOpen] = useState(false)
  const [summaryOpen, setSummaryOpen] = useState(false)
  const [showBackToTop, setShowBackToTop] = useState(false)
  const [showGoDown, setShowGoDown] = useState(false)
  const [activePageGapId, setActivePageGapId] = useState<string | null>(null)
  const pendingBottomAnchorRef = useRef<number | null>(null)
  const skip = Number(searchParams.get('skip') ?? 0)
  const pageSize = readPageSize(searchParams)
  const sortBy = searchParams.get('sortBy') ?? 'lastModified'
  const sortDirection = readSortDirection(searchParams)
  const query = searchParams.get('query') ?? ''
  const visibleColumns = getVisibleColumns(bookColumns, columnPreferences)
  const visibleCardFields = getVisibleColumns(bookCardFields, cardFieldPreferences)
  const booksQuery = useQuery({
    queryKey: ['books', skip, pageSize, query, sortBy, sortDirection],
    queryFn: () => api.getBooks({ skip, take: pageSize, query, sortBy, sortDirection }),
    placeholderData: keepPreviousData,
    staleTime: 30_000,
  })
  const summaryQuery = useQuery({
    queryKey: ['books-summary', query],
    queryFn: () => api.getBooksSummary({ query }),
    enabled: summaryOpen,
    staleTime: 30_000,
  })
  const cycleSortMutation = useMutation({
    mutationFn: ({ nextSortBy, currentSortDirection }: { nextSortBy: string; currentSortDirection: string | null }) => (
      api.getBooks({
        skip,
        take: pageSize,
        query,
        sortBy: nextSortBy,
        sortDirection: currentSortDirection ?? undefined,
        advanceCycle: true,
      })
    ),
    onSuccess: (result, variables) => {
      const resolvedSortDirection = getCyclicSortDirectionFromResult(result.data, variables.nextSortBy) ?? variables.currentSortDirection ?? sortDirection
      queryClient.setQueryData(
        ['books', skip, pageSize, query, variables.nextSortBy, resolvedSortDirection],
        result,
      )

      const next = new URLSearchParams(searchParams)
      next.set('sortBy', variables.nextSortBy)
      next.set('sortDirection', resolvedSortDirection)
      next.delete('skip')
      setSearchParams(next)
    },
  })
  const exportMutation = useMutation({
    mutationFn: () => api.downloadBooksExport({ query, sortBy, sortDirection }),
    onSuccess: (blob) => {
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = 'books-export.csv'
      document.body.append(link)
      link.click()
      link.remove()
      URL.revokeObjectURL(url)
      toast.success('Export ready.')
    },
    onError: () => {
      toast.error('Could not export books.')
    },
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
    if (isCyclicSort(nextSortBy)) {
      cycleSortMutation.mutate({
        nextSortBy,
        currentSortDirection: sortBy === nextSortBy ? sortDirection : null,
      })
      return
    }

    const next = new URLSearchParams(searchParams)
    const nextDirection = getNextSortDirection(nextSortBy, sortBy, sortDirection)
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
  const settingsColumns = viewMode === 'cards' ? bookCardFields : bookColumns
  const settingsPreferences = viewMode === 'cards' ? cardFieldPreferences : columnPreferences
  const setSettingsPreferences = viewMode === 'cards' ? setCardFieldPreferences : setColumnPreferences
  const settingsTitle = viewMode === 'cards' ? 'Visible card fields' : 'Visible columns'
  const settingsDescription = viewMode === 'cards' ? 'Choose which book fields appear on cards.' : 'Change table order and visibility.'
  const allowSettingsReorder = viewMode === 'table'

  useEffect(() => {
    function updateScrollShortcutVisibility() {
      const distanceFromBottom = document.documentElement.scrollHeight - (window.scrollY + window.innerHeight)
      setShowBackToTop(window.scrollY > 480)
      setShowGoDown(distanceFromBottom > 480)
    }

    updateScrollShortcutVisibility()
    window.addEventListener('scroll', updateScrollShortcutVisibility, { passive: true })
    window.addEventListener('resize', updateScrollShortcutVisibility)
    return () => {
      window.removeEventListener('scroll', updateScrollShortcutVisibility)
      window.removeEventListener('resize', updateScrollShortcutVisibility)
    }
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

  function scrollToPageBottom() {
    window.scrollTo({ top: document.documentElement.scrollHeight, behavior: 'smooth' })
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
          <button
            aria-controls="books-summary-panel"
            aria-expanded={summaryOpen}
            className={`${secondaryButtonClass} ${topActionButtonSpacingClass}`}
            type="button"
            onClick={() => setSummaryOpen((current) => !current)}
          >
            <span>Summary</span>
            <ChevronDown className={`h-4 w-4 transition ${summaryOpen ? 'rotate-180' : ''}`} />
          </button>
          <button className={`${secondaryButtonClass} ${topActionButtonSpacingClass}`} disabled={exportMutation.isPending} type="button" onClick={() => exportMutation.mutate()}>
            <Download className="h-4 w-4" />
            {exportMutation.isPending ? 'Exporting...' : 'Export filtered CSV'}
          </button>
          <button className={`${secondaryButtonClass} ${topActionButtonSpacingClass}`} type="button" onClick={() => setImportDialogOpen(true)}>
            <Upload className="h-4 w-4" />
            Import CSV
          </button>
          <Link className={`${buttonClass} ${topActionButtonSpacingClass}`} to="/books/new">
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

      {summaryOpen ? (
        <BookSummaryPanel
          id="books-summary-panel"
          isError={summaryQuery.isError}
          isLoading={summaryQuery.isLoading}
          summary={summaryQuery.data}
        />
      ) : null}

      <BookAdvancedSearch value={query} onChange={updateQuery} />

      <section className="rounded-lg border border-slate-200 bg-white shadow-sm">
        <div className="flex flex-wrap items-center justify-end gap-2 border-b border-slate-200 px-4 py-3">
          {(booksQuery.isFetching || cycleSortMutation.isPending) && !booksQuery.isLoading ? (
            <span className="mr-auto text-xs font-medium text-slate-500">Searching...</span>
          ) : null}
          {viewMode === 'cards' ? <CardsPerRowControl value={cardsPerRow} onChange={setCardsPerRow} /> : null}
          <ViewModeToggle value={viewMode} onChange={setViewMode} />
          <ColumnSettingsPopup
            allowReorder={allowSettingsReorder}
            columns={settingsColumns}
            description={settingsDescription}
            preferences={settingsPreferences}
            title={settingsTitle}
            onChange={setSettingsPreferences}
          />
        </div>
        {viewMode === 'table' ? (
          <div className="w-full overflow-x-auto">
            <table className="w-full table-fixed border-collapse text-left text-sm">
              <thead className="bg-slate-100 text-xs uppercase tracking-wide text-slate-500">
                <tr>
                  {visibleColumns.map((column) => (
                    <ColumnHeader
                      activeDirection={column.sortBy === sortBy ? sortDirection : null}
                      column={column}
                      isCyclic={column.sortBy === 'status' || column.sortBy === 'type'}
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
          <BookCardGrid
            books={booksQuery.data?.data ?? []}
            cardsPerRow={cardsPerRow}
            fields={visibleCardFields}
            isLoading={booksQuery.isLoading}
          />
        )}
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
      </section>
      <ImportBooksDialog open={importDialogOpen} onClose={() => setImportDialogOpen(false)} onImported={handleImportComplete} />
      {showBackToTop || showGoDown ? (
        <div className="fixed bottom-6 right-6 z-40 flex flex-col gap-2">
          {showBackToTop ? (
            <button
              aria-label="Back to top"
              className="inline-flex h-11 w-11 items-center justify-center rounded-full border border-slate-300 bg-white text-slate-700 shadow-xl transition hover:border-slate-400 hover:bg-slate-50 hover:text-slate-950"
              type="button"
              onClick={scrollBackToTop}
            >
              <ArrowUp className="h-4 w-4" />
            </button>
          ) : null}
          {showGoDown ? (
            <button
              aria-label="Go to bottom"
              className="inline-flex h-11 w-11 items-center justify-center rounded-full border border-slate-300 bg-white text-slate-700 shadow-xl transition hover:border-slate-400 hover:bg-slate-50 hover:text-slate-950"
              type="button"
              onClick={scrollToPageBottom}
            >
              <ArrowDown className="h-4 w-4" />
            </button>
          ) : null}
        </div>
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
        <div className="absolute bottom-full left-1/2 z-40 mb-2 w-32 -translate-x-1/2 rounded-xl border border-slate-200 bg-white p-3 shadow-xl">
          <input
            autoFocus
            aria-invalid={!isValid && value.length > 0 ? 'true' : undefined}
            data-gap-id={gapId}
            aria-label="Page number"
            className={`${inputClass} h-10 w-full bg-white text-center ${!isValid && value.length > 0 ? '!border-rose-500 focus:!border-rose-400 focus:ring-rose-400/20' : ''}`}
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

function BookSummaryPanel({
  id,
  isError,
  isLoading,
  summary,
}: {
  id: string
  isError: boolean
  isLoading: boolean
  summary: BookSummaryDto | undefined
}) {
  const [activeTab, setActiveTab] = useState<SummaryTabId>('status')
  const [minutesPerType, setMinutesPerType] = useState<Record<string, number>>(() => readSummaryReadingTimes(summaryReadingTimeStorageKey))

  useEffect(() => {
    const knownTypes = new Set(summary?.typeCounts.map((type) => type.type) ?? [])
    if (!knownTypes.size) {
      return
    }

    setMinutesPerType((current) => {
      const next = { ...current }
      let changed = false
      for (const type of knownTypes) {
        if (!(type in next)) {
          next[type] = 5
          changed = true
        }
      }

      if (changed) {
        window.localStorage.setItem(summaryReadingTimeStorageKey, JSON.stringify(next))
      }

      return changed ? next : current
    })
  }, [summary?.typeCounts])

  if (isLoading) {
    return (
      <section className="grid gap-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm" id={id}>
        <div>
          <h2 className="text-sm font-semibold text-slate-950">Library summary</h2>
          <p className="text-sm text-slate-500">Loading summary...</p>
        </div>
      </section>
    )
  }

  if (isError) {
    return (
      <section className="grid gap-3 rounded-lg border border-rose-200 bg-rose-50 p-4 shadow-sm" id={id}>
        <div>
          <h2 className="text-sm font-semibold text-rose-950">Library summary</h2>
          <p className="text-sm text-rose-800">Could not load summary.</p>
        </div>
      </section>
    )
  }

  if (!summary) {
    return null
  }

  const statusChartData = summary.statusCounts.map((status) => ({ label: status.status, value: status.count }))
  const ratingChartData = [
    ...summary.ratingCounts.map((rating) => ({ label: String(rating.rating), value: rating.bookCount })),
    ...(summary.unratedBooks > 0 ? [{ label: 'Unrated', value: summary.unratedBooks }] : []),
  ]
  const genreChartData = summary.genreCounts.map((genre) => ({ label: genre.genre, value: genre.bookCount }))
  const estimatedReading = getEstimatedReadingTime(summary.typeCounts, minutesPerType)

  return (
    <section className="grid gap-4 rounded-lg border border-slate-200 bg-white p-4 shadow-sm" id={id}>
      <div>
        <h2 className="text-sm font-semibold text-slate-950">Library summary</h2>
        <p className="text-sm text-slate-500">Aggregated from all books matching the current filters.</p>
      </div>
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
        <SummaryMetricCard label="Total books" value={String(summary.totalBooks)} />
        <SummaryMetricCard label="Rated" value={String(summary.ratedBooks)} />
        <SummaryMetricCard label="Unrated" value={String(summary.unratedBooks)} />
        <SummaryMetricCard label="Average rating" value={formatAverageRating(summary.averageRating)} />
        <SummaryMetricCard label="Current chapters" value={formatChapterCount(summary.currentChapters)} />
      </div>
      {summary.totalBooks === 0 ? (
        <div className="rounded-lg border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-600">
          No books match the current filters.
        </div>
      ) : (
        <div className="grid gap-4 rounded-xl border border-slate-200 bg-slate-50 p-4">
          <div className="flex flex-wrap gap-2">
            {([
              { id: 'status', label: 'Status' },
              { id: 'types', label: 'Types' },
              { id: 'ratings', label: 'Ratings' },
              { id: 'genres', label: 'Genres' },
              { id: 'time', label: 'Time' },
            ] as const).map((tab) => (
              <button
                aria-pressed={activeTab === tab.id}
                className={`rounded-full border px-3 py-1.5 text-sm font-semibold transition ${activeTab === tab.id ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-300 bg-white text-slate-700 hover:border-slate-400 hover:text-slate-950'}`}
                key={tab.id}
                type="button"
                onClick={() => setActiveTab(tab.id)}
              >
                {tab.label}
              </button>
            ))}
          </div>
          {activeTab === 'status' ? (
            <SummaryTabLayout
              chart={<SummaryPieChart data={statusChartData} valueLabel="books" />}
              details={<SummaryCountList items={summary.statusCounts.map((status) => ({ label: status.status, value: String(status.count) }))} />}
              title="Status distribution"
            />
          ) : null}
          {activeTab === 'types' ? (
            <SummaryTabLayout
              chart={<SummaryTypeBarChart data={summary.typeCounts} />}
              details={<SummaryTypeDetails items={summary.typeCounts} />}
              title="Book types and current chapters"
            />
          ) : null}
          {activeTab === 'ratings' ? (
            <SummaryTabLayout
              chart={<SummaryRatingsBarChart data={ratingChartData} />}
              details={<SummaryCountList items={ratingChartData.map((rating) => ({ label: rating.label, value: String(rating.value) }))} />}
              title="Rating spread"
            />
          ) : null}
          {activeTab === 'genres' ? (
            <SummaryTabLayout
              chart={<SummaryPieChart data={genreChartData} valueLabel="books" />}
              details={<SummaryCountList items={summary.genreCounts.map((genre) => ({ label: genre.genre, value: String(genre.bookCount) }))} />}
              title="Genre distribution"
            />
          ) : null}
          {activeTab === 'time' ? (
            <SummaryTabLayout
              chart={<SummaryTimeBarChart data={estimatedReading.byType} />}
              details={(
                <SummaryTimeDetails
                  booksWithKnownCurrentChapter={summary.booksWithKnownCurrentChapter}
                  booksWithoutKnownCurrentChapter={summary.booksWithoutKnownCurrentChapter}
                  minutesPerType={minutesPerType}
                  currentChapters={summary.currentChapters}
                  totalHours={estimatedReading.totalHours}
                  typeCounts={summary.typeCounts}
                  onMinutesChange={(type, next) => {
                    setMinutesPerType((current) => {
                      const normalized = Number.isFinite(next) ? Math.max(0, next) : 0
                      const updated = { ...current, [type]: normalized }
                      window.localStorage.setItem(summaryReadingTimeStorageKey, JSON.stringify(updated))
                      return updated
                    })
                  }}
                />
              )}
              title="Estimated reading time"
            />
          ) : null}
        </div>
      )}
    </section>
  )
}

function SummaryMetricCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-slate-200 bg-slate-50 px-4 py-3">
      <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">{label}</div>
      <div className="mt-1 text-2xl font-semibold text-slate-950">{value}</div>
    </div>
  )
}

function SummaryTabLayout({
  chart,
  details,
  title,
}: {
  chart: ReactNode
  details: ReactNode
  title: string
}) {
  return (
    <div className="grid gap-4 xl:grid-cols-[minmax(0,1.5fr)_minmax(18rem,1fr)]">
      <div className="rounded-xl border border-slate-200 bg-white p-4">
        <div className="mb-3 text-sm font-semibold text-slate-950">{title}</div>
        {chart}
      </div>
      <div className="rounded-xl border border-slate-200 bg-white p-4">
        {details}
      </div>
    </div>
  )
}

function SummaryPieChart({ data, valueLabel }: { data: Array<{ label: string; value: number }>; valueLabel: string }) {
  if (!data.length) {
    return <div className="flex h-72 items-center justify-center text-sm text-slate-500">No data for this tab.</div>
  }

  return (
    <div className="h-72 w-full">
      <ResponsiveContainer>
        <PieChart>
          <Pie
            data={data}
            dataKey="value"
            innerRadius={64}
            nameKey="label"
            outerRadius={104}
            paddingAngle={2}
          >
            {data.map((entry, index) => (
              <Cell fill={summaryChartColors[index % summaryChartColors.length]} key={entry.label} />
            ))}
          </Pie>
          <Tooltip formatter={(value, name) => [`${Number(value ?? 0)} ${valueLabel}`, String(name ?? '')]} />
          <Legend />
        </PieChart>
      </ResponsiveContainer>
    </div>
  )
}

function SummaryTypeBarChart({ data }: { data: BookSummaryTypeCountDto[] }) {
  if (!data.length) {
    return <div className="flex h-72 items-center justify-center text-sm text-slate-500">No data for this tab.</div>
  }

  return (
    <div className="h-72 w-full">
      <ResponsiveContainer>
        <BarChart data={data}>
          <XAxis dataKey="type" tickLine={false} axisLine={false} />
          <YAxis allowDecimals={false} tickLine={false} axisLine={false} />
          <Tooltip formatter={(value, _name, item) => [`${formatChapterCount(Number(value ?? 0))} chapters`, String(item.payload.type ?? '')]} />
          <Legend />
          <Bar dataKey="currentChapters" fill="#0f766e" name="Current chapters" radius={[8, 8, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </div>
  )
}

function SummaryRatingsBarChart({ data }: { data: Array<{ label: string; value: number }> }) {
  if (!data.length) {
    return <div className="flex h-72 items-center justify-center text-sm text-slate-500">No data for this tab.</div>
  }

  return (
    <div className="h-72 w-full">
      <ResponsiveContainer>
        <BarChart data={data}>
          <XAxis dataKey="label" tickLine={false} axisLine={false} />
          <YAxis allowDecimals={false} tickLine={false} axisLine={false} />
          <Tooltip formatter={(value, _name, item) => [`${Number(value ?? 0)} books`, `Rating ${String(item.payload.label)}`]} />
          <Legend />
          <Bar dataKey="value" fill="#7c3aed" name="Books" radius={[8, 8, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </div>
  )
}

function SummaryTimeBarChart({ data }: { data: Array<{ type: string; hours: number }> }) {
  if (!data.length) {
    return <div className="flex h-72 items-center justify-center text-sm text-slate-500">No known chapter counts yet.</div>
  }

  return (
    <div className="h-72 w-full">
      <ResponsiveContainer>
        <BarChart data={data}>
          <XAxis dataKey="type" tickLine={false} axisLine={false} />
          <YAxis tickLine={false} axisLine={false} />
          <Tooltip formatter={(value) => [`${Number(value ?? 0).toFixed(1)} h`, 'Estimated time']} />
          <Legend />
          <Bar dataKey="hours" fill="#2563eb" name="Estimated hours" radius={[8, 8, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </div>
  )
}

function SummaryCountList({ items }: { items: Array<{ label: string; value: string }> }) {
  if (!items.length) {
    return <div className="text-sm text-slate-500">No data for this tab.</div>
  }

  return (
    <div className="grid gap-2">
      {items.map((item) => (
        <div className="flex items-center justify-between rounded-md border border-slate-200 bg-slate-50 px-3 py-2 text-sm" key={item.label}>
          <span className="font-medium text-slate-700">{item.label}</span>
          <span className="rounded-full bg-white px-2.5 py-1 text-xs font-semibold text-slate-700">{item.value}</span>
        </div>
      ))}
    </div>
  )
}

function SummaryTypeDetails({ items }: { items: BookSummaryTypeCountDto[] }) {
  if (!items.length) {
    return <div className="text-sm text-slate-500">No type data for this filter.</div>
  }

  return (
    <div className="grid gap-2">
      {items.map((item) => (
        <div className="rounded-md border border-slate-200 bg-slate-50 px-3 py-3" key={item.type}>
          <div className="flex items-center justify-between gap-3">
            <div className="font-semibold text-slate-900">{item.type}</div>
            <div className="rounded-full bg-white px-2.5 py-1 text-xs font-semibold text-slate-700">{item.bookCount} books</div>
          </div>
          <div className="mt-2 text-sm text-slate-600">Current chapters: {formatChapterCount(item.currentChapters)}</div>
        </div>
      ))}
    </div>
  )
}

function SummaryTimeDetails({
  booksWithKnownCurrentChapter,
  booksWithoutKnownCurrentChapter,
  minutesPerType,
  currentChapters,
  totalHours,
  typeCounts,
  onMinutesChange,
}: {
  booksWithKnownCurrentChapter: number
  booksWithoutKnownCurrentChapter: number
  minutesPerType: Record<string, number>
  currentChapters: number
  totalHours: number
  typeCounts: BookSummaryTypeCountDto[]
  onMinutesChange: (type: string, next: number) => void
}) {
  return (
    <div className="grid gap-3">
      <div className="rounded-lg border border-slate-200 bg-slate-50 p-3">
        <div className="text-sm font-semibold text-slate-950">Estimated total</div>
        <div className="mt-1 text-2xl font-semibold text-slate-950">{formatHours(totalHours)}</div>
        <div className="mt-2 text-sm text-slate-600">
          {formatDays(totalHours)} and {formatMonths(totalHours)} based on {formatChapterCount(currentChapters)} current chapters.
        </div>
        <div className="text-sm text-slate-500">
          Books with current chapter: {booksWithKnownCurrentChapter}. Missing current chapter: {booksWithoutKnownCurrentChapter}
        </div>
      </div>
      <div className="grid gap-2">
        {typeCounts.map((type) => (
          <label className="grid gap-2 rounded-md border border-slate-200 bg-white p-3" key={type.type}>
            <div className="flex items-center justify-between gap-3">
              <span className="font-semibold text-slate-900">{type.type}</span>
              <span className="text-sm text-slate-500">{formatChapterCount(type.currentChapters)} current chapters</span>
            </div>
            <div className="flex items-center gap-3">
              <input
                className={`${inputClass} h-10 w-28`}
                min="0"
                step="1"
                type="number"
                value={minutesPerType[type.type] ?? 5}
                onChange={(event) => onMinutesChange(type.type, Number(event.target.value))}
              />
              <span className="text-sm text-slate-600">minutes per chapter</span>
            </div>
          </label>
        ))}
      </div>
    </div>
  )
}

export function ColumnSettingsPopup<T>({
  allowReorder = true,
  columns,
  description,
  preferences,
  title,
  onChange,
}: {
  allowReorder?: boolean
  columns: ColumnDefinition<T>[]
  description?: string
  preferences: ColumnPreference[]
  title?: string
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
  fields,
  isLoading,
}: {
  books: BookDto[]
  cardsPerRow: number
  fields: ColumnDefinition<BookDto>[]
  isLoading: boolean
}) {
  if (isLoading) {
    return <div className="px-4 py-8 text-center text-slate-500">Loading...</div>
  }

  if (!books.length) {
    return <div className="px-4 py-8 text-center text-slate-500">No books match the current filters.</div>
  }

  const cardText = getCardTextSizeClasses(cardsPerRow)
  const showTitle = hasCardField(fields, 'title')
  const showAlternativeTitles = hasCardField(fields, 'alternativeTitles')
  const showAuthor = hasCardField(fields, 'author')
  const showProgress = hasCardField(fields, 'progress')
  const showType = hasCardField(fields, 'type')
  const cardDetailsClass = getCardDetailRowClass({
    showAlternativeTitles,
    showAuthor,
    showProgress,
    showTitle,
    showType,
  })

  return (
    <div className={`grid gap-4 p-4 sm:grid-cols-2 ${getDesktopCardsPerRowClass(cardsPerRow)}`}>
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
              {hasCardField(fields, 'rating') && book.rating != null ? (
                <span
                  className={`absolute right-3 top-3 inline-flex min-h-10 min-w-10 items-center justify-center rounded-full px-3 text-sm font-bold shadow-lg ${getRatingBadgeClass(book.rating)}`}
                >
                  {book.rating}
                </span>
              ) : null}
              {hasCardField(fields, 'status') ? (
                <span className={`absolute bottom-3 right-3 inline-flex max-w-[calc(100%-1.5rem)] items-center rounded-full border px-3 py-1 text-[11px] font-semibold uppercase tracking-wide shadow-lg ${getStatusBadgeClass(book.status)}`}>
                  <span className="truncate">{book.status}</span>
                </span>
              ) : null}
            </div>
            <div className={`grid gap-1 ${cardDetailsClass}`}>
              {showTitle ? (
                <div className="min-h-10">
                  <h2 className={`line-clamp-2 font-semibold text-slate-950 ${cardText.title}`}>{book.primaryTitle}</h2>
                </div>
              ) : null}
              {showAlternativeTitles ? (
                <div className="min-h-10">
                  <p className={`line-clamp-2 text-slate-500 ${cardText.meta}`}>{formatList(book.alternativeTitles)}</p>
                </div>
              ) : null}
              {showAuthor ? (
                <div className="min-h-6">
                  <p className={`truncate text-slate-500 ${cardText.meta}`}>{book.author ?? 'Unknown author'}</p>
                </div>
              ) : null}
              {showProgress || showType ? (
                <div className="flex min-h-7 items-end justify-between gap-3">
                  <div className="min-w-0 flex-1">
                    {showProgress ? (
                      <p className={`truncate font-medium text-slate-700 ${cardText.meta}`}>{formatProgress(book)}</p>
                    ) : null}
                  </div>
                  <div className="shrink-0 text-right">
                    {showType ? (
                      <p className={`font-semibold italic tracking-wide text-slate-700 ${cardText.meta}`}>{book.contentType}</p>
                    ) : null}
                  </div>
                </div>
              ) : null}
            </div>
          </Link>
        </article>
      ))}
    </div>
  )
}

function hasCardField(fields: ColumnDefinition<BookDto>[], id: string) {
  return fields.some((field) => field.id === id)
}

export function getCardTextSizeClasses(cardsPerRow: number) {
  const normalized = normalizeCardsPerRow(cardsPerRow)

  if (normalized <= 4) {
    return {
      title: 'text-lg',
      meta: 'text-base',
    }
  }

  if (normalized <= 6) {
    return {
      title: 'text-base',
      meta: 'text-sm',
    }
  }

  return {
    title: 'text-sm',
    meta: 'text-xs',
  }
}

export function getCardDetailRowClass({
  showAlternativeTitles,
  showAuthor,
  showProgress,
  showTitle,
  showType,
}: {
  showAlternativeTitles: boolean
  showAuthor: boolean
  showProgress: boolean
  showTitle: boolean
  showType: boolean
}) {
  const rows: string[] = []

  if (showTitle) {
    rows.push('minmax(0,2.5rem)')
  }

  if (showAlternativeTitles) {
    rows.push('minmax(0,2.5rem)')
  }

  if (showAuthor) {
    rows.push('minmax(0,1.5rem)')
  }

  if (showProgress || showType) {
    rows.push('minmax(0,1.75rem)')
  }

  return rows.length ? `grid-rows-[${rows.join('_')}]` : ''
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
    <div className="border-t border-slate-200 bg-white px-4 py-3 text-sm text-slate-600">
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

function readSummaryReadingTimes(storageKey: string) {
  const stored = window.localStorage.getItem(storageKey)
  if (!stored) {
    return {}
  }

  try {
    const parsed = JSON.parse(stored) as Record<string, number>
    return Object.fromEntries(
      Object.entries(parsed).filter((entry): entry is [string, number] => typeof entry[0] === 'string' && Number.isFinite(entry[1])),
    )
  } catch {
    return {}
  }
}

function readSortDirection(searchParams: URLSearchParams): SortDirection {
  return searchParams.get('sortDirection') ?? 'desc'
}

function getNextSortDirection(
  nextSortBy: string,
  currentSortBy: string,
  currentSortDirection: string,
) {
  const defaultDirection = nextSortBy === 'lastModified' || nextSortBy === 'created' ? 'desc' : 'asc'
  return currentSortBy === nextSortBy
    ? currentSortDirection === 'asc' ? 'desc' : 'asc'
    : defaultDirection
}

function isCyclicSort(sortBy: string) {
  return sortBy === 'status' || sortBy === 'type'
}

function getCyclicSortDirectionFromResult(books: BookDto[], sortBy: string) {
  const firstBook = books[0]
  if (!firstBook) {
    return null
  }

  return sortBy === 'status'
    ? firstBook.status
    : sortBy === 'type'
      ? firstBook.contentType
      : null
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

export function formatAverageRating(value?: number | null) {
  return value == null ? '-' : value.toFixed(1)
}

export function formatChapterCount(value?: number | null) {
  if (value == null) {
    return '-'
  }

  return Number.isInteger(value) ? String(value) : value.toFixed(1)
}

function formatHours(value: number) {
  return `${value.toFixed(1)} h`
}

function formatDays(value: number) {
  return `${(value / 24).toFixed(1)} days`
}

function formatMonths(value: number) {
  return `${(value / (24 * 30)).toFixed(1)} months`
}

function getEstimatedReadingTime(typeCounts: BookSummaryTypeCountDto[], minutesPerType: Record<string, number>) {
  const byType = typeCounts
    .filter((type) => type.currentChapters > 0)
    .map((type) => {
      const minutes = (minutesPerType[type.type] ?? 5) * type.currentChapters
      return {
        type: type.type,
        hours: minutes / 60,
      }
    })
  const totalHours = byType.reduce((sum, item) => sum + item.hours, 0)

  return {
    byType,
    totalHours,
  }
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
