import { X, ZoomIn } from 'lucide-react'
import { useEffect, useState } from 'react'
import { API_BASE_URL, getStoredSession } from '@/api/http'
import type { BookCoverDto } from '@/api/types'

type BookCoverArtworkProps = {
  title: string
  cover?: BookCoverDto | null
  imageUrl?: string | null
  preferredVariant?: 'full' | 'thumbnail'
  interactive?: boolean
  onClick?: () => void
  onRemove?: () => void
  className?: string
  emptyLabel?: string
  hint?: string
  removeLabel?: string
  emptyActionLabel?: string
  hoverFooter?: string
}

type CoverLightboxProps = {
  open: boolean
  title: string
  imageUrl: string | null
  emptyLabel?: string
  footer?: string
  onClose: () => void
}

const COVER_CACHE_TTL_MS = 60_000

type CoverCacheEntry = {
  blobUrl: string | null
  promise: Promise<string | null> | null
  refs: number
  cleanupTimer: ReturnType<typeof setTimeout> | null
}

const coverImageCache = new Map<string, CoverCacheEntry>()

export function useResolvedCoverImage(cover?: BookCoverDto | null, variant: 'full' | 'thumbnail' = 'full') {
  const [blobUrl, setBlobUrl] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    const cacheKey = getCoverCacheKey(cover, variant)

    if (!cacheKey) {
      setBlobUrl(null)
      return
    }

    const entry = acquireCoverCacheEntry(cacheKey)
    setBlobUrl(entry.blobUrl)

    if (!entry.promise && !entry.blobUrl) {
      const targetUrl = variant === 'thumbnail'
        ? cover!.thumbnailImageUrl ?? cover!.imageUrl!
        : cover!.imageUrl!
      entry.promise = fetchCoverBlobUrl(targetUrl)
        .then((nextBlobUrl) => {
          entry.blobUrl = nextBlobUrl
          return nextBlobUrl
        })
        .finally(() => {
          entry.promise = null
        })
    }

    entry.promise?.then((nextBlobUrl) => {
      if (active) {
        setBlobUrl(nextBlobUrl)
      }
    })

    return () => {
      active = false
      releaseCoverCacheEntry(cacheKey)
    }
  }, [cover?.imageUrl, cover?.thumbnailImageUrl, cover?.lastAttemptAt, variant])

  return blobUrl
}

function getCoverCacheKey(cover?: BookCoverDto | null, variant: 'full' | 'thumbnail') {
  const targetUrl = variant === 'thumbnail'
    ? cover?.thumbnailImageUrl ?? cover?.imageUrl
    : cover?.imageUrl
  if (!targetUrl) {
    return null
  }

  return `${variant}::${targetUrl}::${cover?.lastAttemptAt ?? ''}`
}

function acquireCoverCacheEntry(cacheKey: string) {
  const existing = coverImageCache.get(cacheKey)
  if (existing) {
    existing.refs += 1
    if (existing.cleanupTimer) {
      clearTimeout(existing.cleanupTimer)
      existing.cleanupTimer = null
    }
    return existing
  }

  const created: CoverCacheEntry = {
    blobUrl: null,
    promise: null,
    refs: 1,
    cleanupTimer: null,
  }
  coverImageCache.set(cacheKey, created)
  return created
}

function releaseCoverCacheEntry(cacheKey: string) {
  const entry = coverImageCache.get(cacheKey)
  if (!entry) {
    return
  }

  entry.refs = Math.max(0, entry.refs - 1)
  if (entry.refs > 0 || entry.cleanupTimer) {
    return
  }

  entry.cleanupTimer = setTimeout(() => {
    const current = coverImageCache.get(cacheKey)
    if (!current || current.refs > 0) {
      return
    }

    if (current.blobUrl) {
      URL.revokeObjectURL(current.blobUrl)
    }
    coverImageCache.delete(cacheKey)
  }, COVER_CACHE_TTL_MS)
}

async function fetchCoverBlobUrl(imageUrl: string) {
  const apiOrigin = API_BASE_URL.replace(/\/api\/v1\/?$/, '')
  const token = getStoredSession()?.accessToken
  const response = await fetch(`${apiOrigin}${imageUrl}`, {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
  })
  if (!response.ok) {
    return null
  }

  return URL.createObjectURL(await response.blob())
}

