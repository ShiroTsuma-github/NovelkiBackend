import { useMutation } from '@tanstack/react-query'
import { AlertCircle, ChevronDown, Download, FileUp, LoaderCircle, Save, Trash2, Upload, X } from 'lucide-react'
import type { ReactNode } from 'react'
import { useEffect, useId, useMemo, useRef, useState } from 'react'
import { Virtuoso } from 'react-virtuoso'
import { toast } from 'sonner'
import { api } from '@/api/client'
import { HttpError } from '@/api/http'
import type { BookImportFinalizeResult, BookImportRowDto, BookImportRowUpdateRequest, BookImportSessionDto } from '@/api/types'
import { DialogPanel, useBodyScrollLock } from '@/components/app/DesignSystem'
import { buttonClass, inputClass, secondaryButtonClass } from '@/components/app/FormField'
import { formatProgress } from './bookProgress'

type ImportBooksDialogProps = {
  open: boolean
  onClose: () => void
  onImported: (result: BookImportFinalizeResult) => void
}

function ImportRowsScroller(props: React.ComponentProps<'div'>) {
  return <div {...props} className="import-rows-scroll" />
}

export function getImportSessionStats(session: BookImportSessionDto | null) {
  const invalidRows = session?.rows.filter((row) => !row.isValid) ?? []
  const totalRows = session?.totalRows ?? 0
  const validRows = session?.validRows ?? 0

  return {
    invalidRows,
    invalidRowsCount: session?.invalidRows ?? 0,
    progressPercent: totalRows === 0 ? 0 : (validRows / totalRows) * 100,
    totalRows,
    validRows,
  }
}

