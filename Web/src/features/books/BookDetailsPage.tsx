import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Edit, ExternalLink, Star, Trash2 } from 'lucide-react'
import { useEffect, useRef, useState, type CSSProperties, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { api } from '@/api/client'
import type { BookDto, BookLinkDto } from '@/api/types'
import { HttpError } from '@/api/http'
import { buttonClass, secondaryButtonClass } from '@/components/app/FormField'
import { BookCoverArtwork, CoverLightbox, useResolvedCoverImage } from './BookCoverSection'
import { ProgressDialog } from './ProgressDialog'

export function BookDetailsPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [previewOpen, setPreviewOpen] = useState(false)
  const [deleteConfirmOpen, setDeleteConfirmOpen] = useState(false)
  const [descriptionExpanded, setDescriptionExpanded] = useState(false)
  const [descriptionOverflowing, setDescriptionOverflowing] = useState(false)
  const descriptionRef = useRef<HTMLParagraphElement | null>(null)
  const bookQuery = useQuery({
    queryKey: ['book', id],
    queryFn: () => api.getBook(id!),
    enabled: Boolean(id),
  })
  const previewImageUrl = useResolvedCoverImage(bookQuery.data?.cover)
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
  const book = bookQuery.data

  useEffect(() => {
    const element = descriptionRef.current
    if (!element || !book?.description) {
      setDescriptionOverflowing(false)
      return
    }

    if (descriptionExpanded) {
      return
    }

    const measureOverflow = () => {
      setDescriptionOverflowing(element.scrollHeight > element.clientHeight + 1)
    }

    measureOverflow()

    if (typeof ResizeObserver === 'undefined') {
      return
    }

    const observer = new ResizeObserver(measureOverflow)
    observer.observe(element)
    return () => observer.disconnect()
  }, [book?.description, descriptionExpanded])

  if (bookQuery.isLoading) {
    return <div className="rounded-lg border border-slate-200 bg-white p-6 text-slate-500">Loading...</div>
  }

  if (!book) {
    return <div className="rounded-lg border border-slate-200 bg-white p-6 text-slate-500">Book not found.</div>
  }

  const genreDescriptions = new Map(
    (genresQuery.data?.data ?? [])
      .filter((genre) => book.genres.includes(genre.name) && genre.description)
      .map((genre) => [genre.name, genre.description ?? '']),
  )
  const shouldOfferRefresh = book.cover?.source !== 'ManualUpload' && book.cover?.source !== 'ManualUrl'
  const refreshLabel = book.cover?.imageUrl ? 'Search again' : 'Search cover'
  const displayCoverStatus = getDisplayCoverStatus(book.cover?.status, book.cover?.failureReason)
  const displayCoverFailure = getDisplayCoverFailure(book.cover?.failureReason)
  const coverSourceLabel = book.cover?.source ? `Source: ${book.cover.source}` : undefined
  const coverHint = !book.cover?.imageUrl
    ? [`Status: ${displayCoverStatus}`, displayCoverFailure].filter(Boolean).join('\n')
    : undefined

  const descriptionStyle: CSSProperties | undefined = !descriptionExpanded
    ? {
      display: '-webkit-box',
      WebkitBoxOrient: 'vertical',
      WebkitLineClamp: 4,
      overflow: 'hidden',
    }
    : undefined

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
              onClick={() => setDeleteConfirmOpen(true)}
            >
              <Trash2 className="h-4 w-4" />
              Delete
            </button>
          </div>
        </div>

        <section className="rounded-[1.75rem] border border-slate-200 bg-white p-5 shadow-sm">
          <div className="flex flex-col gap-6">
            <div className="flex flex-col gap-6 lg:flex-row lg:items-start">
              <div className="flex-none lg:w-[320px]">
                <BookCoverArtwork
                  className="w-full lg:w-[320px]"
                  cover={book.cover}
                  emptyActionLabel={!book.cover?.imageUrl && shouldOfferRefresh ? refreshLabel : undefined}
                  emptyLabel="No cover in library yet."
                  hint={coverHint}
                  hoverFooter={coverSourceLabel}
                  interactive
                  title={book.primaryTitle}
                  onClick={() => {
                    if (book.cover?.imageUrl) {
                      setPreviewOpen(true)
                      return
                    }

                    if (shouldOfferRefresh) {
                      refreshCoverMutation.mutate()
                    }
                  }}
                />
              </div>
              <div className="min-w-0 flex-1">
                <div className="flex min-w-0 flex-col gap-5">
                  <div className="flex min-w-0 flex-col gap-3">
                    <div className="flex flex-col gap-2">
                      <div className="text-xs font-semibold uppercase tracking-[0.26em] text-slate-400">{book.contentType}</div>
                      <div className="flex flex-wrap items-start justify-between gap-3">
                        <h1 className="text-3xl font-semibold tracking-tight text-slate-950 md:text-4xl">{book.primaryTitle}</h1>
                        <StatusPill status={book.status} />
                      </div>
                      {book.alternativeTitles.length ? (
                        <p className="text-sm text-slate-400">{book.alternativeTitles.join(' | ')}</p>
                      ) : null}
                      <p className="text-[11px] uppercase tracking-[0.24em] text-slate-400">
                        AUTHOR:{' '}
                        <span className="text-lg font-semibold normal-case tracking-normal text-slate-950">{book.author ?? 'Unknown author'}</span>
                      </p>
                    </div>
                    <div className="grid gap-4 rounded-2xl border border-slate-200 bg-slate-50 p-4">
                      <div className="flex flex-col gap-3 xl:flex-row xl:items-center xl:justify-between">
                        <div className="min-w-0 flex-1">
                          <div className="mb-2 flex items-center justify-between gap-3">
                            <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">Progress</div>
                            <div className="text-sm font-medium text-slate-700">{formatProgressInline(book)}</div>
                          </div>
                          <ProgressBar book={book} />
                        </div>
                        <div className="flex items-center gap-3 xl:pl-4">
                          <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">Rating</div>
                          <RatingSummary rating={book.rating} />
                        </div>
                      </div>
                      <div className="grid gap-4 border-t border-slate-200 pt-4 md:grid-cols-2">
                        <div className="grid gap-2">
                          <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">Genres</div>
                          <GenrePills descriptions={genreDescriptions} values={book.genres} />
                        </div>
                        <div className="grid gap-2">
                          <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">Tags</div>
                          <Pills values={book.tags} empty="No tags." />
                        </div>
                      </div>
                    </div>
                    <div className="grid gap-2">
                      <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">Description</div>
                      {book.description ? (
                        <div className="grid gap-2">
                          <p className="max-w-4xl text-sm leading-6 text-slate-600" ref={descriptionRef} style={descriptionStyle}>{book.description}</p>
                          {descriptionOverflowing ? (
                            <button
                              className="w-fit text-sm font-semibold text-cyan-700 hover:text-cyan-900"
                              type="button"
                              onClick={() => setDescriptionExpanded(!descriptionExpanded)}
                            >
                              {descriptionExpanded ? 'Show less' : 'Show more'}
                            </button>
                          ) : null}
                        </div>
                      ) : (
                        <div className="min-h-6" />
                      )}
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section className="grid gap-4 md:grid-cols-2">
          <Panel title="Links">
            <div className="grid gap-2">
              {book.links.length ? book.links.map((link) => (
                <a className="flex items-center justify-between rounded-md border border-slate-200 px-3 py-2 text-sm hover:bg-slate-50" href={link.url} key={link.id} rel="noreferrer" target="_blank">
                  <span>{getBookLinkLabel(link)}</span>
                  <ExternalLink className="h-4 w-4 text-slate-400" />
                </a>
              )) : <p className="text-sm text-slate-500">No links.</p>}
            </div>
          </Panel>
          <Panel title="Notes">
            <div className="grid gap-3 text-sm text-slate-700">
              {book.notes ? (
                <p className="whitespace-pre-line">{book.notes}</p>
              ) : (
                <p className="text-slate-500">No notes.</p>
              )}
            </div>
          </Panel>
        </section>
        <Panel title="Changelog">
          <div className="grid gap-3">
            {book.progressHistory.length ? book.progressHistory.map((entry) => (
              <div className="rounded-lg border border-slate-200 bg-slate-50 px-4 py-3" key={entry.id}>
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="text-sm font-semibold text-slate-900">
                    {entry.chapterLabel || entry.chapterNumber || 'Updated progress'}
                  </div>
                  <div className="text-xs text-slate-500">{formatDate(entry.changedAt)}</div>
                </div>
                <div className="mt-1 text-sm text-slate-600">
                  {entry.chapterNumber != null || entry.chapterLabel ? (
                    <p>
                      Progress: {entry.chapterLabel || entry.chapterNumber}
                      {entry.chapterNumber != null && entry.chapterLabel ? ` (${entry.chapterNumber})` : ''}
                    </p>
                  ) : null}
                  {entry.comment ? <p className="whitespace-pre-line">{entry.comment}</p> : null}
                </div>
              </div>
            )) : (
              <p className="text-sm text-slate-500">No changelog yet.</p>
            )}
          </div>
        </Panel>
      </div>

      <CoverLightbox
        emptyLabel="No cover has been saved for this book yet."
        footer={coverSourceLabel}
        imageUrl={previewImageUrl}
        open={previewOpen}
        title={book.primaryTitle}
        onClose={() => setPreviewOpen(false)}
      />
      <DeleteBookDialog
        open={deleteConfirmOpen}
        pending={deleteMutation.isPending}
        title={book.primaryTitle}
        onCancel={() => setDeleteConfirmOpen(false)}
        onConfirm={() => deleteMutation.mutate()}
      />
    </>
  )
}

