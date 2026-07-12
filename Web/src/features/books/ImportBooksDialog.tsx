import { useMutation } from '@tanstack/react-query'
import { AlertCircle, Download, FileUp, LoaderCircle, Save, Trash2, Upload, X } from 'lucide-react'
import { useEffect, useMemo, useRef, useState } from 'react'
import { Virtuoso } from 'react-virtuoso'
import { toast } from 'sonner'
import { api } from '@/api/client'
import { HttpError } from '@/api/http'
import type { BookImportFinalizeResult, BookImportRowDto, BookImportRowUpdateRequest, BookImportSessionDto } from '@/api/types'
import { buttonClass, inputClass, secondaryButtonClass } from '@/components/app/FormField'

type ImportBooksDialogProps = {
  open: boolean
  onClose: () => void
  onImported: (result: BookImportFinalizeResult) => void
}

export function ImportBooksDialog({ open, onClose, onImported }: ImportBooksDialogProps) {
  const [session, setSession] = useState<BookImportSessionDto | null>(null)
  const [expandedRowId, setExpandedRowId] = useState<string | null>(null)
  const [allowInvalidRowAutoExpand, setAllowInvalidRowAutoExpand] = useState(true)
  const [confirmCancelOpen, setConfirmCancelOpen] = useState(false)
  const fileInputRef = useRef<HTMLInputElement | null>(null)
  const invalidRows = useMemo(() => session?.rows.filter((row) => !row.isValid) ?? [], [session])

  const createSessionMutation = useMutation({
    mutationFn: (file: File) => api.createBookImportSession(file),
    onSuccess: (nextSession) => {
      setSession(nextSession)
      setAllowInvalidRowAutoExpand(true)
      setExpandedRowId(nextSession.rows.find((row) => !row.isValid)?.rowId ?? null)
      toast.success('Import draft created.')
    },
    onError: (error) => {
      toast.error(error instanceof HttpError ? error.apiError.detail : 'Could not parse CSV.')
    },
  })

  const finalizeMutation = useMutation({
    mutationFn: (sessionId: string) => api.finalizeBookImport(sessionId),
    onSuccess: (result) => {
      setSession(null)
      onImported(result)
      onClose()
    },
    onError: (error) => {
      toast.error(error instanceof HttpError ? error.apiError.detail : 'Could not finalize import.')
    },
  })

  const cancelMutation = useMutation({
    mutationFn: (sessionId: string) => api.cancelBookImport(sessionId),
  })

  const templateMutation = useMutation({
    mutationFn: () => api.downloadBookImportTemplate(),
    onSuccess: (blob) => {
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = 'book-import-template.csv'
      document.body.append(link)
      link.click()
      link.remove()
      URL.revokeObjectURL(url)
    },
    onError: (error) => {
      toast.error(error instanceof HttpError ? error.apiError.detail : 'Could not download CSV template.')
    },
  })

  useEffect(() => {
    if (!open) {
      setSession(null)
      setExpandedRowId(null)
      setAllowInvalidRowAutoExpand(true)
      setConfirmCancelOpen(false)
    }
  }, [open])

  useEffect(() => {
    if (!invalidRows.length) {
      setExpandedRowId(null)
      return
    }

    if (expandedRowId && invalidRows.some((row) => row.rowId === expandedRowId)) {
      return
    }

    if (allowInvalidRowAutoExpand) {
      setExpandedRowId(invalidRows[0]?.rowId ?? null)
    }
  }, [allowInvalidRowAutoExpand, expandedRowId, invalidRows])

  if (!open) {
    return null
  }

  function openFilePicker() {
    fileInputRef.current?.click()
  }

  function handleFileSelection(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file) {
      return
    }

    if (!file.name.toLowerCase().endsWith('.csv')) {
      toast.error('Choose a .csv file.')
      return
    }

    createSessionMutation.mutate(file)
  }

  function handleDismiss() {
    if (session?.sessionId) {
      setConfirmCancelOpen(true)
      return
    }

    onClose()
  }

  function handleConfirmCancelImport() {
    if (!session?.sessionId) {
      setConfirmCancelOpen(false)
      onClose()
      return
    }

    cancelMutation.mutate(session.sessionId, {
      onSuccess: () => {
        setConfirmCancelOpen(false)
        setSession(null)
        setExpandedRowId(null)
        setAllowInvalidRowAutoExpand(true)
        onClose()
      },
      onError: (error) => {
        toast.error(error instanceof HttpError ? error.apiError.detail : 'Could not cancel import.')
      },
    })
  }

  return (
    <div
      aria-modal="true"
      className="fixed inset-0 z-50 overflow-y-auto bg-slate-950/70 p-4 backdrop-blur-sm"
      role="dialog"
      onClick={handleDismiss}
    >
      <div className="mx-auto my-4 flex max-h-[calc(100vh-2rem)] w-full max-w-6xl flex-col gap-4 overflow-hidden rounded-3xl border border-slate-800 bg-slate-950 p-6 text-slate-100 shadow-2xl" onClick={(event) => event.stopPropagation()}>
        <div className="flex items-start justify-between gap-4">
          <div className="grid gap-1">
            <h2 className="text-lg font-semibold text-slate-50">Import books from CSV</h2>
            <p className="text-sm text-slate-400">Upload a file, fix invalid rows, then finalize to save books to your library.</p>
          </div>
          <div className="flex items-center gap-2">
            <button
              className={secondaryButtonClass}
              disabled={templateMutation.isPending}
              type="button"
              onClick={() => templateMutation.mutate()}
            >
              <Download className="h-4 w-4" />
              {templateMutation.isPending ? 'Downloading...' : 'Download template'}
            </button>
            <button className="rounded-full border border-slate-700 p-2 text-slate-400 hover:bg-slate-900 hover:text-slate-100" type="button" onClick={handleDismiss}>
              <X className="h-4 w-4" />
            </button>
          </div>
        </div>

        <input accept=".csv,text/csv" className="hidden" ref={fileInputRef} type="file" onChange={handleFileSelection} />

        {!session ? (
          <div className="grid gap-4 rounded-2xl border border-dashed border-slate-700 bg-slate-900 p-8 text-center">
            <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-slate-900 text-white">
              {createSessionMutation.isPending ? <LoaderCircle className="h-6 w-6 animate-spin" /> : <FileUp className="h-6 w-6" />}
            </div>
            <div className="grid gap-1">
              <div className="text-base font-semibold text-slate-50">
                {createSessionMutation.isPending ? 'Parsing CSV draft...' : 'Choose a CSV file'}
              </div>
              <div className="text-sm text-slate-400">Nothing is written to the database until you press final save.</div>
            </div>
            <div className="flex justify-center">
              <button className={buttonClass} disabled={createSessionMutation.isPending} type="button" onClick={openFilePicker}>
                <Upload className="h-4 w-4" />
                Select file
              </button>
            </div>
          </div>
        ) : (
          <>
            <div className="grid gap-4 md:grid-cols-4">
              <SummaryCard label="File" value={session.fileName} />
              <SummaryCard label="Rows" value={String(session.totalRows)} />
              <SummaryCard label="Positive" value={String(session.validRows)} />
              <SummaryCard label="Negative" value={String(session.invalidRows)} tone={session.invalidRows ? 'warn' : 'ok'} />
            </div>

            <div className="grid gap-2 rounded-2xl border border-slate-800 bg-slate-900 p-4">
              <div className="flex items-center justify-between gap-3">
                <div className="text-sm font-semibold text-slate-100">Progress</div>
                <div className="text-sm text-slate-300">{session.validRows} / {session.totalRows} valid</div>
              </div>
              <div className="h-3 overflow-hidden rounded-full bg-slate-800">
                <div
                  className="h-full rounded-full bg-emerald-500 transition-[width]"
                  style={{ width: `${session.totalRows === 0 ? 0 : (session.validRows / session.totalRows) * 100}%` }}
                />
              </div>
            </div>

            <div className="h-[min(60vh,44rem)] overflow-hidden rounded-2xl border border-slate-800 bg-slate-900/80 p-4">
              {invalidRows.length ? (
                <div className="h-full min-h-0">
                  <Virtuoso
                    className="import-rows-scroll"
                    components={{
                      Scroller: (props) => <div {...props} className="import-rows-scroll" />,
                    }}
                    data={invalidRows}
                    increaseViewportBy={{ bottom: 160, top: 160 }}
                    itemContent={(_, row) => (
                      <div className="pb-3">
                        <ImportRowEditor
                          expanded={expandedRowId === row.rowId}
                          row={row}
                          sessionId={session.sessionId}
                          onSessionChange={setSession}
                          onToggle={() => {
                            setExpandedRowId((current) => {
                              const isCollapsing = current === row.rowId
                              setAllowInvalidRowAutoExpand(!isCollapsing)
                              return isCollapsing ? null : row.rowId
                            })
                          }}
                        />
                      </div>
                    )}
                    overscan={120}
                    style={{ height: '100%' }}
                  />
                </div>
              ) : (
                <div className="grid place-items-center gap-2 rounded-2xl border border-emerald-900/60 bg-emerald-950/40 px-4 py-10 text-center text-emerald-200">
                  <Save className="h-6 w-6" />
                  <div className="text-base font-semibold">All rows are valid</div>
                  <div className="text-sm">You can finalize the import now. Books will be added only after final save.</div>
                </div>
              )}
            </div>

            <div className="flex flex-wrap justify-end gap-2">
              <button className={secondaryButtonClass} disabled={finalizeMutation.isPending} type="button" onClick={handleDismiss}>
                Close
              </button>
              <button
                className={buttonClass}
                disabled={!session.canFinalize || finalizeMutation.isPending}
                type="button"
                onClick={() => finalizeMutation.mutate(session.sessionId)}
              >
                <Save className="h-4 w-4" />
                {finalizeMutation.isPending ? 'Saving...' : 'Finalize import'}
              </button>
            </div>
          </>
        )}
      </div>
      <CancelImportConfirmDialog
        open={confirmCancelOpen}
        pending={cancelMutation.isPending}
        onCancel={() => setConfirmCancelOpen(false)}
        onConfirm={handleConfirmCancelImport}
      />
    </div>
  )
}

