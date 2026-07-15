import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronDown, Download, Edit, Eye, LayoutGrid, List, Plus, Search, Upload } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { api } from '@/api/client'
import type { BookImportFinalizeResult, BookListItemDto, BookSummaryDto, BookSummaryTypeCountDto } from '@/api/types'
import { extractAnalyticsDateFilters } from '@/features/analytics/dateQueryFilters'
import {
  buttonClass,
  inputClass,
  secondaryButtonClass,
} from '@/components/app/FormField'
import { BookDataTable } from './BookDataTable'
import { BookCoverArtwork } from './BookCoverSection'
import { formatProgress } from './bookProgress'
import {
  ColumnSettingsPopup,
  getVisibleColumns,
  totalChaptersColumnLabel,
  useColumnPreferences,
  useViewMode,
  type BookViewMode,
  type ColumnDefinition,
} from './bookListColumns'
import {
  BookListFooter,
  getNextSortDirection,
  ScrollShortcutButtons,
  useBookListPagination,
  useBookListScrollShortcuts,
  useBookListUrlState,
} from './BookListShared'
import { ImportBooksDialog } from './ImportBooksDialog'

export { formatProgress } from './bookProgress'
export {
  ColumnHeader,
  ColumnSettingsPopup,
  defaultColumnPreferences,
  getColumnPopupPosition,
  getVisibleColumns,
  totalChaptersColumnLabel,
  useColumnPreferences,
  useViewMode,
  type BookViewMode,
  type ColumnDefinition,
  type ColumnPreference,
} from './bookListColumns'

const cardsPerRowOptions = [2, 3, 4, 5, 6, 7, 8] as const
const bookColumnsStorageKey = 'novelki.books.columns.v1'
const bookCardFieldsStorageKey = 'novelki.books.card-fields.v1'
const bookLayoutStorageKey = 'novelki.books.layout.v1'
const cardsPerRowStorageKey = 'novelki.books.cards-per-row.v1'
const topActionButtonSpacingClass = 'gap-2.5 pl-3.5 pr-4'

const bookColumns: ColumnDefinition<BookListItemDto>[] = [
  { id: 'id', label: 'Id', defaultVisible: false, widthClass: 'w-36', render: (book) => <span className="font-mono text-xs">{book.id}</span> },
  { id: 'title', label: 'Title', defaultVisible: true, sortBy: 'title', widthClass: 'w-72', render: (book) => <span className="block truncate font-medium text-slate-950">{book.primaryTitle}</span> },
  { id: 'alternativeTitles', label: 'Alternative titles', defaultVisible: false, widthClass: 'w-56', render: (book) => formatList(book.alternativeTitles) },
  { id: 'author', label: 'Author', defaultVisible: true, sortBy: 'author', widthClass: 'w-44', render: (book) => <span className="block truncate">{book.author ?? '-'}</span> },
  { id: 'status', label: 'Status', defaultVisible: true, sortBy: 'status', widthClass: 'w-32', render: (book) => book.status },
  { id: 'type', label: 'Type', defaultVisible: true, sortBy: 'type', widthClass: 'w-24', render: (book) => book.contentType },
  { id: 'progress', label: 'Progress', defaultVisible: true, sortBy: 'progress', widthClass: 'w-32', render: formatProgress },
  { id: 'totalChapters', label: totalChaptersColumnLabel(), defaultVisible: false, sortBy: 'chapters', widthClass: 'w-28', render: (book) => book.totalChapters ?? '-' },
  { id: 'rating', label: 'Rating', defaultVisible: true, sortBy: 'rating', widthClass: 'w-16', render: (book) => book.rating ?? '-' },
  { id: 'priority', label: 'Priority', defaultVisible: false, sortBy: 'priority', widthClass: 'w-20', render: (book) => book.priority ?? '-' },
  { id: 'created', label: 'Created', defaultVisible: false, sortBy: 'created', widthClass: 'w-40', render: (book) => formatDate(book.created) },
  { id: 'lastModified', label: 'Updated', defaultVisible: true, sortBy: 'lastModified', widthClass: 'w-40', render: (book) => formatDate(book.lastModified || book.created) },
  { id: 'genres', label: 'Genres', defaultVisible: false, widthClass: 'w-40', render: (book) => <Pills values={book.genres} /> },
  { id: 'tags', label: 'Tags', defaultVisible: true, widthClass: 'w-40', render: (book) => <Pills values={book.tags} /> },
  { id: 'links', label: 'Links', defaultVisible: false, widthClass: 'w-16', render: (book) => book.linksCount },
  { id: 'notes', label: 'Notes', defaultVisible: false, widthClass: 'w-56', render: (book) => truncate(book.notes) },
  { id: 'description', label: 'Description', defaultVisible: false, widthClass: 'w-56', render: (book) => truncate(book.description) },
]