function ProgressBar({ book }: { book: BookDto }) {
  const current = typeof book.currentChapterNumber === 'number' ? Number(book.currentChapterNumber) : null
  const total = typeof book.totalChapters === 'number' ? Number(book.totalChapters) : null
  const percent = current != null && total != null && total > 0
    ? Math.max(0, Math.min(100, (current / total) * 100))
    : null

  return (
    <div className="grid gap-2">
      <div className="h-3 overflow-hidden rounded-full bg-slate-200">
        <div
          className="h-full rounded-full bg-cyan-500 transition-[width]"
          style={{ width: percent == null ? '0%' : `${percent}%` }}
        />
      </div>
      <div className="text-xs text-slate-500">
        {percent == null ? 'No percentage available yet.' : `${Math.round(percent)}% complete`}
      </div>
    </div>
  )
}

function RatingSummary({ rating }: { rating?: number | null }) {
  const normalizedRating = typeof rating === 'number' ? Math.max(0, Math.min(10, rating)) : null
  const filledStars = normalizedRating == null ? 0 : Math.round(normalizedRating)

  return (
    <div className="flex flex-wrap items-center gap-2">
      <div className="flex items-center gap-1">
        {Array.from({ length: 10 }, (_, index) => (
          <Star
            className={`h-4 w-4 ${index < filledStars ? 'fill-amber-400 text-amber-400' : 'fill-slate-200 text-slate-300'}`}
            key={index}
          />
        ))}
      </div>
      <div className="text-sm font-medium text-slate-700">{normalizedRating == null ? '?/10' : `${normalizedRating}/10`}</div>
    </div>
  )
}

