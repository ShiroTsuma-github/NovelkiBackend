import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Edit, ExternalLink, RefreshCw, Trash2 } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { api } from '@/api/client'
import { HttpError } from '@/api/http'
import { buttonClass, secondaryButtonClass } from '@/components/app/FormField'
import { BookCoverArtwork, CoverLightbox, useResolvedCoverImage } from './BookCoverSection'
import { formatProgress } from './BooksPage'
import { ProgressDialog } from './ProgressDialog'

export function BookDetailsPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [previewOpen, setPreviewOpen] = useState(false)
  const bookQuery = useQuery({
    queryKey: ['book', id],
    queryFn: () => api.getBook(id!),
    enabled: Boolean(id),
  })
  const previewImageUrl = useResolvedCoverImage(bookQuery.data?.cover)
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
  const shouldOfferRefresh = book.cover?.source !== 'ManualUpload' && book.cover?.source !== 'ManualUrl'
  const refreshLabel = book.cover?.imageUrl ? 'Search again' : 'Search cover'
  const displayCoverStatus = getDisplayCoverStatus(book.cover?.status, book.cover?.failureReason)
  const displayCoverFailure = getDisplayCoverFailure(book.cover?.failureReason)
  const coverFacts = [`Status: ${displayCoverStatus}`]
  if (book.cover?.source) {
    coverFacts.push(`Source: ${book.cover.source}`)
  }
  if (displayCoverFailure) {
    coverFacts.push(displayCoverFailure)
  }

  return (
    <>
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

        <section className="rounded-[1.75rem] border border-slate-200 bg-white p-5 shadow-sm">
          <div className="flex flex-col gap-6">
            <div className="flex flex-col gap-6 lg:flex-row lg:items-start">
              <div className="flex-none lg:w-[220px]">
                <BookCoverArtwork
                  className="w-full lg:w-[220px]"
                  cover={book.cover}
                  emptyLabel="No cover in library yet."
                  interactive
                  title={book.primaryTitle}
                  onClick={() => setPreviewOpen(true)}
                />
              </div>
              <div className="min-w-0 flex-1">
                <div className="flex min-w-0 flex-col gap-5">
                  <div className="flex min-w-0 flex-col gap-2">
                    <div className="text-xs font-semibold uppercase tracking-[0.26em] text-slate-400">{book.contentType}</div>
                    <h1 className="text-3xl font-semibold tracking-tight text-slate-950 md:text-4xl">{book.primaryTitle}</h1>
                    <p className="text-base text-slate-600">{book.author ?? 'Unknown author'}</p>
                    {book.description ? (
                      <p className="max-w-4xl text-sm leading-6 text-slate-600">{book.description}</p>
                    ) : null}
                  </div>
                  <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                    <Metric description={statusDescription} label="Status" value={book.status} />
                    <Metric description={typeDescription} label="Type" value={book.contentType} />
                    <Metric label="Progress" value={formatProgress(book)} />
                    <Metric label="Rating" value={book.rating?.toString() ?? '-'} />
                  </div>
                </div>
              </div>
            </div>
            <div className="flex flex-col gap-3 lg:flex-row lg:items-start">
              <div className="flex-none lg:w-[220px]">
                <div className="grid gap-1 rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-600">
                  {coverFacts.map((fact) => (
                    <p className={fact === displayCoverFailure ? 'text-red-600' : ''} key={fact}>{fact}</p>
                  ))}
                </div>
                {shouldOfferRefresh ? (
                  <button
                    className={`${secondaryButtonClass} mt-3 w-full justify-center`}
                    disabled={refreshCoverMutation.isPending}
                    type="button"
                    onClick={() => refreshCoverMutation.mutate()}
                  >
                    <RefreshCw className="h-4 w-4" />
                    {refreshLabel}
                  </button>
                ) : null}
              </div>
              <div className="hidden min-w-0 flex-1 lg:block" />
            </div>
          </div>
        </section>

        <section className="grid gap-4 md:grid-cols-2">
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

      <CoverLightbox
        emptyLabel="No cover has been saved for this book yet."
        imageUrl={previewImageUrl}
        open={previewOpen}
        title={book.primaryTitle}
        onClose={() => setPreviewOpen(false)}
      />
    </>
  )
}

function Metric({ description, label, value }: { description?: string | null; label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-slate-50 p-4">
      <div className="text-xs uppercase tracking-wide text-slate-500">{label}</div>
      <div className="mt-1 font-semibold text-slate-950">{value}</div>
      {description ? <div className="mt-1 text-xs font-normal text-slate-500">{description}</div> : null}
    </div>
  )
}

function Panel({ title, children }: { title: string; children: ReactNode }) {
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

function getDisplayCoverStatus(status?: string | null, failureReason?: string | null) {
  if (status === 'Failed' && isProviderResponseFailure(failureReason)) {
    return 'Not found'
  }

  return status ?? 'Missing'
}

function getDisplayCoverFailure(failureReason?: string | null) {
  if (!failureReason) {
    return null
  }

  if (isProviderResponseFailure(failureReason)) {
    return 'No valid cover response was found from the configured providers.'
  }

  return failureReason
}

function isProviderResponseFailure(failureReason?: string | null) {
  if (!failureReason) {
    return false
  }

  return failureReason.includes("invalid start of a value") || failureReason.includes("'<'")
}
