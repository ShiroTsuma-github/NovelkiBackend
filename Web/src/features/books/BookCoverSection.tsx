import { X, ZoomIn } from 'lucide-react'
import { useEffect, useState } from 'react'
import { API_BASE_URL, tokenStorageKey } from '@/api/http'
import type { BookCoverDto } from '@/api/types'

type BookCoverArtworkProps = {
  title: string
  cover?: BookCoverDto | null
  imageUrl?: string | null
  interactive?: boolean
  onClick?: () => void
  className?: string
  emptyLabel?: string
  hint?: string
}

type CoverLightboxProps = {
  open: boolean
  title: string
  imageUrl: string | null
  emptyLabel?: string
  onClose: () => void
}

export function useResolvedCoverImage(cover?: BookCoverDto | null) {
  const [blobUrl, setBlobUrl] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    let objectUrl: string | null = null

    async function loadImage() {
      if (!cover?.imageUrl) {
        setBlobUrl(null)
        return
      }

      const apiOrigin = API_BASE_URL.replace(/\/api\/v1\/?$/, '')
      const token = localStorage.getItem(tokenStorageKey)
      const response = await fetch(`${apiOrigin}${cover.imageUrl}`, {
        headers: token ? { Authorization: `Bearer ${token}` } : undefined,
      })
      if (!response.ok) {
        setBlobUrl(null)
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
  }, [cover?.imageUrl, cover?.lastAttemptAt])

  return blobUrl
}

export function BookCoverArtwork({
  title,
  cover,
  imageUrl,
  interactive = false,
  onClick,
  className = '',
  emptyLabel = 'No cover',
  hint,
}: BookCoverArtworkProps) {
  const blobUrl = useResolvedCoverImage(cover)
  const resolvedImageUrl = imageUrl ?? blobUrl
  const wrapperClassName = [
    'group relative flex aspect-[2/3] w-full items-center justify-center overflow-hidden rounded-2xl border border-slate-200 bg-slate-100 shadow-sm',
    interactive ? 'cursor-zoom-in transition hover:border-slate-300 hover:shadow-md' : '',
    className,
  ].filter(Boolean).join(' ')

  const content = resolvedImageUrl ? (
    <img alt={title} className="h-full w-full object-cover" src={resolvedImageUrl} />
  ) : (
    <div className="grid place-items-center gap-2 px-6 text-center text-slate-500">
      <div className="text-xs font-semibold uppercase tracking-[0.28em] text-slate-400">No Cover</div>
      <div className="text-sm">{emptyLabel}</div>
      {hint ? <div className="text-xs text-slate-400">{hint}</div> : null}
    </div>
  )

  if (!interactive) {
    return <div className={wrapperClassName}>{content}</div>
  }

  return (
    <button className={wrapperClassName} type="button" onClick={onClick}>
      {content}
      <span className="pointer-events-none absolute right-3 top-3 inline-flex items-center gap-1 rounded-full bg-slate-950/75 px-2.5 py-1 text-[11px] font-medium text-white opacity-0 transition group-hover:opacity-100">
        <ZoomIn className="h-3 w-3" />
        Preview
      </span>
    </button>
  )
}

export function CoverLightbox({
  open,
  title,
  imageUrl,
  emptyLabel = 'No cover available.',
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
          <img alt={title} className="max-h-[90vh] w-full rounded-3xl bg-white object-contain shadow-2xl" src={imageUrl} />
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