export function BookCoverArtwork({
  title,
  cover,
  imageUrl,
  preferredVariant = 'full',
  interactive = false,
  onClick,
  onRemove,
  className = '',
  emptyLabel = 'No cover',
  hint,
  removeLabel = 'Remove cover',
  emptyActionLabel,
  hoverFooter,
}: BookCoverArtworkProps) {
  const blobUrl = useResolvedCoverImage(cover, preferredVariant)
  const resolvedImageUrl = imageUrl ?? blobUrl
  const wrapperClassName = [
    'group relative flex aspect-[2/3] w-full items-center justify-center overflow-hidden rounded-2xl border border-slate-200 bg-slate-100 shadow-sm',
    interactive ? 'transition hover:border-slate-300 hover:shadow-md' : '',
    interactive && resolvedImageUrl ? 'cursor-zoom-in' : '',
    interactive && !resolvedImageUrl ? 'cursor-pointer' : '',
    className,
  ].filter(Boolean).join(' ')

  const content = resolvedImageUrl ? (
    <img alt={title} className="h-full w-full object-cover" src={resolvedImageUrl} />
  ) : (
    <div className="grid place-items-center gap-2 px-6 text-center text-slate-500">
      <div className="text-xs font-semibold uppercase tracking-[0.28em] text-slate-400">No Cover</div>
      <div className="text-sm">{emptyLabel}</div>
      {hint ? <div className="whitespace-pre-line text-xs text-slate-400">{hint}</div> : null}
      {emptyActionLabel ? (
        <div className="inline-flex items-center rounded-full bg-slate-900 px-3 py-1 text-xs font-semibold text-white">
          {emptyActionLabel}
        </div>
      ) : null}
    </div>
  )

  if (!interactive) {
    return (
      <div className={wrapperClassName}>
        {content}
        {resolvedImageUrl && onRemove ? (
          <button
            aria-label={removeLabel}
            className="absolute left-3 top-3 inline-flex h-9 w-9 items-center justify-center rounded-full bg-red-600/90 text-white opacity-0 shadow-lg transition hover:bg-red-700 group-hover:opacity-100"
            type="button"
            onClick={onRemove}
          >
            <X className="h-4 w-4" />
          </button>
        ) : null}
      </div>
    )
  }

  return (
    <button className={wrapperClassName} type="button" onClick={onClick}>
      {content}
      {resolvedImageUrl && onRemove ? (
        <span
          className="absolute left-3 top-3 z-10"
          onClick={(event) => {
            event.preventDefault()
            event.stopPropagation()
            onRemove()
          }}
        >
          <span className="inline-flex h-9 w-9 items-center justify-center rounded-full bg-red-600/90 text-white opacity-0 shadow-lg transition hover:bg-red-700 group-hover:opacity-100">
            <X className="h-4 w-4" />
          </span>
        </span>
      ) : null}
      {resolvedImageUrl ? (
        <span className="pointer-events-none absolute right-3 top-3 inline-flex items-center gap-1 rounded-full bg-slate-950/75 px-2.5 py-1 text-[11px] font-medium text-white opacity-0 transition group-hover:opacity-100">
          <ZoomIn className="h-3 w-3" />
          Preview
        </span>
      ) : null}
      {resolvedImageUrl && hoverFooter ? (
        <span className="pointer-events-none absolute inset-x-0 bottom-0 bg-gradient-to-t from-slate-950/85 via-slate-950/55 to-transparent px-4 pb-3 pt-8 text-center text-xs font-medium text-white opacity-0 transition group-hover:opacity-100">
          {hoverFooter}
        </span>
      ) : null}
    </button>
  )
}

export function CoverLightbox({
  open,
  title,
  imageUrl,
  emptyLabel = 'No cover available.',
  footer,
  onClose,
}: CoverLightboxProps) {
  useEffect(() => {
    if (!open) {
      return
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onClose()
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [onClose, open])

  if (!open) {
    return null
  }

  return (
    <div
      aria-modal="true"
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/80 p-4 backdrop-blur-sm"
      role="dialog"
      onClick={onClose}
    >
      <div className="relative max-h-[90vh] w-full max-w-4xl" onClick={(event) => event.stopPropagation()}>
        <button
          aria-label="Close preview"
          className="absolute right-3 top-3 z-10 inline-flex h-10 w-10 items-center justify-center rounded-full bg-slate-950/80 text-white transition hover:bg-slate-950"
          type="button"
          onClick={onClose}
        >
          <X className="h-4 w-4" />
        </button>
        {imageUrl ? (
          <div className="grid gap-3">
            <img alt={title} className="max-h-[82vh] w-full rounded-3xl bg-white object-contain shadow-2xl" src={imageUrl} />
            {footer ? (
              <div className="rounded-2xl bg-slate-900/85 px-4 py-3 text-sm text-slate-100 shadow-xl">
                {footer}
              </div>
            ) : null}
          </div>
        ) : (
          <div className="grid min-h-[70vh] place-items-center rounded-3xl border border-dashed border-slate-600 bg-slate-900 px-8 text-center text-slate-200 shadow-2xl">
            <div className="grid gap-2">
              <div className="text-xs font-semibold uppercase tracking-[0.32em] text-slate-500">No Cover</div>
              <div className="text-lg font-medium">{emptyLabel}</div>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
