import { useQuery } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, ChevronsUpDown, Edit, Eye, Plus, Search } from 'lucide-react'
import { Link, useSearchParams } from 'react-router-dom'
import { api } from '@/api/client'
import type { BookDto } from '@/api/types'
import {
  buttonClass,
  inputClass,
  secondaryButtonClass,
} from '@/components/app/FormField'
import { buildBookQuery, emptyFilters, type BookFilters } from './queryBuilder'

const pageSizeOptions = [20, 50, 100, 500]
type SortDirection = 'asc' | 'desc'

export function BooksPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const skip = Number(searchParams.get('skip') ?? 0)
  const pageSize = readPageSize(searchParams)
  const sortBy = searchParams.get('sortBy') ?? 'title'
  const sortDirection = readSortDirection(searchParams)
  const filters = readFilters(searchParams)
  const query = buildBookQuery(filters)
  const booksQuery = useQuery({
    queryKey: ['books', skip, pageSize, query, sortBy, sortDirection],
    queryFn: () => api.getBooks({ skip, take: pageSize, query, sortBy, sortDirection }),
  })

  function updateFilter(key: keyof BookFilters, value: string) {
    const next = new URLSearchParams(searchParams)
    if (value) {
      next.set(key, value)
    } else {
      next.delete(key)
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
    const nextDirection = sortBy === nextSortBy && sortDirection === 'asc' ? 'desc' : 'asc'
    next.set('sortBy', nextSortBy)
    next.set('sortDirection', nextDirection)
    next.delete('skip')
    setSearchParams(next)
  }

  const total = booksQuery.data?.total ?? 0
  const canGoBack = skip > 0
  const canGoForward = skip + pageSize < total

  return (
    <div className="grid gap-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-slate-950">Książki</h1>
          <p className="text-sm text-slate-500">Lista, wyszukiwanie i szybka nawigacja po bibliotece.</p>
        </div>
        <Link className={buttonClass} to="/books/new">
          <Plus className="h-4 w-4" />
          Dodaj książkę
        </Link>
      </div>

      <section className="grid gap-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <div className="grid gap-3 md:grid-cols-[2fr_1fr_1fr_1fr]">
          <label className="relative">
            <Search className="pointer-events-none absolute left-3 top-3 h-4 w-4 text-slate-400" />
            <input
              className={`${inputClass} w-full pl-9`}
              placeholder="Szukaj po tytule lub autorze"
              value={filters.text}
              onChange={(event) => updateFilter('text', event.target.value)}
            />
          </label>
          <input className={inputClass} placeholder="Tag" value={filters.tag} onChange={(event) => updateFilter('tag', event.target.value)} />
          <input className={inputClass} placeholder="Autor" value={filters.author} onChange={(event) => updateFilter('author', event.target.value)} />
          <input className={inputClass} placeholder="Ocena min." type="number" value={filters.ratingMin} onChange={(event) => updateFilter('ratingMin', event.target.value)} />
        </div>
        <div className="grid gap-3 md:grid-cols-5">
          <input className={inputClass} placeholder="Tytuł" value={filters.title} onChange={(event) => updateFilter('title', event.target.value)} />
          <input className={inputClass} placeholder="Gatunek" value={filters.genre} onChange={(event) => updateFilter('genre', event.target.value)} />
          <input className={inputClass} placeholder="Status" value={filters.status} onChange={(event) => updateFilter('status', event.target.value)} />
          <input className={inputClass} placeholder="Typ" value={filters.type} onChange={(event) => updateFilter('type', event.target.value)} />
          <input className={inputClass} placeholder="Priorytet" type="number" value={filters.priority} onChange={(event) => updateFilter('priority', event.target.value)} />
        </div>
        {query ? <p className="text-xs text-slate-500">Backend query: <code>{query}</code></p> : null}
      </section>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[900px] border-collapse text-left text-sm">
            <thead className="bg-slate-100 text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <SortableHeader activeDirection={sortBy === 'title' ? sortDirection : null} label="Tytuł" sortBy="title" onSort={setSort} />
                <SortableHeader activeDirection={sortBy === 'author' ? sortDirection : null} label="Autor" sortBy="author" onSort={setSort} />
                <SortableHeader activeDirection={sortBy === 'status' ? sortDirection : null} label="Status" sortBy="status" onSort={setSort} />
                <SortableHeader activeDirection={sortBy === 'type' ? sortDirection : null} label="Typ" sortBy="type" onSort={setSort} />
                <SortableHeader activeDirection={sortBy === 'progress' ? sortDirection : null} label="Progres" sortBy="progress" onSort={setSort} />
                <SortableHeader activeDirection={sortBy === 'rating' ? sortDirection : null} label="Ocena" sortBy="rating" onSort={setSort} />
                <th className="px-4 py-3">Tagi</th>
                <th className="px-4 py-3"></th>
              </tr>
            </thead>
            <tbody>
              {booksQuery.isLoading ? (
                <tr><td className="px-4 py-8 text-center text-slate-500" colSpan={8}>Ładowanie...</td></tr>
              ) : null}
              {booksQuery.data?.data.map((book) => (
                <BookRow book={book} key={book.id} />
              ))}
              {booksQuery.data?.data.length === 0 ? (
                <tr><td className="px-4 py-8 text-center text-slate-500" colSpan={8}>Brak książek dla aktualnych filtrów.</td></tr>
              ) : null}
            </tbody>
          </table>
        </div>
        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 px-4 py-3 text-sm text-slate-600">
          <span>{total ? `${skip + 1}-${Math.min(skip + pageSize, total)} z ${total}` : '0 wyników'}</span>
          <div className="flex items-center gap-3">
            <label className="flex items-center gap-2">
              <span>Na stronę</span>
              <select className={`${inputClass} h-10 w-24`} value={pageSize} onChange={(event) => setPageSize(event.target.value)}>
                {pageSizeOptions.map((option) => <option key={option} value={option}>{option}</option>)}
              </select>
            </label>
            <button className={secondaryButtonClass} disabled={!canGoBack} type="button" onClick={() => setSkip(skip - pageSize)}>Poprzednie</button>
            <button className={secondaryButtonClass} disabled={!canGoForward} type="button" onClick={() => setSkip(skip + pageSize)}>Następne</button>
          </div>
        </div>
      </section>
    </div>
  )
}