function StatusPill({ status }: { status: string }) {
  const normalized = status.trim().toLowerCase()
  const className = normalized === 'reading'
    ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
    : normalized === 'completed'
      ? 'border-cyan-200 bg-cyan-50 text-cyan-700'
      : normalized === 'plan to read'
        ? 'border-amber-200 bg-amber-50 text-amber-700'
        : normalized === 'on hold'
          ? 'border-orange-200 bg-orange-50 text-orange-700'
          : normalized === 'dropped'
            ? 'border-rose-200 bg-rose-50 text-rose-700'
            : 'border-slate-200 bg-slate-100 text-slate-700'

  return (
    <span className={`inline-flex min-h-10 items-center self-center rounded-full border px-4 py-1.5 text-sm font-semibold uppercase tracking-wide ${className}`}>
      {status}
    </span>
  )
}

function formatProgressInline(book: {
  status?: string | null
  currentChapterNumber?: number | null
  currentChapterLabel?: string | null
  totalChapters?: number | null
}) {
  const isCompleted = book.status?.trim().toLowerCase() === 'completed'
  const current = isCompleted && book.currentChapterNumber != null
    ? book.currentChapterNumber
    : book.currentChapterLabel || book.currentChapterNumber
  if (!current && !book.totalChapters) {
    return '-'
  }

  return `${current ?? '?'}${book.totalChapters ? ` / ${book.totalChapters}` : ''}`
}