const bookCardFields: ColumnDefinition<BookListItemDto>[] = [
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
  const {
    pageSize,
    query,
    requestQuery,
    setPageSize,
    setSkip,
    setSort: setSortParams,
    skip,
    sortBy,
    sortDirection,
    updateQuery,
  } = useBookListUrlState(searchParams, setSearchParams)
  const visibleColumns = getVisibleColumns(bookColumns, columnPreferences)
  const visibleCardFields = getVisibleColumns(bookCardFields, cardFieldPreferences)
  const booksQuery = useQuery({
    queryKey: ['books', skip, pageSize, requestQuery, sortBy, sortDirection],
    queryFn: () => api.getBooks({ skip, take: pageSize, query: requestQuery, sortBy, sortDirection }),
    placeholderData: keepPreviousData,
    staleTime: 30_000,
  })
  const summaryQuery = useQuery({
    queryKey: ['books-summary', requestQuery],
    queryFn: () => api.getBooksSummary({ query: requestQuery }),
    enabled: summaryOpen,
    staleTime: 30_000,
  })
  const cycleSortMutation = useMutation({
    mutationFn: ({ nextSortBy, currentSortDirection }: { nextSortBy: string; currentSortDirection: string | null }) => (
      api.getBooks({
        skip,
        take: pageSize,
        query: requestQuery,
        sortBy: nextSortBy,
        sortDirection: currentSortDirection ?? undefined,
        advanceCycle: true,
      })
    ),
    onSuccess: (result, variables) => {
      const resolvedSortDirection = getCyclicSortDirectionFromResult(result.data, variables.nextSortBy) ?? variables.currentSortDirection ?? sortDirection
      queryClient.setQueryData(
        ['books', skip, pageSize, requestQuery, variables.nextSortBy, resolvedSortDirection],
        result,
      )

      setSortParams(variables.nextSortBy, resolvedSortDirection)
    },
  })
  const exportMutation = useMutation({
    mutationFn: () => api.downloadBooksExport({ query: requestQuery, sortBy, sortDirection }),
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
  function setSort(nextSortBy: string) {
    if (isCyclicSort(nextSortBy)) {
      cycleSortMutation.mutate({
        nextSortBy,
        currentSortDirection: sortBy === nextSortBy ? sortDirection : null,
      })
      return
    }

    const nextDirection = getNextSortDirection(nextSortBy, sortBy, sortDirection)
    setSortParams(nextSortBy, nextDirection)
  }

  const total = booksQuery.data?.total ?? 0
  const pagination = useBookListPagination({
    dataLength: booksQuery.data?.data.length ?? 0,
    isFetching: booksQuery.isFetching,
    pageSize,
    setSkip,
    skip,
    total,
  })
  const scrollShortcuts = useBookListScrollShortcuts()
  const settingsColumns = viewMode === 'cards' ? bookCardFields : bookColumns
  const settingsPreferences = viewMode === 'cards' ? cardFieldPreferences : columnPreferences
  const setSettingsPreferences = viewMode === 'cards' ? setCardFieldPreferences : setColumnPreferences
  const settingsTitle = viewMode === 'cards' ? 'Visible card fields' : 'Visible columns'
  const settingsDescription = viewMode === 'cards' ? 'Choose which book fields appear on cards.' : 'Change table order and visibility.'
  const allowSettingsReorder = viewMode === 'table'

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
          analyticsHref={buildAnalyticsHref(requestQuery)}
          id="books-summary-panel"
          isError={summaryQuery.isError}
          isLoading={summaryQuery.isLoading}
          summary={summaryQuery.data}
        />
      ) : null}

      <BookAdvancedSearch value={query} onChange={updateQuery} />

      <section className="min-w-0 overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
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
          <BookDataTable
            columns={visibleColumns}
            emptyMessage="No books match the current filters."
            isCyclicSort={isCyclicSort}
            isLoading={booksQuery.isLoading}
            items={booksQuery.data?.data ?? []}
            renderActions={(book) => (
              <div className="flex justify-end gap-2">
                <Link className={secondaryButtonClass} to={`/books/${book.id}`}><Eye className="h-4 w-4" /></Link>
                <Link className={secondaryButtonClass} to={`/books/${book.id}/edit`}><Edit className="h-4 w-4" /></Link>
              </div>
            )}
            sortBy={sortBy}
            sortDirection={sortDirection}
            wrapperClassName="w-full overflow-x-auto"
            onSort={setSort}
          />
        ) : (
          <BookCardGrid
            books={booksQuery.data?.data ?? []}
            cardsPerRow={cardsPerRow}
            fields={visibleCardFields}
            isLoading={booksQuery.isLoading}
          />
        )}
        <BookListFooter
          activePageGapId={pagination.activePageGapId}
          canGoBack={pagination.canGoBack}
          canGoForward={pagination.canGoForward}
          currentPage={pagination.currentPage}
          pageSize={pageSize}
          setActivePageGapId={pagination.setActivePageGapId}
          setPageSize={setPageSize}
          skip={skip}
          total={total}
          totalPages={pagination.totalPages}
          visiblePages={pagination.visiblePages}
          onGoToPage={pagination.onGoToPage}
        />
      </section>
      <ImportBooksDialog open={importDialogOpen} onClose={() => setImportDialogOpen(false)} onImported={handleImportComplete} />
      <ScrollShortcutButtons
        showBackToTop={scrollShortcuts.showBackToTop}
        showGoDown={scrollShortcuts.showGoDown}
        onBackToTop={scrollShortcuts.scrollBackToTop}
        onGoDown={scrollShortcuts.scrollToPageBottom}
      />
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