function CancelImportConfirmDialog({
  open,
  pending,
  onCancel,
  onConfirm,
}: {
  open: boolean
  pending: boolean
  onCancel: () => void
  onConfirm: () => void
}) {
  if (!open) {
    return null
  }

  return (
    <div
      aria-modal="true"
      className="fixed inset-0 z-[60] flex items-center justify-center bg-slate-950/80 p-4 backdrop-blur-sm"
      role="dialog"
      onClick={pending ? undefined : onCancel}
    >
      <div className="grid w-full max-w-md gap-5 rounded-3xl border border-slate-800 bg-slate-950 p-6 shadow-2xl" onClick={(event) => event.stopPropagation()}>
        <div className="grid gap-2">
          <h2 className="text-lg font-semibold text-slate-50">Cancel import?</h2>
          <p className="text-sm leading-6 text-slate-300">
            Closing now will discard this import session and remove your in-progress row fixes.
          </p>
        </div>
        <div className="flex justify-end gap-2">
          <button className={secondaryButtonClass} disabled={pending} type="button" onClick={onCancel}>
            Keep editing
          </button>
          <button
            className="inline-flex min-h-10 items-center justify-center rounded-md border border-rose-900 bg-rose-950 px-4 py-2 text-sm font-semibold text-rose-200 transition hover:bg-rose-900 disabled:cursor-not-allowed disabled:border-slate-700 disabled:bg-slate-900 disabled:text-slate-500"
            disabled={pending}
            type="button"
            onClick={onConfirm}
          >
            {pending ? 'Cancelling...' : 'Cancel import'}
          </button>
        </div>
      </div>
    </div>
  )
}

