import { useQuery } from '@tanstack/react-query'
import { ArrowRight, Ban, CalendarDays, Hash, ListFilter, Search, TextCursorInput, Trash2, Undo2, X } from 'lucide-react'
import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import { api } from '@/api/client'
import { inputClass } from '@/components/app/FormField'
import { Surface } from '@/components/app/DesignSystem'
import { useDebouncedValue } from '@/lib/useDebouncedValue'
import {
  analyzeBookSearch,
  applyBookSearchSuggestion,
  getBookSearchValue,
  getBookSearchScopeQuery,
  getLocalBookSearchSuggestions,
  type BookSearchSuggestionItem,
} from './bookSearchSyntax'

const suggestionDelayMs = 220
const suggestionListId = 'book-search-suggestions'

export function BookSearchInput({
  value,
  onChange,
}: {
  value: string
  onChange: (value: string) => void
}) {
  const [draftValue, setDraftValue] = useState(value)
  const [caret, setCaret] = useState(value.length)
  const [open, setOpen] = useState(false)
  const [activeIndex, setActiveIndex] = useState(0)
  const inputRef = useRef<HTMLInputElement>(null)
  const pendingSelectionRef = useRef<number | null>(null)
  const rootRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    setDraftValue(value)
    setCaret((current) => Math.min(current, value.length))
  }, [value])

  useLayoutEffect(() => {
    const pendingSelection = pendingSelectionRef.current
    if (pendingSelection == null) {
      return
    }

    pendingSelectionRef.current = null
    inputRef.current?.focus()
    inputRef.current?.setSelectionRange(pendingSelection, pendingSelection)
  }, [draftValue])

  const context = useMemo(() => analyzeBookSearch(draftValue, caret), [caret, draftValue])
  const remoteField = context.definition?.remote && context.hasColon
    ? context.definition.canonical
    : null
  const searchValue = getBookSearchValue(context)
  const scopeQuery = getBookSearchScopeQuery(draftValue, context)
  const remoteRequest = remoteField == null ? '' : `${remoteField}\u0000${searchValue}\u0000${scopeQuery}`
  const debouncedRequest = useDebouncedValue(remoteRequest, suggestionDelayMs)
  const [debouncedField, debouncedSearch = '', debouncedScopeQuery = ''] = debouncedRequest.split('\u0000')
  const remoteSuggestions = useQuery({
    queryKey: ['book-search-suggestions', debouncedField, debouncedSearch, debouncedScopeQuery],
    queryFn: ({ signal }) => api.getBookSearchSuggestions({
      field: debouncedField,
      search: debouncedSearch || undefined,
      query: debouncedScopeQuery || undefined,
      take: 10,
      signal,
    }),
    enabled: open && remoteField != null && debouncedField === remoteField,
    staleTime: 30_000,
  })

  const suggestions = useMemo(() => {
    const local = getLocalBookSearchSuggestions(context)
    if (!remoteField) {
      return local
    }

    const response = debouncedRequest === remoteRequest
      ? (remoteSuggestions.data ?? []).filter(
          (suggestion) => suggestion.value.trim().toLocaleLowerCase() !== 'none',
        )
      : []
    if (context.complete && response.some((suggestion) => suggestion.isExact && suggestion.isAvailable)) {
      return local
    }

    const values: BookSearchSuggestionItem[] = response.map((suggestion) => ({
      id: `value:${suggestion.value}`,
      kind: 'value',
      label: suggestion.value,
      description: suggestion.isAvailable
        ? `${formatCount(suggestion.count)} ${suggestion.count === 1 ? 'book' : 'books'}`
        : '0 books in current filters',
      disabled: !suggestion.isAvailable,
      value: suggestion.value,
    }))
    return [...local, ...values]
  }, [context, debouncedRequest, remoteField, remoteRequest, remoteSuggestions.data])

  const suggestionSignature = suggestions.map((suggestion) => suggestion.id).join('|')
  useEffect(() => {
    setActiveIndex(getNextSelectableIndex(suggestions, -1, 1))
  }, [suggestionSignature])

  useEffect(() => {
    function closeOnOutsidePointer(event: PointerEvent) {
      if (!rootRef.current?.contains(event.target as Node)) {
        setOpen(false)
      }
    }

    document.addEventListener('pointerdown', closeOnOutsidePointer)
    return () => document.removeEventListener('pointerdown', closeOnOutsidePointer)
  }, [])

  function updateDraft(nextValue: string, nextCaret: number) {
    setDraftValue(nextValue)
    setCaret(nextCaret)
    onChange(nextValue)
  }

  function selectSuggestion(suggestion: BookSearchSuggestionItem) {
    if (suggestion.disabled) {
      return
    }

    const result = applyBookSearchSuggestion(draftValue, context, suggestion)
    pendingSelectionRef.current = result.caret
    updateDraft(result.value, result.caret)
    setOpen(true)
  }

  const loadingRemote = remoteField != null &&
    (remoteRequest !== debouncedRequest || remoteSuggestions.isFetching)

  return (
    <Surface className="relative grid gap-2 p-4" tone="muted">
      <div className="relative" ref={rootRef}>
        <label className="sr-only" htmlFor="book-search-input">Search books</label>
        <Search className="pointer-events-none absolute left-3 top-1/2 z-10 h-4 w-4 -translate-y-1/2 text-slate-400" />
        <input
          aria-activedescendant={open && suggestions[activeIndex] ? `${suggestionListId}-${activeIndex}` : undefined}
          aria-autocomplete="list"
          aria-controls={suggestionListId}
          aria-expanded={open}
          autoComplete="off"
          className={`${inputClass} ui-control--search ${draftValue ? 'pr-12' : ''}`}
          id="book-search-input"
          placeholder="Search your library or type : for filters"
          ref={inputRef}
          role="combobox"
          value={draftValue}
          onChange={(event) => {
            const nextValue = event.target.value
            updateDraft(nextValue, event.target.selectionStart ?? nextValue.length)
            setOpen(true)
          }}
          onClick={(event) => {
            setCaret(event.currentTarget.selectionStart ?? draftValue.length)
            setOpen(true)
          }}
          onFocus={(event) => {
            setCaret(event.currentTarget.selectionStart ?? draftValue.length)
            setOpen(true)
          }}
          onKeyUp={(event) => {
            if (!['ArrowDown', 'ArrowUp', 'Enter', 'Tab', 'Escape'].includes(event.key)) {
              setCaret(event.currentTarget.selectionStart ?? draftValue.length)
            }
          }}
          onKeyDown={(event) => {
            if (event.key === 'Escape') {
              setOpen(false)
              return
            }
            if (!open || suggestions.length === 0) {
              return
            }
            if (event.key === 'ArrowDown') {
              event.preventDefault()
              setActiveIndex((current) => getNextSelectableIndex(suggestions, current, 1))
            } else if (event.key === 'ArrowUp') {
              event.preventDefault()
              setActiveIndex((current) => getNextSelectableIndex(suggestions, current, -1))
            } else if (event.key === 'Enter' || event.key === 'Tab') {
              const suggestion = suggestions[activeIndex] ?? suggestions[0]
              if (suggestion && !suggestion.disabled) {
                event.preventDefault()
                selectSuggestion(suggestion)
              }
            }
          }}
        />
        {draftValue ? (
          <button
            aria-label="Clear search and filters"
            className="absolute right-1.5 top-1/2 z-10 grid h-9 w-9 -translate-y-1/2 place-items-center rounded-[var(--qs-control-radius)] text-slate-500 transition hover:bg-slate-100 hover:text-slate-950 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-slate-950"
            type="button"
            onClick={() => {
              updateDraft('', 0)
              setOpen(false)
              inputRef.current?.focus()
            }}
          >
            <X aria-hidden="true" className="h-4 w-4" />
          </button>
        ) : null}

        {open ? (
          <div
            aria-busy={loadingRemote}
            className="ui-popover absolute left-0 right-0 z-40 mt-2 max-h-[26rem] overflow-y-auto p-1.5 shadow-xl ring-1 ring-slate-950/5"
            id={suggestionListId}
            role="listbox"
          >
            {suggestions.map((suggestion, index) => (
              <button
                aria-disabled={suggestion.disabled || undefined}
                aria-selected={activeIndex === index}
                disabled={suggestion.disabled}
                className={`group flex min-h-11 w-full items-center gap-3 rounded-[var(--qs-control-radius)] px-3 py-2 text-left transition ${
                  suggestion.disabled
                    ? 'cursor-not-allowed text-slate-400 opacity-60'
                    : activeIndex === index
                    ? 'bg-slate-900 text-white'
                    : suggestion.kind === 'remove'
                      ? 'text-rose-700 hover:bg-rose-50'
                      : 'text-[var(--qs-text)] hover:bg-[var(--qs-surface-muted)]'
                }`}
                id={`${suggestionListId}-${index}`}
                key={suggestion.id}
                role="option"
                type="button"
                onMouseEnter={() => {
                  if (!suggestion.disabled) {
                    setActiveIndex(index)
                  }
                }}
                onMouseDown={(event) => event.preventDefault()}
                onClick={() => selectSuggestion(suggestion)}
              >
                <SuggestionIcon item={suggestion} />
                <span className="min-w-0 flex-1">
                  <span className="block truncate font-mono text-sm font-semibold">{suggestion.label}</span>
                  <span className={`block truncate text-xs ${
                    activeIndex === index ? 'text-slate-300' : 'text-[var(--qs-muted)]'
                  }`}>
                    {suggestion.description}
                  </span>
                </span>
                {(suggestion.kind === 'filter' ||
                  suggestion.kind === 'value' ||
                  suggestion.kind === 'wildcard') ? (
                  <ArrowRight className={`h-4 w-4 shrink-0 ${
                    suggestion.disabled
                      ? 'text-slate-300'
                      : activeIndex === index ? 'text-slate-300' : 'text-slate-400 group-hover:text-slate-600'
                  }`} />
                ) : null}
              </button>
            ))}

            {loadingRemote ? (
              <div className="px-3 py-2 text-xs font-medium text-[var(--qs-muted)]" role="status">
                Loading matching values…
              </div>
            ) : null}
            {remoteSuggestions.isError ? (
              <div className="px-3 py-2 text-xs font-medium text-rose-700" role="status">
                Value suggestions are unavailable. Manual search still works.
              </div>
            ) : null}
            {!loadingRemote && suggestions.length === 0 ? (
              <div className="px-3 py-3 text-sm text-[var(--qs-muted)]" role="status">
                No suggestions. Keep typing to search the raw query.
              </div>
            ) : null}
          </div>
        ) : null}
      </div>

      <p className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-slate-500">
        <span>Choose a suggestion or type</span>
        <code>:</code>
        <span>to build a filter.</span>
        <code>-</code>
        <span>excludes a term.</span>
      </p>
    </Surface>
  )
}

