import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Edit } from 'lucide-react'
import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { api } from '@/api/client'
import type { AdminBookListItemDto } from '@/api/types'
import { HttpError } from '@/api/http'
import { buttonVariants, PageHeader, Surface } from '@/components/app/DesignSystem'
import { inputClass, secondaryButtonClass } from '@/components/app/FormField'
import { AdminMetadataManager } from './AdminMetadataManager'
import { BookDataTable } from '@/features/books/BookDataTable'
import {
  ColumnSettingsPopup,
  getVisibleColumns,
  totalChaptersColumnLabel,
  useColumnPreferences,
  type ColumnDefinition,
} from '@/features/books/bookListColumns'
import {
  BookAdvancedSearch,
  formatDate,
  formatProgress,
} from '@/features/books/BooksPage'
import {
  BookListFooter,
  getNextSortDirection,
  ScrollShortcutButtons,
  useBookListPagination,
  useBookListScrollShortcuts,
  useBookListUrlState,
} from '@/features/books/BookListShared'

const adminColumnsStorageKey = 'novelki.adminBooks.columns.v1'

const adminBookColumns: ColumnDefinition<AdminBookListItemDto>[] = [
  { id: 'id', label: 'Id', defaultVisible: false, widthClass: 'w-36', render: (book) => <span className="font-mono text-xs">{book.id}</span> },
  { id: 'title', label: 'Title', defaultVisible: true, sortBy: 'title', widthClass: 'w-[24%]', render: (book) => <span className="block truncate font-medium text-slate-950">{book.primaryTitle}</span> },
  { id: 'ownerId', label: 'OwnerId', defaultVisible: true, sortBy: 'owner', widthClass: 'w-[15%]', render: (book) => <span className="font-mono text-xs">{book.ownerId}</span> },
  { id: 'ownerUsername', label: 'Username', defaultVisible: false, widthClass: 'w-[12%]', render: (book) => <span className="block truncate">{book.ownerUsername ?? '-'}</span> },
  { id: 'ownerEmail', label: 'Email', defaultVisible: false, widthClass: 'w-[16%]', render: (book) => <span className="block truncate">{book.ownerEmail ?? '-'}</span> },
  { id: 'author', label: 'Author', defaultVisible: true, sortBy: 'author', widthClass: 'w-[11%]', render: (book) => <span className="block truncate">{book.author ?? '-'}</span> },
  { id: 'status', label: 'Status', defaultVisible: true, sortBy: 'status', widthClass: 'w-20', render: (book) => book.status },
  { id: 'type', label: 'Type', defaultVisible: true, sortBy: 'type', widthClass: 'w-20', render: (book) => book.contentType },
  { id: 'progress', label: 'Progress', defaultVisible: true, sortBy: 'progress', widthClass: 'w-24', render: formatProgress },
  { id: 'totalChapters', label: totalChaptersColumnLabel(), defaultVisible: false, sortBy: 'chapters', widthClass: 'w-28', render: (book) => book.totalChapters ?? '-' },
  { id: 'rating', label: 'Rating', defaultVisible: true, sortBy: 'rating', widthClass: 'w-16', render: (book) => book.rating ?? '-' },
  { id: 'priority', label: 'Priority', defaultVisible: false, sortBy: 'priority', widthClass: 'w-20', render: (book) => book.priority ?? '-' },
  { id: 'created', label: 'Created', defaultVisible: false, sortBy: 'created', widthClass: 'w-36', render: (book) => formatDate(book.created) },
  { id: 'lastModified', label: 'Updated', defaultVisible: true, sortBy: 'lastModified', widthClass: 'w-32', render: (book) => formatDate(book.lastModified || book.created) },
  { id: 'genres', label: 'Genres', defaultVisible: false, widthClass: 'w-[10%]', render: (book) => formatList(book.genres) },
  { id: 'tags', label: 'Tags', defaultVisible: false, widthClass: 'w-[10%]', render: (book) => formatList(book.tags) },
  { id: 'links', label: 'Links', defaultVisible: false, widthClass: 'w-16', render: (book) => book.linksCount },
  { id: 'notes', label: 'Notes', defaultVisible: false, widthClass: 'w-[16%]', render: (book) => truncate(book.notes) },
  { id: 'description', label: 'Description', defaultVisible: false, widthClass: 'w-[16%]', render: (book) => truncate(book.description) },
]