export function BookAdvancedSearch({
  value,
  onChange,
}: {
  value: string
  onChange: (value: string) => void
}) {
  const [draftValue, setDraftValue] = useState(value)

  useEffect(() => {
    setDraftValue(value)
  }, [value])

  return (
    <section className="grid gap-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
      <label className="relative">
        <Search className="pointer-events-none absolute left-3 top-3 h-4 w-4 text-slate-400" />
        <input
          className={`${inputClass} w-full pl-9`}
          placeholder={'Search: returnee author:Toika title:"Lord of Mysteries" genre:fantasy,"slice of life" rating:>=8'}
          value={draftValue}
          onChange={(event) => {
            const nextValue = event.target.value
            setDraftValue(nextValue)
            onChange(nextValue)
          }}
        />
      </label>
      <p className="text-xs text-slate-500">
        Supports filters like <code>author:John</code>, <code>tag:favorite,"to read soon"</code>, <code>genre:fantasy,"slice of life"</code>, <code>rating:&gt;=8</code>, <code>rating:8</code>, <code>progress:&gt;=50</code>, <code>chapters:&lt;200</code>, <code>total:&gt;500</code>, <code>total-chapters:&gt;500</code>, and wildcard searches like <code>title:i*</code>.
      </p>
    </section>
  )
}