function ImportRowEditor({
  expanded,
  onToggle,
  row,
  sessionId,
  onSessionChange,
}: {
  expanded: boolean
  onToggle: () => void
  row: BookImportRowDto
  sessionId: string
  onSessionChange: (session: BookImportSessionDto) => void
}) {
  const [draft, setDraft] = useState<BookImportRowUpdateRequest>({
    primaryTitle: row.primaryTitle ?? '',
    authorName: row.authorName ?? '',
    contentType: row.contentType ?? '',
    status: row.status ?? '',
    tags: row.tags ?? '',
    totalChapters: row.totalChapters ?? '',
    currentChapterNumber: row.currentChapterNumber ?? '',
    currentChapterLabel: row.currentChapterLabel ?? '',
    rating: row.rating ?? '',
    priority: row.priority ?? '',
    description: row.description ?? '',
    notes: row.notes ?? '',
    rawImportedLine: row.rawImportedLine ?? '',
  })

  useEffect(() => {
    setDraft({
      primaryTitle: row.primaryTitle ?? '',
      authorName: row.authorName ?? '',
      contentType: row.contentType ?? '',
      status: row.status ?? '',
      tags: row.tags ?? '',
      totalChapters: row.totalChapters ?? '',
      currentChapterNumber: row.currentChapterNumber ?? '',
      currentChapterLabel: row.currentChapterLabel ?? '',
      rating: row.rating ?? '',
      priority: row.priority ?? '',
      description: row.description ?? '',
      notes: row.notes ?? '',
      rawImportedLine: row.rawImportedLine ?? '',
    })
  }, [row])

  const mutation = useMutation({
    mutationFn: () => api.updateBookImportRow(sessionId, row.rowId, draft),
    onSuccess: (session) => {
      onSessionChange(session)
      toast.success(`Row ${row.lineNumber} updated.`)
    },
    onError: (error) => {
      toast.error(error instanceof HttpError ? error.apiError.detail : 'Could not update import row.')
    },
  })

  const deleteMutation = useMutation({
    mutationFn: () => api.deleteBookImportRow(sessionId, row.rowId),
    onSuccess: (session) => {
      onSessionChange(session)
      toast.success(`Line ${row.lineNumber} removed.`)
    },
    onError: (error) => {
      toast.error(error instanceof HttpError ? error.apiError.detail : 'Could not remove import row.')
    },
  })

  function update<K extends keyof BookImportRowUpdateRequest>(key: K, value: string) {
    setDraft((current) => ({ ...current, [key]: value }))
  }

  function fieldError(field: keyof BookImportRowUpdateRequest) {
    return row.fieldErrors?.[field] ?? []
  }

  return (
      <div className="grid gap-4 rounded-2xl border border-slate-700 bg-slate-900 p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="grid gap-1">
          <button className="inline-flex items-center gap-2 text-left text-sm font-semibold text-amber-300" type="button" onClick={onToggle}>
            <AlertCircle className="h-4 w-4" />
            Line {row.lineNumber}
          </button>
          <div className="text-sm text-slate-200">
            {row.primaryTitle?.trim() || 'Untitled row'}
          </div>
          <div className="line-clamp-2 text-xs text-slate-400">
            {row.errors.join(' ')}
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <button className={secondaryButtonClass} type="button" onClick={onToggle}>
            {expanded ? 'Collapse' : 'Edit row'}
          </button>
          <button className={secondaryButtonClass} disabled={mutation.isPending || deleteMutation.isPending} type="button" onClick={() => mutation.mutate()}>
            {mutation.isPending ? 'Saving...' : 'Revalidate row'}
          </button>
          <button className="inline-flex min-h-10 items-center justify-center gap-2 rounded-md border border-rose-900 bg-rose-950 px-4 py-2 text-sm font-semibold text-rose-200 transition hover:bg-rose-900 disabled:cursor-not-allowed disabled:border-slate-700 disabled:bg-slate-900 disabled:text-slate-500" disabled={mutation.isPending || deleteMutation.isPending} type="button" onClick={() => deleteMutation.mutate()}>
            <Trash2 className="h-4 w-4" />
            {deleteMutation.isPending ? 'Removing...' : 'Remove row'}
          </button>
        </div>
      </div>

      <div className="grid gap-2 rounded-xl border border-amber-900/60 bg-slate-950 p-3 text-sm text-slate-200">
        {row.errors.map((error) => <p key={error}>{error}</p>)}
      </div>

      {expanded ? (
        <>
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            <LabeledInput error={fieldError('primaryTitle')} label="Title" value={draft.primaryTitle ?? ''} onChange={(value) => update('primaryTitle', value)} />
            <LabeledInput error={fieldError('authorName')} label="Author" value={draft.authorName ?? ''} onChange={(value) => update('authorName', value)} />
            <LabeledInput error={fieldError('tags')} label="Tags" value={draft.tags ?? ''} onChange={(value) => update('tags', value)} />
            <LabeledInput error={fieldError('contentType')} label="Type" value={draft.contentType ?? ''} onChange={(value) => update('contentType', value)} />
            <LabeledInput error={fieldError('status')} label="Status" value={draft.status ?? ''} onChange={(value) => update('status', value)} />
            <LabeledInput error={fieldError('currentChapterLabel')} label="Current label" value={draft.currentChapterLabel ?? ''} onChange={(value) => update('currentChapterLabel', value)} />
            <LabeledInput error={fieldError('currentChapterNumber')} label="Current chapter" value={draft.currentChapterNumber ?? ''} onChange={(value) => update('currentChapterNumber', value)} />
            <LabeledInput error={fieldError('totalChapters')} label="Total chapters" value={draft.totalChapters ?? ''} onChange={(value) => update('totalChapters', value)} />
            <LabeledInput error={fieldError('rating')} label="Rating" value={draft.rating ?? ''} onChange={(value) => update('rating', value)} />
            <LabeledInput error={fieldError('priority')} label="Priority" value={draft.priority ?? ''} onChange={(value) => update('priority', value)} />
          </div>

          <div className="grid gap-3 md:grid-cols-2">
            <LabeledTextarea error={fieldError('notes')} label="Notes" value={draft.notes ?? ''} onChange={(value) => update('notes', value)} />
            <LabeledTextarea error={fieldError('description')} label="Description" value={draft.description ?? ''} onChange={(value) => update('description', value)} />
          </div>
        </>
      ) : null}
    </div>
  )
}

