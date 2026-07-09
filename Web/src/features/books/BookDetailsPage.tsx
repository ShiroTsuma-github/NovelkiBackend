import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Edit, ExternalLink, RefreshCw, Trash2, Upload } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { api } from '@/api/client'
import { API_BASE_URL, HttpError, tokenStorageKey } from '@/api/http'
import type { BookDto } from '@/api/types'
import {
  buttonClass,
  inputClass,
  secondaryButtonClass,
} from '@/components/app/FormField'
import { formatProgress } from './BooksPage'
import { ProgressDialog } from './ProgressDialog'

export function BookDetailsPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const bookQuery = useQuery({
    queryKey: ['book', id],
    queryFn: () => api.getBook(id!),
    enabled: Boolean(id),
  })
  const typesQuery = useQuery({ queryKey: ['types'], queryFn: api.getTypes, staleTime: 300_000 })
  const statusesQuery = useQuery({ queryKey: ['statuses'], queryFn: api.getStatuses, staleTime: 300_000 })
  const genresQuery = useQuery({ queryKey: ['genres'], queryFn: api.getGenres, staleTime: 300_000 })
  const deleteMutation = useMutation({
    mutationFn: () => api.deleteBook(id!),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['books'] })
      toast.success('Book deleted.')
      navigate('/books', { replace: true })
    },
    onError: (error) => {
      toast.error(error instanceof HttpError ? error.apiError.detail : 'Failed to delete the book.')
    },
  })
  const refreshCoverMutation = useMutation({
    mutationFn: () => api.refreshBookCover(id!),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['book', id] }),
        queryClient.invalidateQueries({ queryKey: ['books'] }),
      ])
      toast.success('Cover search queued.')
    },
    onError: (error) => {
      toast.error(error instanceof HttpError ? error.apiError.detail : 'Failed to refresh cover.')
    },
  })
  const setCoverUrlMutation = useMutation({
    mutationFn: (imageUrl: string) => api.setBookCoverFromUrl(id!, imageUrl),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['book', id] }),
        queryClient.invalidateQueries({ queryKey: ['books'] }),
      ])
      toast.success('Cover saved.')
    },
    onError: (error) => {
      toast.error(error instanceof HttpError ? error.apiError.detail : 'Failed to save cover URL.')
    },
  })
  const uploadCoverMutation = useMutation({
    mutationFn: (file: File) => api.uploadBookCover(id!, file),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['book', id] }),
        queryClient.invalidateQueries({ queryKey: ['books'] }),
      ])
      toast.success('Cover uploaded.')
    },
    onError: (error) => {
      toast.error(error instanceof HttpError ? error.apiError.detail : 'Failed to upload cover.')
    },
  })

  if (bookQuery.isLoading) {
    return <div className="rounded-lg border border-slate-200 bg-white p-6 text-slate-500">Loading...</div>
  }

  if (!bookQuery.data) {
    return <div className="rounded-lg border border-slate-200 bg-white p-6 text-slate-500">Book not found.</div>
  }

  const book = bookQuery.data
  const typeDescription = typesQuery.data?.data.find((type) => type.name === book.contentType)?.description
  const statusDescription = statusesQuery.data?.data.find((status) => status.name === book.status)?.description
  const genreDescriptions = genresQuery.data?.data
    .filter((genre) => book.genres.includes(genre.name) && genre.description)
    .map((genre) => `${genre.name}: ${genre.description}`)

  return (
    <div className="grid gap-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Link className={secondaryButtonClass} to="/books">
          <ArrowLeft className="h-4 w-4" />
          List
        </Link>
        <div className="flex flex-wrap gap-2">
          <ProgressDialog book={book} />
          <Link className={buttonClass} to={`/books/${book.id}/edit`}>
            <Edit className="h-4 w-4" />
            Edit
          </Link>
          <button
            className="inline-flex min-h-10 items-center justify-center gap-2 rounded-md bg-red-600 px-4 py-2 text-sm font-semibold text-white hover:bg-red-700 disabled:bg-red-300"
            disabled={deleteMutation.isPending}
            type="button"
            onClick={() => deleteMutation.mutate()}
          >
            <Trash2 className="h-4 w-4" />
            Delete
          </button>
        </div>
      </div>

      <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <div className="grid gap-1">
          <h1 className="text-3xl font-semibold text-slate-950">{book.primaryTitle}</h1>
          <p className="text-sm text-slate-500">{book.author ?? 'Unknown author'}</p>
        </div>
        <div className="mt-5 grid gap-4 md:grid-cols-4">
          <Metric description={statusDescription} label="Status" value={book.status} />
          <Metric description={typeDescription} label="Type" value={book.contentType} />
          <Metric label="Progress" value={formatProgress(book)} />
          <Metric label="Rating" value={book.rating?.toString() ?? '-'} />
        </div>
      </section>

      <section className="grid gap-4 md:grid-cols-2">
        <CoverPanel
          book={book}
          refreshPending={refreshCoverMutation.isPending}
          setUrlPending={setCoverUrlMutation.isPending}
          uploadPending={uploadCoverMutation.isPending}
          onRefresh={() => refreshCoverMutation.mutate()}
          onSetUrl={(imageUrl) => setCoverUrlMutation.mutate(imageUrl)}
          onUpload={(file) => uploadCoverMutation.mutate(file)}
        />
        <Panel title="Alternative titles">
          <Pills values={book.alternativeTitles} empty="No aliases." />
        </Panel>
        <Panel title="Tags and genres">
          <div className="grid gap-3">
            <Pills values={book.tags} empty="No tags." />
            <Pills values={book.genres} empty="No genres." />
            {genreDescriptions?.length ? (
              <div className="grid gap-1 text-xs text-slate-500">
                {genreDescriptions.map((description) => <p key={description}>{description}</p>)}
              </div>
            ) : null}
          </div>
        </Panel>
        <Panel title="Links">
          <div className="grid gap-2">
            {book.links.length ? book.links.map((link) => (
              <a className="flex items-center justify-between rounded-md border border-slate-200 px-3 py-2 text-sm hover:bg-slate-50" href={link.url} key={link.id} rel="noreferrer" target="_blank">
                <span>{link.label || link.sourceType}</span>
                <ExternalLink className="h-4 w-4 text-slate-400" />
              </a>
            )) : <p className="text-sm text-slate-500">No links.</p>}
          </div>
        </Panel>
        <Panel title="Notes">
          <div className="grid gap-3 text-sm text-slate-700">
            <p>{book.comment || 'No comment.'}</p>
            <p>{book.notes || 'No notes.'}</p>
          </div>
        </Panel>
      </section>
    </div>
  )
}

