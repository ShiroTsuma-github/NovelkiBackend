import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Edit, Plus } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { api } from '@/api/client'
import type { AdminBookListItemDto, DictionaryMutationRequest } from '@/api/types'
import { HttpError } from '@/api/http'
import { buttonClass, inputClass, secondaryButtonClass } from '@/components/app/FormField'
import {
  BookAdvancedSearch,
  ColumnHeader,
  ColumnSettingsPopup,
  formatDate,
  formatProgress,
  getVisibleColumns,
  useColumnPreferences,
  type ColumnDefinition,
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
  { id: 'totalChapters', label: 'Chapters', defaultVisible: false, sortBy: 'chapters', widthClass: 'w-24', render: (book) => book.totalChapters ?? '-' },
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
      <div>
        <h1 className="text-2xl font-semibold text-slate-950">Admin panel</h1>
        <p className="text-sm text-slate-500">Global dictionaries and books from all users.</p>
      </div>

      <section className="grid gap-4 rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-base font-semibold text-slate-950">Add dictionary item</h2>
        <p className="text-sm text-slate-500">The description is shown as help when users choose a type, status, or genre.</p>
        <div className="grid gap-4 lg:grid-cols-3">
          <DictionaryCreateForm
            label="Status"
            mutationFn={api.createAdminStatus}
            queryKeys={['statuses']}
          />
          <DictionaryCreateForm
            label="Type"
            mutationFn={api.createAdminType}
            queryKeys={['types']}
          />
          <DictionaryCreateForm
            label="Genre"
            mutationFn={api.createAdminGenre}
            queryKeys={['genres']}
          />
        </div>
      </section>

      <section className="grid gap-4 rounded-lg border border-red-200 bg-white p-5 shadow-sm">
        <h2 className="text-base font-semibold text-slate-950">Purge user library</h2>
        <p className="text-sm text-slate-500">Deletes all books for the given owner id, then removes orphaned authors and that user&apos;s orphaned tags.</p>
        <div className="flex flex-wrap gap-2">
          <input
            className={`${inputClass} min-w-80`}
            placeholder="OwnerId"
            value={ownerIdToPurge}
            onChange={(event) => setOwnerIdToPurge(event.target.value)}
          />
          <button
            className="inline-flex min-h-10 items-center justify-center rounded-md bg-red-600 px-4 py-2 text-sm font-semibold text-white hover:bg-red-700 disabled:bg-red-300"
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
      </section>

      <BookAdvancedSearch value={query} onChange={updateQuery} />

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
        <div className="flex flex-wrap items-center justify-end gap-2 border-b border-slate-200 px-4 py-3">
          {adminBooksQuery.isFetching && !adminBooksQuery.isLoading ? (
            <span className="mr-auto text-xs font-medium text-slate-500">Searching...</span>
          ) : null}
          <ColumnSettingsPopup columns={adminBookColumns} preferences={columnPreferences} onChange={setColumnPreferences} />
        </div>
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
              {adminBooksQuery.isLoading ? (
                <tr><td className="px-4 py-8 text-center text-slate-500" colSpan={visibleColumns.length + 1}>Loading...</td></tr>
              ) : null}
              {adminBooksQuery.data?.data.map((book) => (
                <AdminBookRow book={book} columns={visibleColumns} key={book.id} />
              ))}
              {adminBooksQuery.data?.data.length === 0 ? (
                <tr><td className="px-4 py-8 text-center text-slate-500" colSpan={visibleColumns.length + 1}>No results.</td></tr>
              ) : null}
            </tbody>
          </table>
        </div>
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
      <ScrollShortcutButtons
        showBackToTop={scrollShortcuts.showBackToTop}
        showGoDown={scrollShortcuts.showGoDown}
        onBackToTop={scrollShortcuts.scrollBackToTop}
        onGoDown={scrollShortcuts.scrollToPageBottom}
      />
    </div>
  )
}

function DictionaryCreateForm({
  label,
  mutationFn,
  queryKeys,
}: {
  label: string
  mutationFn: (request: DictionaryMutationRequest) => Promise<unknown>
  queryKeys: string[]
}) {
  const queryClient = useQueryClient()
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const mutation = useMutation({
    mutationFn,
    onSuccess: async () => {
      setName('')
      setDescription('')
      await Promise.all(queryKeys.map((queryKey) => queryClient.invalidateQueries({ queryKey: [queryKey] })))
      toast.success(`${label} added.`)
    },
    onError: (error) => {
      toast.error(error instanceof HttpError ? error.apiError.detail : error.message)
    },
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    mutation.mutate({
      name: name.trim(),
      description: description.trim() || null,
    })
  }

  return (
    <form className="grid gap-3" onSubmit={handleSubmit}>
      <div className="text-sm font-semibold text-slate-700">{label}</div>
      <input className={inputClass} placeholder="Name" required value={name} onChange={(event) => setName(event.target.value)} />
      <input className={inputClass} placeholder="Help description" value={description} onChange={(event) => setDescription(event.target.value)} />
      <button className={buttonClass} disabled={mutation.isPending || !name.trim()} type="submit">
        <Plus className="h-4 w-4" />
        Add
      </button>
    </form>
  )
}

function AdminBookRow({ book, columns }: { book: AdminBookListItemDto; columns: ColumnDefinition<AdminBookListItemDto>[] }) {
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
        <div className="flex justify-end">
          <Link className={secondaryButtonClass} to={`/admin/books/${book.id}/edit`}><Edit className="h-4 w-4" /></Link>
        </div>
      </td>
    </tr>
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