export function ImportBooksDialog({ open, onClose, onImported }: ImportBooksDialogProps) {
  const [session, setSession] = useState<BookImportSessionDto | null>(null)
  const [expandedRowId, setExpandedRowId] = useState<string | null>(null)
  const [confirmCancelOpen, setConfirmCancelOpen] = useState(false)
  useBodyScrollLock(open)
  const [dropzoneActive, setDropzoneActive] = useState(false)
  const [finalizeResult, setFinalizeResult] = useState<BookImportFinalizeResult | null>(null)
  const fileInputRef = useRef<HTMLInputElement | null>(null)
  const importStats = useMemo(() => getImportSessionStats(session), [session])
  const invalidRows = importStats.invalidRows

  const createSessionMutation = useMutation({
    mutationFn: (file: File) => api.createBookImportSession(file),
    onSuccess: (nextSession) => {
      setSession(nextSession)
      setExpandedRowId(null)
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
      setFinalizeResult(result)
      onImported(result)
    },
    onError: (error) => {
      toast.error(error instanceof HttpError ? error.apiError.detail : 'Could not finalize import.')
    },
  })

  const cancelMutation = useMutation({
    mutationFn: (sessionId: string) => api.cancelBookImport(sessionId),
  })

  const deleteInvalidRowsMutation = useMutation({
    mutationFn: (sessionId: string) => api.deleteInvalidBookImportRows(sessionId),
    onSuccess: (nextSession) => {
      setSession(nextSession)
      setExpandedRowId(null)
      toast.success('Invalid rows removed.')
    },
    onError: (error) => {
      toast.error(error instanceof HttpError ? error.apiError.detail : 'Could not remove invalid rows.')
    },
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
      setConfirmCancelOpen(false)
      setDropzoneActive(false)
      setFinalizeResult(null)
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

    setExpandedRowId(null)
  }, [expandedRowId, invalidRows])

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

    handleSelectedFile(file)
  }

  function handleSelectedFile(file: File) {
    if (!file.name.toLowerCase().endsWith('.csv')) {
      toast.error('Choose a .csv file.')
      return
    }

    createSessionMutation.mutate(file)
  }

  function handleDrop(event: React.DragEvent<HTMLDivElement>) {
    event.preventDefault()
    setDropzoneActive(false)

    const file = event.dataTransfer.files?.[0]
    if (!file) {
      return
    }

    handleSelectedFile(file)
  }

  function handleDismiss() {
    if (finalizeResult) {
      onClose()
      return
    }

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
      className="fixed inset-0 z-50 flex items-start justify-center overflow-hidden bg-slate-950/70 p-1 backdrop-blur-sm sm:p-2"
      role="dialog"
      onClick={handleDismiss}
    >
      <DialogPanel data-testid="import-dialog-panel" className={`${session && !finalizeResult ? 'h-[calc(100vh-0.5rem)] sm:h-[calc(100vh-1rem)]' : 'max-h-[calc(100vh-0.5rem)] sm:max-h-[calc(100vh-1rem)]'} flex max-w-6xl flex-col gap-2 overflow-hidden p-3 sm:p-4`} onClick={(event) => event.stopPropagation()}>
        <div className="flex items-start justify-between gap-2">
          <div className="grid gap-1">
            <h2 className={`${session && !finalizeResult ? 'text-base' : 'text-lg'} font-semibold text-slate-50`}>Import books from CSV</h2>
            {session && !finalizeResult ? null : (
              <p className="text-sm text-slate-400">Upload a file, fix invalid rows, then finalize to save books to your library.</p>
            )}
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
            <button className="min-h-11 min-w-11 rounded-md border border-slate-700 p-2 text-slate-400 hover:bg-slate-900 hover:text-slate-100" type="button" onClick={handleDismiss}>
              <X className="h-4 w-4" />
            </button>
          </div>
        </div>

        <input accept=".csv,text/csv" className="hidden" ref={fileInputRef} type="file" onChange={handleFileSelection} />

        {finalizeResult ? (
          <ImportFinalizeSuccess result={finalizeResult} onClose={onClose} />
        ) : !session ? (
          <div
            className={`grid gap-4 rounded-2xl border border-dashed p-8 text-center transition ${dropzoneActive ? 'border-cyan-400 bg-slate-900/80' : 'border-slate-700 bg-slate-900'}`}
            onDragEnter={(event) => {
              event.preventDefault()
              setDropzoneActive(true)
            }}
            onDragLeave={(event) => {
              event.preventDefault()
              if (event.currentTarget.contains(event.relatedTarget as Node | null)) {
                return
              }
              setDropzoneActive(false)
            }}
            onDragOver={(event) => {
              event.preventDefault()
              if (!dropzoneActive) {
                setDropzoneActive(true)
              }
            }}
            onDrop={handleDrop}
          >
            <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-md bg-slate-900 text-white">
              {createSessionMutation.isPending ? <LoaderCircle className="h-6 w-6 animate-spin" /> : <FileUp className="h-6 w-6" />}
            </div>
            <div className="grid gap-1">
              <div className="text-base font-semibold text-slate-50">
                {createSessionMutation.isPending ? 'Parsing CSV draft...' : 'Choose a CSV file'}
              </div>
              <div className="text-sm text-slate-400">Drop a CSV here or use file selection. Nothing is written to the database until you press final save.</div>
            </div>
            <div className="flex justify-center">
              <button className={buttonClass} disabled={createSessionMutation.isPending} type="button" onClick={openFilePicker}>
                <Upload className="h-4 w-4" />
                Select file
              </button>
            </div>
          </div>
        ) : (
          <div className="flex min-h-0 flex-1 flex-col gap-2">
            <div className="grid grid-cols-2 gap-2 md:grid-cols-4">
              <SummaryCard label="File" value={session.fileName} />
              <SummaryCard label="Rows" value={formatImportCount(importStats.totalRows)} />
              <SummaryCard label="Positive" value={formatImportCount(importStats.validRows)} />
              <SummaryCard label="Negative" value={formatImportCount(importStats.invalidRowsCount)} tone={importStats.invalidRowsCount ? 'warn' : 'ok'} />
            </div>

            <div className="grid gap-1 rounded-xl border border-slate-800 bg-slate-900 px-3 py-2">
              <div className="flex items-center justify-between gap-3">
                <div className="text-sm font-semibold text-slate-100">Progress</div>
                <div className="text-sm text-slate-300">{formatImportCount(importStats.validRows)} / {formatImportCount(importStats.totalRows)} valid</div>
              </div>
              <div className="h-2 overflow-hidden rounded-full bg-slate-800">
                <div
                  className="h-full rounded-full bg-emerald-500 transition-[width]"
                  style={{ width: `${importStats.progressPercent}%` }}
                />
              </div>
            </div>

            <div className="min-h-0 flex-1 overflow-hidden rounded-2xl border border-slate-800 bg-slate-900/80 p-2" data-testid="import-invalid-rows-panel">
              {invalidRows.length ? (
                <div className="grid h-full min-h-0 gap-3">
                  <Virtuoso
                    className="import-rows-scroll"
                    components={{ Scroller: ImportRowsScroller }}
                    data={invalidRows}
                    increaseViewportBy={{ bottom: 160, top: 160 }}
                    itemContent={(_, row) => (
                      <div className="pb-2 pr-2">
                        <ImportRowEditor
                          availableContentTypes={session.availableContentTypes}
                          availableStatuses={session.availableStatuses}
                          expanded={expandedRowId === row.rowId}
                          row={row}
                          sessionId={session.sessionId}
                          onSessionChange={setSession}
                          onToggle={() => {
                            setExpandedRowId((current) => {
                              const isCollapsing = current === row.rowId
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
              {invalidRows.length ? (
                <button
                  className="inline-flex min-h-10 items-center justify-center gap-2 rounded-md border border-rose-900 bg-rose-950 px-4 py-2 text-sm font-semibold text-rose-200 transition hover:bg-rose-900 disabled:cursor-not-allowed disabled:border-slate-700 disabled:bg-slate-900 disabled:text-slate-500"
                  disabled={deleteInvalidRowsMutation.isPending || finalizeMutation.isPending}
                  type="button"
                  onClick={() => deleteInvalidRowsMutation.mutate(session.sessionId)}
                >
                  <Trash2 className="h-4 w-4" />
                  {deleteInvalidRowsMutation.isPending ? 'Removing invalid...' : 'Discard all invalid'}
                </button>
              ) : null}
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
          </div>
        )}
      </DialogPanel>
      <CancelImportConfirmDialog
        open={confirmCancelOpen}
        pending={cancelMutation.isPending}
        onCancel={() => setConfirmCancelOpen(false)}
        onConfirm={handleConfirmCancelImport}
      />
    </div>
  )
}

function ImportFinalizeSuccess({ result, onClose }: { result: BookImportFinalizeResult; onClose: () => void }) {
  return (
    <div className="grid min-h-0 gap-4">
      <div className="grid gap-4 md:grid-cols-2">
        <SummaryCard label="Imported" value={formatImportCount(result.importedCount)} tone="ok" />
        <SummaryCard label="Skipped" value={formatImportCount(result.skippedCount)} tone={result.skippedCount ? 'warn' : 'neutral'} />
      </div>

      {result.errors.length ? (
        <div className="grid gap-2 rounded-2xl border border-amber-900/60 bg-amber-950/40 p-4 text-sm text-amber-100">
          <div className="font-semibold">Partial import messages</div>
          {result.errors.map((error) => <p key={error}>{error}</p>)}
        </div>
      ) : null}

      <div className="grid gap-3 rounded-2xl border border-slate-800 bg-slate-900/80 p-4">
        <div>
          <h3 className="text-base font-semibold text-slate-50">Imported books</h3>
          <p className="text-sm text-slate-400">Saved titles from this CSV finalization.</p>
        </div>
        {result.importedBooks.length ? (
          <div className="max-h-[min(28rem,55vh)] overflow-auto rounded-xl border border-slate-800">
            <table className="w-full border-collapse text-left text-sm">
              <thead className="bg-slate-950 text-xs uppercase tracking-wide text-slate-400">
                <tr>
                  <th className="px-4 py-3">Title</th>
                  <th className="w-32 px-4 py-3">Type</th>
                  <th className="w-36 px-4 py-3">Progress</th>
                </tr>
              </thead>
              <tbody>
                {result.importedBooks.map((book) => (
                  <tr className="border-t border-slate-800" key={`${book.contentType}:${book.primaryTitle}`}>
                    <td className="px-4 py-3 font-medium text-slate-100">{book.primaryTitle}</td>
                    <td className="px-4 py-3 text-slate-300">{book.contentType}</td>
                    <td className="px-4 py-3 text-slate-300">{formatProgress(book)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="rounded-xl border border-slate-800 bg-slate-950 px-4 py-6 text-center text-sm text-slate-400">
            No books were imported.
          </div>
        )}
      </div>

      <div className="flex justify-end">
        <button className={buttonClass} type="button" onClick={onClose}>
          Close
        </button>
      </div>
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
      <DialogPanel className="grid max-w-md gap-5 p-6" onClick={(event) => event.stopPropagation()}>
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
      </DialogPanel>
    </div>
  )
}

function ImportRowEditor({
  availableContentTypes,
  availableStatuses,
  expanded,
  onToggle,
  row,
  sessionId,
  onSessionChange,
}: {
  availableContentTypes: string[]
  availableStatuses: string[]
  expanded: boolean
  onToggle: () => void
  row: BookImportRowDto
  sessionId: string
  onSessionChange: (session: BookImportSessionDto) => void
}) {
  if (expanded) {
    return (
      <ExpandedImportRowEditor
        availableContentTypes={availableContentTypes}
        availableStatuses={availableStatuses}
        row={row}
        sessionId={sessionId}
        onSessionChange={onSessionChange}
        onToggle={onToggle}
      />
    )
  }

  return (
    <CollapsedImportRowEditor
      row={row}
      sessionId={sessionId}
      onSessionChange={onSessionChange}
      onToggle={onToggle}
    />
  )
}

function CollapsedImportRowEditor({
  onToggle,
  row,
  sessionId,
  onSessionChange,
}: {
  onToggle: () => void
  row: BookImportRowDto
  sessionId: string
  onSessionChange: (session: BookImportSessionDto) => void
}) {
  const deleteMutation = useDeleteImportRowMutation(sessionId, row, onSessionChange)

  return (
    <ImportRowShell
      deletePending={deleteMutation.isPending}
      expanded={false}
      row={row}
      savePending={false}
      onDelete={() => deleteMutation.mutate()}
      onRevalidate={undefined}
      onToggle={onToggle}
    />
  )
}

function ExpandedImportRowEditor({
  availableContentTypes,
  availableStatuses,
  onToggle,
  row,
  sessionId,
  onSessionChange,
}: {
  availableContentTypes: string[]
  availableStatuses: string[]
  onToggle: () => void
  row: BookImportRowDto
  sessionId: string
  onSessionChange: (session: BookImportSessionDto) => void
}) {
  const formEndRef = useRef<HTMLDivElement | null>(null)
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

  useEffect(() => {
    let frame = 0
    let cancelled = false

    const scrollAfterLayout = (attempt: number) => {
      if (cancelled) {
        return
      }

      scrollElementIntoNearestImportScroller(formEndRef.current)
      if (attempt < 6) {
        frame = requestAnimationFrame(() => scrollAfterLayout(attempt + 1))
      }
    }

    frame = requestAnimationFrame(() => scrollAfterLayout(0))

    return () => {
      cancelled = true
      cancelAnimationFrame(frame)
    }
  }, [])

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

  const deleteMutation = useDeleteImportRowMutation(sessionId, row, onSessionChange)

  function update<K extends keyof BookImportRowUpdateRequest>(key: K, value: string) {
    setDraft((current) => ({ ...current, [key]: value }))
  }

  function fieldError(field: keyof BookImportRowUpdateRequest) {
    return row.fieldErrors?.[field] ?? []
  }

  return (
    <ImportRowShell
      deletePending={deleteMutation.isPending}
      expanded
      row={row}
      savePending={mutation.isPending}
      onDelete={() => deleteMutation.mutate()}
      onRevalidate={() => mutation.mutate()}
      onToggle={onToggle}
    >
      <div className="grid gap-3 md:grid-cols-3 xl:grid-cols-4">
        <LabeledInput error={fieldError('primaryTitle')} label="Title" value={draft.primaryTitle ?? ''} onChange={(value) => update('primaryTitle', value)} />
        <LabeledInput error={fieldError('authorName')} label="Author" value={draft.authorName ?? ''} onChange={(value) => update('authorName', value)} />
        <LabeledInput error={fieldError('tags')} label="Tags" value={draft.tags ?? ''} onChange={(value) => update('tags', value)} />
        <LabeledInput error={fieldError('contentType')} label="Type" suggestions={availableContentTypes} value={draft.contentType ?? ''} onChange={(value) => update('contentType', value)} />
        <LabeledInput error={fieldError('status')} label="Status" suggestions={availableStatuses} value={draft.status ?? ''} onChange={(value) => update('status', value)} />
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
      <div ref={formEndRef} aria-hidden="true" />
    </ImportRowShell>
  )
}

function scrollElementIntoNearestImportScroller(element: HTMLElement | null) {
  const scroller = element?.closest('.import-rows-scroll')
  if (!(element instanceof HTMLElement) || !(scroller instanceof HTMLElement)) {
    if (typeof element?.scrollIntoView === 'function') {
      element.scrollIntoView({ block: 'nearest', inline: 'nearest' })
    }
    return
  }

  const elementBox = element.getBoundingClientRect()
  const scrollerBox = scroller.getBoundingClientRect()
  const bottomOverflow = elementBox.bottom - scrollerBox.bottom
  const topOverflow = scrollerBox.top - elementBox.top

  if (bottomOverflow > 0) {
    scroller.scrollTop += bottomOverflow + 8
    return
  }

  if (topOverflow > 0) {
    scroller.scrollTop -= topOverflow + 8
  }
}

function ImportRowShell({
  children,
  deletePending,
  expanded,
  onDelete,
  onRevalidate,
  onToggle,
  row,
  savePending,
}: {
  children?: ReactNode
  deletePending: boolean
  expanded: boolean
  onDelete: () => void
  onRevalidate: (() => void) | undefined
  onToggle: () => void
  row: BookImportRowDto
  savePending: boolean
}) {
  const busy = savePending || deletePending

  return (
    <div className="grid gap-4 rounded-2xl border border-slate-700 bg-slate-900 p-4" data-testid={`import-row-${row.rowId}`}>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="grid min-w-0 flex-1 gap-1">
          <button className="inline-flex items-center gap-2 text-left text-sm font-semibold text-amber-300" type="button" onClick={onToggle}>
            <AlertCircle className="h-4 w-4" />
            Line {row.lineNumber}
          </button>
          <div className="max-w-full text-sm text-slate-200" title={row.primaryTitle?.trim() || 'Untitled row'}>
            <span className="block max-w-full truncate md:line-clamp-2 md:whitespace-normal">
              {row.primaryTitle?.trim() || 'Untitled row'}
            </span>
          </div>
        </div>
        <div className="flex shrink-0 flex-wrap justify-end gap-2">
          <button className={secondaryButtonClass} type="button" onClick={onToggle}>
            {expanded ? 'Collapse' : 'Edit row'}
          </button>
          <button
            className={secondaryButtonClass}
            disabled={!expanded || busy}
            title={expanded ? undefined : 'Edit the row before revalidating it.'}
            type="button"
            onClick={onRevalidate}
          >
            {savePending ? 'Saving...' : 'Revalidate row'}
          </button>
          <button className="inline-flex min-h-10 items-center justify-center gap-2 rounded-md border border-rose-900 bg-rose-950 px-4 py-2 text-sm font-semibold text-rose-200 transition hover:bg-rose-900 disabled:cursor-not-allowed disabled:border-slate-700 disabled:bg-slate-900 disabled:text-slate-500" disabled={busy} type="button" onClick={onDelete}>
            <Trash2 className="h-4 w-4" />
            {deletePending ? 'Removing...' : 'Remove row'}
          </button>
        </div>
      </div>

      <div className="grid gap-2 rounded-xl border border-amber-900/60 bg-slate-950 p-3 text-sm text-slate-200">
        {row.errors.map((error) => <p key={error}>{error}</p>)}
      </div>

      {children}
    </div>
  )
}

function useDeleteImportRowMutation(sessionId: string, row: BookImportRowDto, onSessionChange: (session: BookImportSessionDto) => void) {
  return useMutation({
    mutationFn: () => api.deleteBookImportRow(sessionId, row.rowId),
    onSuccess: (session) => {
      onSessionChange(session)
      toast.success(`Line ${row.lineNumber} removed.`)
    },
    onError: (error) => {
      toast.error(error instanceof HttpError ? error.apiError.detail : 'Could not remove import row.')
    },
  })
}

function SummaryCard({ label, value, tone = 'neutral' }: { label: string; value: string; tone?: 'neutral' | 'ok' | 'warn' }) {
  const toneClass = tone === 'ok'
    ? 'border-emerald-900/60 bg-emerald-950/40 text-emerald-200'
    : tone === 'warn'
      ? 'border-amber-900/60 bg-amber-950/40 text-amber-200'
      : 'border-slate-700 bg-slate-900 text-slate-100'

  return (
    <div className={`grid gap-0.5 rounded-xl border px-3 py-1.5 ${toneClass}`}>
      <div className="text-[0.68rem] font-semibold uppercase tracking-wide">{label}</div>
      <div className="truncate text-sm font-semibold">{value}</div>
    </div>
  )
}

function formatImportCount(value: number) {
  return new Intl.NumberFormat('en-US').format(value)
}

function LabeledInput({
  error = [],
  label,
  suggestions = [],
  value,
  onChange,
}: {
  error?: string[]
  label: string
  suggestions?: string[]
  value: string
  onChange: (value: string) => void
}) {
  const errorMessage = error.join(' ')
  const listboxId = useId()
  const containerRef = useRef<HTMLLabelElement | null>(null)
  const inputRef = useRef<HTMLInputElement | null>(null)
  const [open, setOpen] = useState(false)
  const normalizedValue = value.trim().toLowerCase()
  const matchingSuggestions = suggestions.filter((suggestion) => suggestion.toLowerCase().includes(normalizedValue))
  const hasExactSuggestionMatch = suggestions.some((suggestion) => suggestion.toLowerCase() === normalizedValue)
  const filteredSuggestions = normalizedValue.length > 0 && matchingSuggestions.length > 0 && !hasExactSuggestionMatch
    ? matchingSuggestions
    : suggestions

  useEffect(() => {
    if (!suggestions.length || !open) {
      return
    }

    const handlePointerDown = (event: PointerEvent) => {
      if (!containerRef.current?.contains(event.target as Node)) {
        setOpen(false)
      }
    }

    document.addEventListener('pointerdown', handlePointerDown)
    return () => document.removeEventListener('pointerdown', handlePointerDown)
  }, [open, suggestions.length])

  const showSuggestions = suggestions.length > 0 && open && filteredSuggestions.length > 0
  const closeSuggestions = () => setOpen(false)
  const openSuggestions = () => {
    if (suggestions.length > 0) {
      setOpen(true)
    }
  }
  const selectSuggestion = (suggestion: string) => {
    onChange(suggestion)
    setOpen(false)
    inputRef.current?.focus()
  }

  return (
    <label ref={containerRef} className="grid gap-1 text-sm">
      <span className="font-medium text-slate-300">{label}</span>
      <div className="relative">
        <input
          ref={inputRef}
          aria-controls={showSuggestions ? listboxId : undefined}
          aria-expanded={suggestions.length > 0 ? open : undefined}
          aria-autocomplete={suggestions.length > 0 ? 'list' : undefined}
          aria-invalid={errorMessage ? 'true' : undefined}
          className={`${inputClass} ${suggestions.length > 0 ? 'pr-10' : ''} ${errorMessage ? '!border-rose-500 focus:!border-rose-400 focus:ring-rose-400/20' : ''}`}
          role={suggestions.length > 0 ? 'combobox' : undefined}
          value={value}
          onBlur={(event) => {
            if (!containerRef.current?.contains(event.relatedTarget as Node | null)) {
              closeSuggestions()
            }
          }}
          onChange={(event) => {
            onChange(event.target.value)
            openSuggestions()
          }}
          onFocus={openSuggestions}
        />
        {suggestions.length > 0 ? (
          <button
            aria-label={`Show ${label.toLowerCase()} suggestions`}
            className="absolute inset-y-0 right-0 inline-flex w-10 items-center justify-center text-slate-400 transition hover:text-slate-200"
            type="button"
            onClick={() => {
              if (open) {
                closeSuggestions()
                return
              }

              inputRef.current?.focus()
              openSuggestions()
            }}
          >
            <ChevronDown className={`h-4 w-4 transition ${open ? 'rotate-180' : ''}`} />
          </button>
        ) : null}
        {showSuggestions ? (
          <div
            className="absolute left-0 right-0 top-full z-20 mt-1 overflow-hidden rounded-md border border-slate-700 bg-slate-900 shadow-xl"
            id={listboxId}
            role="listbox"
          >
            {filteredSuggestions.map((suggestion) => (
              <button
                key={suggestion}
                className={`block w-full px-3 py-2 text-left text-sm transition hover:bg-slate-800 hover:text-slate-50 ${suggestion === value ? 'bg-slate-800 text-slate-50' : 'text-slate-200'}`}
                type="button"
                onMouseDown={(event) => event.preventDefault()}
                onClick={() => selectSuggestion(suggestion)}
              >
                {suggestion}
              </button>
            ))}
          </div>
        ) : null}
      </div>
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
        className={`${inputClass} min-h-28 ${errorMessage ? '!border-rose-500 focus:!border-rose-400 focus:ring-rose-400/20' : ''}`}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </label>
  )
}