export function AdminPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const [columnPreferences, setColumnPreferences] = useColumnPreferences(adminColumnsStorageKey, adminBookColumns)
  const [ownerIdToPurge, setOwnerIdToPurge] = useState('')
  const queryClient = useQueryClient()
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
  const visibleColumns = getVisibleColumns(adminBookColumns, columnPreferences)
  const adminBooksQuery = useQuery({
    queryKey: ['adminBooks', skip, pageSize, requestQuery, sortBy, sortDirection],
    queryFn: () => api.getAdminBooks({ skip, take: pageSize, query: requestQuery, sortBy, sortDirection }),
    placeholderData: keepPreviousData,
    staleTime: 30_000,
  })
  const total = adminBooksQuery.data?.total ?? 0
  const pagination = useBookListPagination({
    dataLength: adminBooksQuery.data?.data.length ?? 0,
    isFetching: adminBooksQuery.isFetching,
    pageSize,
    setSkip,
    skip,
    total,
  })
  const scrollShortcuts = useBookListScrollShortcuts()
  const purgeMutation = useMutation({
    mutationFn: (ownerId: string) => api.deleteAdminBooksByOwner(ownerId),
    onSuccess: async (result) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['adminBooks'] }),
        queryClient.invalidateQueries({ queryKey: ['books'] }),
      ])
      toast.success(`Deleted ${result.deletedBooks} books, ${result.deletedAuthors} authors, ${result.deletedTags} tags.`)
      setOwnerIdToPurge('')
    },
    onError: (error) => {
      toast.error(error instanceof HttpError ? error.apiError.detail : error.message)
    },
  })

  function setSort(nextSortBy: string) {
    setSortParams(nextSortBy, getNextSortDirection(nextSortBy, sortBy, sortDirection))
  }

  return (
    <div className="grid gap-5">
      <PageHeader
        description="Global dictionaries and books from all users."
        eyebrow="System workspace"
        title="Admin panel"
      />

      <AdminMetadataManager />

      <Surface className="grid gap-4 p-5" tone="danger">
        <h2 className="ui-panel-title text-inherit">Purge user library</h2>
        <p className="text-sm text-inherit opacity-80">Deletes all books for the given owner id, then removes orphaned authors and that user&apos;s orphaned tags.</p>
        <div className="flex flex-wrap gap-2">
          <input
            className={`${inputClass} min-w-80`}
            placeholder="OwnerId"
            value={ownerIdToPurge}
            onChange={(event) => setOwnerIdToPurge(event.target.value)}
          />
          <button
            className={buttonVariants.destructive}
            disabled={purgeMutation.isPending || !ownerIdToPurge.trim()}
            type="button"
            onClick={() => {
              const ownerId = ownerIdToPurge.trim()
              if (!window.confirm(`Delete all books for owner ${ownerId}? This also removes orphaned authors and tags.`)) {
                return
              }

              purgeMutation.mutate(ownerId)
            }}
          >
            {purgeMutation.isPending ? 'Purging...' : 'Delete all by owner id'}
          </button>
        </div>
      </Surface>

      <BookAdvancedSearch value={query} onChange={updateQuery} />

      <Surface className="overflow-hidden">
        <div className="flex flex-wrap items-center justify-end gap-2 border-b border-slate-200 px-4 py-3">
          {adminBooksQuery.isFetching && !adminBooksQuery.isLoading ? (
            <span className="mr-auto text-xs font-medium text-slate-500">Searching...</span>
          ) : null}
          <ColumnSettingsPopup columns={adminBookColumns} preferences={columnPreferences} onChange={setColumnPreferences} />
        </div>
        <BookDataTable
          columns={visibleColumns}
          emptyMessage="No results."
          isLoading={adminBooksQuery.isLoading}
          items={adminBooksQuery.data?.data ?? []}
          renderActions={(book) => (
            <div className="flex justify-end">
              <Link aria-label={`Edit ${book.primaryTitle}`} className={`${secondaryButtonClass} ui-icon-button`} to={`/admin/books/${book.id}/edit`}><Edit className="h-4 w-4" /></Link>
            </div>
          )}
          sortBy={sortBy}
          sortDirection={sortDirection}
          onSort={setSort}
        />
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
      </Surface>
      <ScrollShortcutButtons
        showBackToTop={scrollShortcuts.showBackToTop}
        showGoDown={scrollShortcuts.showGoDown}
        onBackToTop={scrollShortcuts.scrollBackToTop}
        onGoDown={scrollShortcuts.scrollToPageBottom}
      />
    </div>
  )
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
