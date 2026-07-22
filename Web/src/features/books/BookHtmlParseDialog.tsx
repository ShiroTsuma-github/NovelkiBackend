import { useMutation } from '@tanstack/react-query'
import { AlertTriangle, ArrowLeft, Braces, Check, FileCode2, X } from 'lucide-react'
import { useEffect, useId, useMemo, useRef, useState } from 'react'
import { api } from '@/api/client'
import { HttpError } from '@/api/http'
import type { BookHtmlParseResult } from '@/api/types'
import { Badge, DialogPanel, buttonVariants, useBodyScrollLock } from '@/components/app/DesignSystem'
import { buttonClass, inputClass, secondaryButtonClass } from '@/components/app/FormField'
import type { BookFormValues } from './bookFormSchema'

export type BookHtmlParseField =
  | 'primaryTitle'
  | 'authorName'
  | 'contentType'
  | 'alternativeTitles'
  | 'genres'
  | 'tags'
  | 'description'
  | 'canonicalUrl'
  | 'coverUrl'

type PreviewField = {
  key: BookHtmlParseField
  label: string
  value: string
  conflict: boolean
}

export function BookHtmlParseDialog({
  currentValues,
  hasDraftCover,
  open,
  onApply,
  onClose,
}: {
  currentValues: BookFormValues
  hasDraftCover: boolean
  open: boolean
  onApply: (result: BookHtmlParseResult, selected: ReadonlySet<BookHtmlParseField>) => void
  onClose: () => void
}) {
  const titleId = useId()
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const [html, setHtml] = useState('')
  const [result, setResult] = useState<BookHtmlParseResult | null>(null)
  const [selected, setSelected] = useState<Set<BookHtmlParseField>>(new Set())
  useBodyScrollLock(open)

  const parseMutation = useMutation({
    mutationFn: api.parseBookHtml,
    onSuccess: (parsed) => {
      setResult(parsed)
      setSelected(new Set(getAvailableFieldKeys(parsed)))
    },
  })

  const previewFields = useMemo(
    () => result ? buildPreviewFields(result, currentValues, hasDraftCover) : [],
    [currentValues, hasDraftCover, result],
  )

  useEffect(() => {
    if (!open) return
    const frame = window.requestAnimationFrame(() => textareaRef.current?.focus())
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') closeAndReset()
    }
    window.addEventListener('keydown', closeOnEscape)
    return () => {
      window.cancelAnimationFrame(frame)
      window.removeEventListener('keydown', closeOnEscape)
    }
  }, [onClose, open])

  if (!open) return null

  const error = parseMutation.error instanceof HttpError
    ? parseMutation.error.apiError.detail
    : parseMutation.error instanceof Error ? parseMutation.error.message : null

  function closeAndReset() {
    parseMutation.reset()
    setHtml('')
    setResult(null)
    setSelected(new Set())
    onClose()
  }

  function toggleField(key: BookHtmlParseField) {
    setSelected((current) => {
      const next = new Set(current)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  }

  return (
    <div aria-labelledby={titleId} aria-modal="true" className="book-html-dialog-backdrop" role="dialog" onClick={closeAndReset}>
      <DialogPanel className="book-html-dialog" onClick={(event) => event.stopPropagation()}>
        <header className="book-html-dialog__header">
          <div className="book-html-dialog__mark" aria-hidden="true"><FileCode2 className="h-5 w-5" /></div>
          <div className="min-w-0 flex-1">
            <div className="ui-eyebrow">Metadata resolver</div>
            <h2 className="book-html-dialog__title" id={titleId}>Parse page HTML</h2>
            <p className="book-html-dialog__intro">Paste the full source of a supported book page. Nothing is saved until you review and apply it.</p>
          </div>
          <button aria-label="Close HTML parser" className={`${buttonVariants.ghost} ui-icon-button`} type="button" onClick={closeAndReset}>
            <X className="h-4 w-4" />
          </button>
        </header>

        {result ? (
          <div className="book-html-dialog__content">
            <div className="book-html-dialog__source-row">
              <div>
                <div className="text-sm font-semibold text-[var(--qs-text)]">Detected source</div>
                <div className="mt-1 text-xs text-[var(--qs-muted)]">Select the metadata that should be applied to this draft.</div>
              </div>
              <Badge tone="accent">{result.source}</Badge>
            </div>
            <div className="book-html-preview-grid">
              {previewFields.map((field) => (
                <label className={`book-html-preview ${selected.has(field.key) ? 'book-html-preview--selected' : ''}`} key={field.key}>
                  <input checked={selected.has(field.key)} className="sr-only" type="checkbox" onChange={() => toggleField(field.key)} />
                  <span className="book-html-preview__check" aria-hidden="true">{selected.has(field.key) ? <Check className="h-3.5 w-3.5" /> : null}</span>
                  <span className="min-w-0 flex-1">
                    <span className="book-html-preview__heading">
                      <span>{field.label}</span>
                      {field.conflict ? <span className="book-html-preview__conflict">replace / merge</span> : null}
                    </span>
                    <span className="book-html-preview__value">{field.value}</span>
                  </span>
                </label>
              ))}
            </div>
            {result.warnings.length ? (
              <div className="book-html-warnings" role="status">
                <AlertTriangle className="mt-0.5 h-4 w-4 flex-none" />
                <div>
                  <div className="text-sm font-semibold">Review {result.warnings.length === 1 ? 'one warning' : `${result.warnings.length} warnings`}</div>
                  <ul className="mt-1 grid gap-1 text-xs">
                    {result.warnings.map((warning, index) => <li key={`${warning}-${index}`}>{warning}</li>)}
                  </ul>
                </div>
              </div>
            ) : null}
          </div>
        ) : (
          <div className="book-html-dialog__content">
            <label className="grid gap-2" htmlFor={`${titleId}-input`}>
              <span className="text-sm font-semibold text-[var(--qs-text)]">Full page HTML</span>
              <textarea className={`${inputClass} book-html-dialog__textarea`} id={`${titleId}-input`} placeholder="<!doctype html>…" ref={textareaRef} spellCheck={false} value={html} onChange={(event) => setHtml(event.target.value)} />
            </label>
            <div aria-live="polite">{error ? <div className="book-html-dialog__error">{error}</div> : null}</div>
          </div>
        )}

        <footer className="book-html-dialog__footer">
          {result ? (
            <>
              <button className={secondaryButtonClass} type="button" onClick={() => { parseMutation.reset(); setResult(null) }}>
                <ArrowLeft className="h-4 w-4" /> Back to HTML
              </button>
              <button className={buttonClass} disabled={selected.size === 0} type="button" onClick={() => { onApply(result, selected); closeAndReset() }}>
                <Check className="h-4 w-4" /> Apply selected
              </button>
            </>
          ) : (
            <>
              <div className="book-html-dialog__privacy"><Braces className="h-4 w-4" /> Parsed as text; scripts are never executed.</div>
              <button className={buttonClass} disabled={!html.trim() || parseMutation.isPending} type="button" onClick={() => parseMutation.mutate(html)}>
                <FileCode2 className="h-4 w-4" /> {parseMutation.isPending ? 'Parsing…' : 'Parse HTML'}
              </button>
            </>
          )}
        </footer>
      </DialogPanel>
    </div>
  )
}

function getAvailableFieldKeys(result: BookHtmlParseResult): BookHtmlParseField[] {
  const fields: BookHtmlParseField[] = []
  if (result.primaryTitle) fields.push('primaryTitle')
  if (result.authorName) fields.push('authorName')
  if (result.contentType?.id) fields.push('contentType')
  if (result.alternativeTitles.length) fields.push('alternativeTitles')
  if (result.genres.some((genre) => genre.id)) fields.push('genres')
  if (result.tags.length) fields.push('tags')
  if (result.description) fields.push('description')
  if (result.canonicalUrl) fields.push('canonicalUrl')
  if (result.coverUrl) fields.push('coverUrl')
  return fields
}

function buildPreviewFields(result: BookHtmlParseResult, values: BookFormValues, hasDraftCover: boolean): PreviewField[] {
  const fields: PreviewField[] = []
  if (result.primaryTitle) fields.push({ key: 'primaryTitle', label: 'Primary title', value: result.primaryTitle, conflict: Boolean(values.primaryTitle.trim()) })
  if (result.authorName) fields.push({ key: 'authorName', label: 'Author', value: result.authorName, conflict: Boolean(values.authorName?.trim()) })
  if (result.contentType?.id) fields.push({ key: 'contentType', label: 'Content type', value: result.contentType.name, conflict: Boolean(values.contentTypeId) })
  if (result.alternativeTitles.length) fields.push({ key: 'alternativeTitles', label: 'Alternative titles', value: result.alternativeTitles.join(' · '), conflict: Boolean(values.alternativeTitlesText.trim()) })
  const matchedGenres = result.genres.filter((genre) => genre.id)
  if (matchedGenres.length) fields.push({ key: 'genres', label: 'Genres', value: matchedGenres.map((genre) => genre.name).join(' · '), conflict: values.genreIds.length > 0 })
  if (result.tags.length) fields.push({ key: 'tags', label: 'Tags', value: result.tags.join(' · '), conflict: Boolean(values.tagsText.trim()) })
  if (result.description) fields.push({ key: 'description', label: 'Description', value: result.description, conflict: Boolean(values.description?.trim()) })
  if (result.canonicalUrl) fields.push({ key: 'canonicalUrl', label: 'Source link', value: result.canonicalUrl, conflict: Boolean(values.linksText.trim()) })
  if (result.coverUrl) fields.push({ key: 'coverUrl', label: 'Cover', value: result.coverUrl, conflict: hasDraftCover })
  return fields
}