function SortableHeader({
  activeDirection,
  label,
  sortBy,
  onSort,
}: {
  activeDirection: SortDirection | null
  label: string
  sortBy: string
  onSort: (sortBy: string) => void
}) {
  const Icon = activeDirection === 'asc' ? ArrowUp : activeDirection === 'desc' ? ArrowDown : ChevronsUpDown

  return (
    <th className="px-4 py-3">
      <button
        className="inline-flex h-8 items-center gap-1 rounded-md px-2 text-xs font-semibold uppercase tracking-wide text-slate-500 hover:bg-slate-200 hover:text-slate-950"
        type="button"
        onClick={() => onSort(sortBy)}
      >
        {label}
        <Icon className="h-3.5 w-3.5" />
      </button>
    </th>
  )
}

function BookRow({ book }: { book: BookDto }) {
  return (
    <tr className="border-t border-slate-100 hover:bg-slate-50">
      <td className="px-4 py-3 font-medium text-slate-950">{book.primaryTitle}</td>
      <td className="px-4 py-3 text-slate-600">{book.author ?? '-'}</td>
      <td className="px-4 py-3 text-slate-600">{book.status}</td>
      <td className="px-4 py-3 text-slate-600">{book.contentType}</td>
      <td className="px-4 py-3 text-slate-600">{formatProgress(book)}</td>
      <td className="px-4 py-3 text-slate-600">{book.rating ?? '-'}</td>
      <td className="px-4 py-3">
        <div className="flex flex-wrap gap-1">
          {book.tags.slice(0, 3).map((tag) => (
            <span className="rounded bg-slate-100 px-2 py-1 text-xs text-slate-600" key={tag}>{tag}</span>
          ))}
        </div>
      </td>
      <td className="px-4 py-3">
        <div className="flex justify-end gap-2">
          <Link className={secondaryButtonClass} to={`/books/${book.id}`}><Eye className="h-4 w-4" /></Link>
          <Link className={secondaryButtonClass} to={`/books/${book.id}/edit`}><Edit className="h-4 w-4" /></Link>
        </div>
      </td>
    </tr>
  )
}

function readFilters(searchParams: URLSearchParams): BookFilters {
  return {
    ...emptyFilters,
    text: searchParams.get('text') ?? '',
    title: searchParams.get('title') ?? '',
    author: searchParams.get('author') ?? '',
    tag: searchParams.get('tag') ?? '',
    genre: searchParams.get('genre') ?? '',
    status: searchParams.get('status') ?? '',
    type: searchParams.get('type') ?? '',
    ratingMin: searchParams.get('ratingMin') ?? '',
    priority: searchParams.get('priority') ?? '',
  }
}

function readPageSize(searchParams: URLSearchParams) {
  const value = Number(searchParams.get('take') ?? 20)
  return pageSizeOptions.includes(value) ? value : 20
}

function readSortDirection(searchParams: URLSearchParams): SortDirection {
  return searchParams.get('sortDirection') === 'desc' ? 'desc' : 'asc'
}

export function formatProgress(book: BookDto) {
  const current = book.currentChapterLabel || book.currentChapterNumber
  if (!current && !book.totalChapters) {
    return '-'
  }
  return `${current ?? '?'}${book.totalChapters ? ` / ${book.totalChapters}` : ''}`
}
