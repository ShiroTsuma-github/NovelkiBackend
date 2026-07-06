import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Edit, Plus } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { api } from '@/api/client'
import type { AdminBookDto, DictionaryMutationRequest } from '@/api/types'
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

const pageSizeOptions = [20, 50, 100, 500]
const adminColumnsStorageKey = 'novelki.adminBooks.columns.v1'
type SortDirection = 'asc' | 'desc'

const adminBookColumns: ColumnDefinition<AdminBookDto>[] = [
  { id: 'id', label: 'Id', defaultVisible: false, render: (book) => <span className="font-mono text-xs">{book.id}</span> },
  { id: 'title', label: 'Title', defaultVisible: true, sortBy: 'title', render: (book) => <span className="font-medium text-slate-950">{book.primaryTitle}</span> },
  { id: 'ownerId', label: 'OwnerId', defaultVisible: true, sortBy: 'owner', render: (book) => <span className="font-mono text-xs">{book.ownerId}</span> },
  { id: 'author', label: 'Author', defaultVisible: true, sortBy: 'author', render: (book) => book.author ?? '-' },
  { id: 'status', label: 'Status', defaultVisible: true, sortBy: 'status', render: (book) => book.status },
  { id: 'type', label: 'Type', defaultVisible: true, sortBy: 'type', render: (book) => book.contentType },
  { id: 'progress', label: 'Progress', defaultVisible: true, sortBy: 'progress', render: formatProgress },
  { id: 'rating', label: 'Rating', defaultVisible: true, sortBy: 'rating', render: (book) => book.rating ?? '-' },
  { id: 'priority', label: 'Priority', defaultVisible: false, sortBy: 'priority', render: (book) => book.priority ?? '-' },
  { id: 'created', label: 'Created', defaultVisible: false, sortBy: 'created', render: (book) => formatDate(book.created) },
  { id: 'lastModified', label: 'Updated', defaultVisible: true, sortBy: 'lastModified', render: (book) => formatDate(book.lastModified || book.created) },
  { id: 'genres', label: 'Genres', defaultVisible: false, render: (book) => formatList(book.genres) },
  { id: 'tags', label: 'Tags', defaultVisible: false, render: (book) => formatList(book.tags) },
  { id: 'links', label: 'Links', defaultVisible: false, render: (book) => book.links.length },
  { id: 'notes', label: 'Notes', defaultVisible: false, render: (book) => truncate(book.notes) },
  { id: 'description', label: 'Description', defaultVisible: false, render: (book) => truncate(book.description) },
]

export function AdminPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const [columnPreferences, setColumnPreferences] = useColumnPreferences(adminColumnsStorageKey, adminBookColumns)
  const skip = Number(searchParams.get('skip') ?? 0)
  const pageSize = readPageSize(searchParams)
  const sortBy = searchParams.get('sortBy') ?? 'lastModified'
  const sortDirection = readSortDirection(searchParams)
  const query = searchParams.get('query') ?? ''
  const visibleColumns = getVisibleColumns(adminBookColumns, columnPreferences)
  const adminBooksQuery = useQuery({
    queryKey: ['adminBooks', skip, pageSize, query, sortBy, sortDirection],
    queryFn: () => api.getAdminBooks({ skip, take: pageSize, query, sortBy, sortDirection }),
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

  const total = adminBooksQuery.data?.total ?? 0

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

      <BookAdvancedSearch value={query} onChange={updateQuery} />

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[1000px] border-collapse text-left text-sm">
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
                <th className="relative px-4 py-3 text-right">
                  <ColumnSettingsPopup columns={adminBookColumns} preferences={columnPreferences} onChange={setColumnPreferences} />
                </th>
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
        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 px-4 py-3 text-sm text-slate-600">
          <span>{total ? `${skip + 1}-${Math.min(skip + pageSize, total)} of ${total}` : '0 results'}</span>
          <div className="flex items-center gap-3">
            <label className="flex items-center gap-2">
              <span>Per page</span>
              <select className={`${inputClass} h-10 w-24`} value={pageSize} onChange={(event) => setPageSize(event.target.value)}>
                {pageSizeOptions.map((option) => <option key={option} value={option}>{option}</option>)}
              </select>
            </label>
            <button className={secondaryButtonClass} disabled={skip <= 0} type="button" onClick={() => setSkip(skip - pageSize)}>Previous</button>
            <button className={secondaryButtonClass} disabled={skip + pageSize >= total} type="button" onClick={() => setSkip(skip + pageSize)}>Next</button>
          </div>
        </div>
      </section>
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

function AdminBookRow({ book, columns }: { book: AdminBookDto; columns: ColumnDefinition<AdminBookDto>[] }) {
  return (
    <tr className="border-t border-slate-100 hover:bg-slate-50">
      {columns.map((column) => (
        <td className="px-4 py-3 text-slate-600" key={column.id}>{column.render(book)}</td>
      ))}
      <td className="px-4 py-3">
        <div className="flex justify-end">
          <Link className={secondaryButtonClass} to={`/admin/books/${book.id}/edit`}><Edit className="h-4 w-4" /></Link>
        </div>
      </td>
    </tr>
  )
}

function readPageSize(searchParams: URLSearchParams) {
  const value = Number(searchParams.get('take') ?? 20)
  return pageSizeOptions.includes(value) ? value : 20
}

function readSortDirection(searchParams: URLSearchParams): SortDirection {
  return searchParams.get('sortDirection') === 'asc' ? 'asc' : 'desc'
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
