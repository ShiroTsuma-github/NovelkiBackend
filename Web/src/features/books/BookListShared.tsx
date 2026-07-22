import { ChevronDown, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, ArrowDown, ArrowUp } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import type { Dispatch, SetStateAction } from 'react'
import type { SetURLSearchParams } from 'react-router-dom'
import { inputClass } from '@/components/app/FormField'

export const bookListPageSizeOptions = [20, 50, 100, 500]

const paginationDirectionButtonClass =
  'inline-flex min-h-11 min-w-11 items-center justify-center rounded-md border border-slate-300 bg-white px-3 text-xl font-medium text-slate-700 transition hover:bg-slate-100 hover:text-slate-950'

const paginationPageButtonClass =
  'pagination-page-button inline-flex min-h-11 min-w-11 items-center justify-center bg-transparent px-3 text-base font-medium text-slate-500 transition hover:bg-slate-100 hover:text-slate-950'

const paginationActivePageButtonClass =
  'pagination-page-button inline-flex min-h-11 min-w-11 items-center justify-center bg-slate-900 px-3 text-base font-semibold text-white'

export function useBookListUrlState(
  searchParams: URLSearchParams,
  setSearchParams: SetURLSearchParams,
  options: {
    defaultSortBy?: string
    defaultSortDirection?: string
    pageSizeStorageKey?: string
  } = {},
) {
  const defaultSortBy = options.defaultSortBy ?? 'lastModified'
  const defaultSortDirection = options.defaultSortDirection ?? 'desc'
  const skip = Number(searchParams.get('skip') ?? 0)
  const pageSize = readBookListPageSize(searchParams, options.pageSizeStorageKey)
  const sortBy = searchParams.get('sortBy') ?? defaultSortBy
  const sortDirection = searchParams.get('sortDirection') ?? defaultSortDirection
  const query = searchParams.get('query') ?? ''
  const requestQuery = query.trim()

  useEffect(() => {
    if (options.pageSizeStorageKey) {
      window.localStorage.setItem(options.pageSizeStorageKey, String(pageSize))
    }
  }, [options.pageSizeStorageKey, pageSize])

  function updateQuery(value: string) {
    const next = new URLSearchParams(searchParams)
    if (value.trim()) {
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
    if (options.pageSizeStorageKey) {
      window.localStorage.setItem(options.pageSizeStorageKey, nextPageSize)
    }
    const next = new URLSearchParams(searchParams)
    next.set('take', nextPageSize)
    next.delete('skip')
    setSearchParams(next)
  }

  function setSort(nextSortBy: string, nextSortDirection: string) {
    const next = new URLSearchParams(searchParams)
    next.set('sortBy', nextSortBy)
    next.set('sortDirection', nextSortDirection)
    next.delete('skip')
    setSearchParams(next)
  }

  return {
    pageSize,
    query,
    requestQuery,
    setPageSize,
    setSkip,
    setSort,
    skip,
    sortBy,
    sortDirection,
    updateQuery,
  }
}

export function useBookListPagination({
  dataLength,
  isFetching,
  pageSize,
  setSkip,
  skip,
  total,
}: {
  dataLength: number
  isFetching: boolean
  pageSize: number
  setSkip: (nextSkip: number) => void
  skip: number
  total: number
}) {
  const [activePageGapId, setActivePageGapId] = useState<string | null>(null)
  const pendingBottomAnchorRef = useRef<number | null>(null)
  const canGoBack = skip > 0
  const canGoForward = skip + pageSize < total
  const totalPages = Math.max(1, Math.ceil(total / pageSize))
  const currentPage = Math.min(totalPages, Math.floor(skip / pageSize) + 1)
  const visiblePages = getVisiblePageNumbers(currentPage, totalPages)

  useEffect(() => {
    if (isFetching || pendingBottomAnchorRef.current == null) {
      return
    }

    const distanceFromBottom = pendingBottomAnchorRef.current
    pendingBottomAnchorRef.current = null

    requestAnimationFrame(() => {
      const target = Math.max(0, document.documentElement.scrollHeight - window.innerHeight - distanceFromBottom)
      window.scrollTo({ top: target })
    })
  }, [dataLength, isFetching, skip])

  function goToPage(page: number) {
    const nextPage = Math.min(Math.max(1, page), totalPages)
    prepareBottomAnchorForPageChange()
    setActivePageGapId(null)
    setSkip((nextPage - 1) * pageSize)
  }

  function prepareBottomAnchorForPageChange() {
    const distanceFromBottom = document.documentElement.scrollHeight - (window.scrollY + window.innerHeight)
    pendingBottomAnchorRef.current = distanceFromBottom <= 24 ? Math.max(0, distanceFromBottom) : null
  }

  return {
    activePageGapId,
    canGoBack,
    canGoForward,
    currentPage,
    setActivePageGapId,
    totalPages,
    visiblePages,
    onGoToPage: goToPage,
  }
}

export function useBookListScrollShortcuts() {
  const [showBackToTop, setShowBackToTop] = useState(false)
  const [showGoDown, setShowGoDown] = useState(false)

  useEffect(() => {
    function updateScrollShortcutVisibility() {
      const distanceFromBottom = document.documentElement.scrollHeight - (window.scrollY + window.innerHeight)
      setShowBackToTop(window.scrollY > 480)
      setShowGoDown(distanceFromBottom > 480)
    }

    updateScrollShortcutVisibility()
    window.addEventListener('scroll', updateScrollShortcutVisibility, { passive: true })
    window.addEventListener('resize', updateScrollShortcutVisibility)
    return () => {
      window.removeEventListener('scroll', updateScrollShortcutVisibility)
      window.removeEventListener('resize', updateScrollShortcutVisibility)
    }
  }, [])

  return {
    showBackToTop,
    showGoDown,
    scrollBackToTop: () => window.scrollTo({ top: 0, behavior: 'smooth' }),
    scrollToPageBottom: () => window.scrollTo({ top: document.documentElement.scrollHeight, behavior: 'smooth' }),
  }
}

export function BookListFooter({
  activePageGapId,
  canGoBack,
  canGoForward,
  currentPage,
  pageSize,
  setActivePageGapId,
  setPageSize,
  skip,
  total,
  totalPages,
  visiblePages,
  onGoToPage,
}: {
  activePageGapId: string | null
  canGoBack: boolean
  canGoForward: boolean
  currentPage: number
  pageSize: number
  setActivePageGapId: Dispatch<SetStateAction<string | null>>
  setPageSize: (nextPageSize: string) => void
  skip: number
  total: number
  totalPages: number
  visiblePages: Array<number | 'ellipsis'>
  onGoToPage: (page: number) => void
}) {
  return (
    <div className="border-t border-slate-200 bg-white px-4 py-3 text-sm text-slate-600">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <span>{total ? `${skip + 1}-${Math.min(skip + pageSize, total)} of ${total}` : '0 results'}</span>
        <div className="flex flex-wrap items-center justify-end gap-3">
          <label className="flex items-center gap-2">
            <span>Per page</span>
            <span className="relative inline-flex">
              <select
                className={`${inputClass} ui-control--compact-select ui-control--page-size bg-white transition hover:border-slate-400 hover:bg-white focus:bg-white`}
                value={pageSize}
                onChange={(event) => setPageSize(event.target.value)}
              >
                {bookListPageSizeOptions.map((option) => <option key={option} value={option}>{option}</option>)}
              </select>
              <ChevronDown className="pointer-events-none absolute right-2 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
            </span>
          </label>
          <div className="flex flex-wrap items-center gap-1.5">
            {canGoBack ? (
              <button aria-label="First page" className={paginationDirectionButtonClass} type="button" onClick={() => onGoToPage(1)}>
                <ChevronsLeft className="h-4 w-4" />
              </button>
            ) : null}
            {canGoBack ? (
              <button aria-label="Previous page" className={paginationDirectionButtonClass} type="button" onClick={() => onGoToPage(currentPage - 1)}>
                <ChevronLeft className="h-4 w-4" />
              </button>
            ) : null}
            {visiblePages.map((item, index) => item === 'ellipsis'
              ? (
                <PageGapJump
                  gapId={`ellipsis-${index}`}
                  isOpen={activePageGapId === `ellipsis-${index}`}
                  key={`ellipsis-${index}`}
                  totalPages={totalPages}
                  onGoToPage={onGoToPage}
                  onOpen={() => setActivePageGapId(`ellipsis-${index}`)}
                  onClose={() => setActivePageGapId((current) => current === `ellipsis-${index}` ? null : current)}
                />
              )
              : (
                <button
                  aria-current={item === currentPage ? 'page' : undefined}
                  className={item === currentPage ? paginationActivePageButtonClass : paginationPageButtonClass}
                  key={item}
                  type="button"
                  onClick={() => onGoToPage(item)}
                >
                  {item}
                </button>
              ))}
            {canGoForward ? (
              <button aria-label="Next page" className={paginationDirectionButtonClass} type="button" onClick={() => onGoToPage(currentPage + 1)}>
                <ChevronRight className="h-4 w-4" />
              </button>
            ) : null}
            {canGoForward ? (
              <button aria-label="Last page" className={paginationDirectionButtonClass} type="button" onClick={() => onGoToPage(totalPages)}>
                <ChevronsRight className="h-4 w-4" />
              </button>
            ) : null}
          </div>
        </div>
      </div>
    </div>
  )
}

export function ScrollShortcutButtons({
  showBackToTop,
  showGoDown,
  onBackToTop,
  onGoDown,
}: {
  showBackToTop: boolean
  showGoDown: boolean
  onBackToTop: () => void
  onGoDown: () => void
}) {
  if (!showBackToTop && !showGoDown) {
    return null
  }

  return (
    <div className="fixed bottom-6 right-6 z-40 flex flex-col gap-2">
      {showBackToTop ? (
        <button
          aria-label="Back to top"
          className="inline-flex h-11 w-11 items-center justify-center rounded-md border border-slate-300 bg-white text-slate-700 transition hover:border-slate-400 hover:bg-slate-50 hover:text-slate-950"
          type="button"
          onClick={onBackToTop}
        >
          <ArrowUp className="h-4 w-4" />
        </button>
      ) : null}
      {showGoDown ? (
        <button
          aria-label="Go to bottom"
          className="inline-flex h-11 w-11 items-center justify-center rounded-md border border-slate-300 bg-white text-slate-700 transition hover:border-slate-400 hover:bg-slate-50 hover:text-slate-950"
          type="button"
          onClick={onGoDown}
        >
          <ArrowDown className="h-4 w-4" />
        </button>
      ) : null}
    </div>
  )
}

export function getNextSortDirection(
  nextSortBy: string,
  currentSortBy: string,
  currentSortDirection: string,
) {
  const defaultDirection = nextSortBy === 'lastModified' || nextSortBy === 'created' ? 'desc' : 'asc'
  return currentSortBy === nextSortBy
    ? currentSortDirection === 'asc' ? 'desc' : 'asc'
    : defaultDirection
}

function PageGapJump({
  gapId,
  isOpen,
  totalPages,
  onGoToPage,
  onOpen,
  onClose,
}: {
  gapId: string
  isOpen: boolean
  totalPages: number
  onGoToPage: (page: number) => void
  onOpen: () => void
  onClose: () => void
}) {
  const [value, setValue] = useState('')
  const containerRef = useRef<HTMLDivElement | null>(null)

  const parsed = Number(value)
  const isValid = Number.isInteger(parsed) && parsed >= 1 && parsed <= totalPages

  function submit() {
    if (!isValid) {
      return false
    }

    onGoToPage(parsed)
    setValue('')
    return true
  }

  useEffect(() => {
    if (!isOpen) {
      return
    }

    function handlePointerDown(event: PointerEvent) {
      if (containerRef.current?.contains(event.target as Node)) {
        return
      }

      if (submit()) {
        return
      }

      onClose()
    }

    document.addEventListener('pointerdown', handlePointerDown)
    return () => document.removeEventListener('pointerdown', handlePointerDown)
  }, [isOpen, isValid, onClose, parsed, totalPages])

  useEffect(() => {
    if (!isOpen) {
      setValue('')
    }
  }, [isOpen])

  return (
    <div className="relative" ref={containerRef}>
      <button
        aria-label="Jump between pages"
        aria-expanded={isOpen}
        className={paginationPageButtonClass}
        type="button"
        onClick={() => {
          if (isOpen) {
            onClose()
            return
          }

          onOpen()
        }}
      >
        ...
      </button>
      {isOpen ? (
        <div className="ui-popover absolute bottom-full left-1/2 mb-2 w-32 -translate-x-1/2 p-3">
          <input
            autoFocus
            aria-invalid={!isValid && value.length > 0 ? 'true' : undefined}
            data-gap-id={gapId}
            aria-label="Page number"
            className={`${inputClass} min-h-11 w-full text-center ${!isValid && value.length > 0 ? '!border-rose-500' : ''}`}
            inputMode="numeric"
            max={totalPages}
            min={1}
            placeholder={`1-${totalPages}`}
            value={value}
            onChange={(event) => setValue(event.target.value.replace(/[^\d]/g, ''))}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                submit()
              }
              if (event.key === 'Escape') {
                onClose()
              }
            }}
          />
        </div>
      ) : null}
    </div>
  )
}

function readBookListPageSize(searchParams: URLSearchParams, storageKey?: string) {
  const parameter = searchParams.get('take')
  const value = Number(parameter ?? (storageKey ? window.localStorage.getItem(storageKey) : null) ?? 20)
  return bookListPageSizeOptions.includes(value) ? value : 20
}

function getVisiblePageNumbers(currentPage: number, totalPages: number): Array<number | 'ellipsis'> {
  if (totalPages <= 7) {
    return Array.from({ length: totalPages }, (_, index) => index + 1)
  }

  if (currentPage <= 4) {
    return [1, 2, 3, 4, 5, 6, 'ellipsis', totalPages]
  }

  if (currentPage >= totalPages - 3) {
    return [1, 'ellipsis', totalPages - 5, totalPages - 4, totalPages - 3, totalPages - 2, totalPages - 1, totalPages]
  }

  return [1, 'ellipsis', currentPage - 1, currentPage, currentPage + 1, 'ellipsis', totalPages]
}