function CoverPanel({
  book,
  refreshPending,
  setUrlPending,
  uploadPending,
  onRefresh,
  onSetUrl,
  onUpload,
}: {
  book: BookDto
  refreshPending: boolean
  setUrlPending: boolean
  uploadPending: boolean
  onRefresh: () => void
  onSetUrl: (imageUrl: string) => void
  onUpload: (file: File) => void
}) {
  const [imageUrl, setImageUrl] = useState('')

  function submitUrl(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const trimmed = imageUrl.trim()
    if (trimmed) {
      onSetUrl(trimmed)
      setImageUrl('')
    }
  }

  return (
    <Panel title="Cover">
      <div className="grid gap-3">
        <CoverImage book={book} />
        <div className="grid gap-1 text-sm text-slate-600">
          <p>Status: <span className="font-medium text-slate-900">{book.cover?.status ?? 'Missing'}</span></p>
          {book.cover?.source ? <p>Source: {book.cover.source}</p> : null}
          {book.cover?.failureReason ? <p className="text-red-600">{book.cover.failureReason}</p> : null}
        </div>
        <form className="grid gap-2" onSubmit={submitUrl}>
          <input
            className={inputClass}
            placeholder="Paste image URL"
            value={imageUrl}
            onChange={(event) => setImageUrl(event.target.value)}
          />
          <div className="flex flex-wrap gap-2">
            <button className={buttonClass} disabled={setUrlPending || !imageUrl.trim()} type="submit">
              Save URL
            </button>
            <label className={secondaryButtonClass}>
              <Upload className="h-4 w-4" />
              Upload
              <input
                accept="image/jpeg,image/png,image/webp"
                className="sr-only"
                disabled={uploadPending}
                type="file"
                onChange={(event) => {
                  const file = event.target.files?.[0]
                  if (file) {
                    onUpload(file)
                    event.target.value = ''
                  }
                }}
              />
            </label>
            <button className={secondaryButtonClass} disabled={refreshPending} type="button" onClick={onRefresh}>
              <RefreshCw className="h-4 w-4" />
              Retry
            </button>
          </div>
        </form>
      </div>
    </Panel>
  )
}

function CoverImage({ book }: { book: BookDto }) {
  const [blobUrl, setBlobUrl] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    let objectUrl: string | null = null

    async function loadImage() {
      if (!book.cover?.imageUrl) {
        setBlobUrl(null)
        return
      }

      const apiOrigin = API_BASE_URL.replace(/\/api\/v1\/?$/, '')
      const token = localStorage.getItem(tokenStorageKey)
      const response = await fetch(`${apiOrigin}${book.cover.imageUrl}`, {
        headers: token ? { Authorization: `Bearer ${token}` } : undefined,
      })
      if (!response.ok) {
        return
      }

      objectUrl = URL.createObjectURL(await response.blob())
      if (active) {
        setBlobUrl(objectUrl)
      }
    }

    loadImage()

    return () => {
      active = false
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl)
      }
    }
  }, [book.cover?.imageUrl, book.cover?.lastAttemptAt])

  if (!blobUrl) {
    return <div className="flex aspect-[2/3] max-h-72 items-center justify-center rounded-md border border-dashed border-slate-300 bg-slate-50 text-sm text-slate-500">No cover</div>
  }

  return <img alt={book.primaryTitle} className="max-h-72 rounded-md border border-slate-200 object-cover" src={blobUrl} />
}

function Metric({ description, label, value }: { description?: string | null; label: string; value: string }) {
  return (
    <div className="rounded-md border border-slate-200 bg-slate-50 p-3">
      <div className="text-xs uppercase tracking-wide text-slate-500">{label}</div>
      <div className="mt-1 font-semibold text-slate-950">{value}</div>
      {description ? <div className="mt-1 text-xs font-normal text-slate-500">{description}</div> : null}
    </div>
  )
}

function Panel({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
      <h2 className="mb-3 text-base font-semibold text-slate-950">{title}</h2>
      {children}
    </section>
  )
}

function Pills({ values, empty }: { values: string[]; empty: string }) {
  if (!values.length) {
    return <p className="text-sm text-slate-500">{empty}</p>
  }
  return (
    <div className="flex flex-wrap gap-2">
      {values.map((value) => (
        <span className="rounded bg-slate-100 px-2 py-1 text-xs font-medium text-slate-700" key={value}>{value}</span>
      ))}
    </div>
  )
}