function getBookLinkLabel(link: BookLinkDto) {
  const customLabel = link.label?.trim()
  if (customLabel) {
    return customLabel
  }

  const sourceType = link.sourceType.trim()
  if (sourceType && sourceType.toLowerCase() !== 'other') {
    return sourceType
  }

  try {
    return new URL(link.url).hostname.replace(/^www\./, '')
  } catch {
    return link.url
  }
}

function Panel({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
      <h2 className="mb-3 text-base font-semibold text-slate-950">{title}</h2>
      {children}
    </section>
  )
}

function DeleteBookDialog({
  open,
  pending,
  title,
  onCancel,
  onConfirm,
}: {
  open: boolean
  pending: boolean
  title: string
  onCancel: () => void
  onConfirm: () => void
}) {
  if (!open) {
    return null
  }

  return (
    <div
      aria-modal="true"
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 p-4 backdrop-blur-sm"
      role="dialog"
      onClick={pending ? undefined : onCancel}
    >
      <div className="grid w-full max-w-md gap-5 rounded-3xl border border-slate-200 bg-white p-6 shadow-2xl" onClick={(event) => event.stopPropagation()}>
        <div className="grid gap-2">
          <h2 className="text-lg font-semibold text-slate-950">Delete book</h2>
          <p className="text-sm leading-6 text-slate-600">
            Are you sure you want to delete <span className="font-semibold text-slate-900">{title}</span>? This cannot be undone.
          </p>
        </div>
        <div className="flex justify-end gap-2">
          <button className={secondaryButtonClass} disabled={pending} type="button" onClick={onCancel}>
            Cancel
          </button>
          <button
            className="inline-flex min-h-10 items-center justify-center rounded-md bg-red-600 px-4 py-2 text-sm font-semibold text-white hover:bg-red-700 disabled:bg-red-300"
            disabled={pending}
            type="button"
            onClick={onConfirm}
          >
            {pending ? 'Deleting...' : 'Delete'}
          </button>
        </div>
      </div>
    </div>
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

function GenrePills({ values, descriptions }: { values: string[]; descriptions: Map<string, string> }) {
  if (!values.length) {
    return <p className="text-sm text-slate-500">No genres.</p>
  }

  return (
    <div className="flex flex-wrap gap-2">
      {values.map((value) => {
        const description = descriptions.get(value)
        return (
          <span className="group relative" key={value}>
            <span className="rounded bg-slate-100 px-2 py-1 text-xs font-medium text-slate-700">{value}</span>
            {description ? (
              <span className="pointer-events-none absolute bottom-full left-1/2 z-10 mb-2 hidden w-56 -translate-x-1/2 rounded-lg bg-slate-950 px-3 py-2 text-xs font-normal leading-5 text-white shadow-xl group-hover:block">
                {description}
              </span>
            ) : null}
          </span>
        )
      })}
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

function formatDate(value?: string | null) {
  if (!value) {
    return '-'
  }

  return new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}