function BookSummaryPanel({
  analyticsHref,
  id,
  isError,
  isLoading,
  summary,
}: {
  analyticsHref: string
  id: string
  isError: boolean
  isLoading: boolean
  summary: BookSummaryDto | undefined
}) {
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

  return (
    <section className="grid gap-4 rounded-lg border border-slate-200 bg-white p-4 shadow-sm" id={id}>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-sm font-semibold text-slate-950">Library summary</h2>
          <p className="text-sm text-slate-500">Aggregated from all books matching the current filters.</p>
        </div>
        <Link className={`${secondaryButtonClass} ${topActionButtonSpacingClass}`} to={analyticsHref}>
          Open analytics
        </Link>
      </div>
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
        <SummaryMetricCard label="Total books" value={formatBookCount(summary.totalBooks)} />
        <SummaryMetricCard label="Rated" value={formatBookCount(summary.ratedBooks)} />
        <SummaryMetricCard label="Unrated" value={formatBookCount(summary.unratedBooks)} />
        <SummaryMetricCard label="Average rating" value={formatAverageRating(summary.averageRating)} />
        <SummaryMetricCard label="Current chapters" value={formatChapterCount(summary.currentChapters)} />
      </div>
      {summary.totalBooks === 0 ? (
        <div className="rounded-lg border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-600">
          No books match the current filters.
        </div>
      ) : (
        <div className="grid gap-4 rounded-xl border border-slate-200 bg-slate-50 p-4">
          <div className="grid gap-4 xl:grid-cols-2">
            <SummaryCompactSection
              title="Status distribution"
              items={summary.statusCounts.map((status) => ({ label: status.status, value: formatBookCount(status.count) }))}
            />
            <SummaryCompactTypeSection items={summary.typeCounts} />
          </div>
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

function SummaryCompactSection({ items, title }: { items: Array<{ label: string; value: string }>; title: string }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4">
      <div className="mb-3 text-sm font-semibold text-slate-950">{title}</div>
      <SummaryCountList items={items} />
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

function SummaryCompactTypeSection({ items }: { items: BookSummaryTypeCountDto[] }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4">
      <div className="mb-3 text-sm font-semibold text-slate-950">Book types</div>
      <SummaryTypeDetails items={items} />
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
            <div className="font-semibold text-slate-950">{item.type}</div>
            <div className="rounded-full bg-white px-2.5 py-1 text-xs font-semibold text-slate-700">{formatBookCount(item.bookCount)} books</div>
          </div>
          <div className="mt-2 text-sm text-slate-600">Current chapters: {formatChapterCount(item.currentChapters)}</div>
        </div>
      ))}
    </div>
  )
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

function BookCardGrid({
  books,
  cardsPerRow,
  fields,
  isLoading,
}: {
  books: BookListItemDto[]
  cardsPerRow: number
  fields: ColumnDefinition<BookListItemDto>[]
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
                preferredVariant="thumbnail"
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

function hasCardField(fields: ColumnDefinition<BookListItemDto>[], id: string) {
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

export function readCardsPerRow(storageKey: string) {
  const stored = Number(window.localStorage.getItem(storageKey) ?? 4)
  return normalizeCardsPerRow(stored)
}

function isCyclicSort(sortBy: string) {
  return sortBy === 'status' || sortBy === 'type'
}

function getCyclicSortDirectionFromResult(books: BookListItemDto[], sortBy: string) {
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

export function formatAverageRating(value?: number | null) {
  return value == null ? '-' : value.toFixed(1)
}

export function formatBookCount(value?: number | null) {
  return value == null ? '-' : new Intl.NumberFormat('en-US').format(value)
}

export function formatChapterCount(value?: number | null) {
  if (value == null) {
    return '-'
  }

  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 1 }).format(value)
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

function buildAnalyticsHref(query: string) {
  const params = new URLSearchParams()
  const analyticsFilters = extractAnalyticsDateFilters(query)
  if (analyticsFilters.query) {
    params.set('query', analyticsFilters.query)
  }
  if (analyticsFilters.from) {
    params.set('from', analyticsFilters.from)
  }
  if (analyticsFilters.to) {
    params.set('to', analyticsFilters.to)
  }

  const search = params.toString()
  return search ? `/analytics?${search}` : '/analytics'
}