function SuggestionIcon({ item }: { item: BookSearchSuggestionItem }) {
  const iconClass = 'h-4 w-4 shrink-0'
  if (item.kind === 'remove') {
    return <Trash2 className={iconClass} />
  }
  if (item.kind === 'exclude') {
    return <Ban className={iconClass} />
  }
  if (item.kind === 'include') {
    return <Undo2 className={iconClass} />
  }
  if (item.kind === 'operator' || item.definition?.kind === 'number') {
    return <Hash className={iconClass} />
  }
  if (item.definition?.kind === 'date') {
    return <CalendarDays className={iconClass} />
  }
  if (item.kind === 'value' || item.kind === 'wildcard') {
    return <TextCursorInput className={iconClass} />
  }
  return <ListFilter className={iconClass} />
}

function formatCount(count: number) {
  return new Intl.NumberFormat('en', { notation: 'compact', maximumFractionDigits: 1 }).format(count)
}

function getNextSelectableIndex(suggestions: BookSearchSuggestionItem[], current: number, direction: 1 | -1) {
  if (suggestions.length === 0 || suggestions.every((suggestion) => suggestion.disabled)) {
    return 0
  }

  for (let offset = 1; offset <= suggestions.length; offset += 1) {
    const index = (current + offset * direction + suggestions.length) % suggestions.length
    if (!suggestions[index]?.disabled) {
      return index
    }
  }

  return 0
}