function SummaryCard({ label, value, tone = 'neutral' }: { label: string; value: string; tone?: 'neutral' | 'ok' | 'warn' }) {
  const toneClass = tone === 'ok'
    ? 'border-emerald-900/60 bg-emerald-950/40 text-emerald-200'
    : tone === 'warn'
      ? 'border-amber-900/60 bg-amber-950/40 text-amber-200'
      : 'border-slate-700 bg-slate-900 text-slate-100'

  return (
    <div className={`grid gap-1 rounded-2xl border px-4 py-3 ${toneClass}`}>
      <div className="text-xs font-semibold uppercase tracking-wide">{label}</div>
      <div className="truncate text-base font-semibold">{value}</div>
    </div>
  )
}

function LabeledInput({ error = [], label, value, onChange }: { error?: string[]; label: string; value: string; onChange: (value: string) => void }) {
  const errorMessage = error.join(' ')

  return (
    <label className="grid gap-1 text-sm">
      <span className="font-medium text-slate-300">{label}</span>
      <input
        aria-invalid={errorMessage ? 'true' : undefined}
        className={`${inputClass} ${errorMessage ? 'border-rose-500 focus:border-rose-400 focus:ring-rose-400/20' : ''}`}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
      {errorMessage ? <span className="text-xs font-medium text-rose-300">{errorMessage}</span> : null}
    </label>
  )
}

function LabeledTextarea({ error = [], label, value, onChange }: { error?: string[]; label: string; value: string; onChange: (value: string) => void }) {
  const errorMessage = error.join(' ')

  return (
    <label className="grid gap-1 text-sm">
      <span className="font-medium text-slate-300">{label}</span>
      <textarea
        aria-invalid={errorMessage ? 'true' : undefined}
        className={`${inputClass} min-h-28 ${errorMessage ? 'border-rose-500 focus:border-rose-400 focus:ring-rose-400/20' : ''}`}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
      {errorMessage ? <span className="text-xs font-medium text-rose-300">{errorMessage}</span> : null}
    </label>
  )
}
