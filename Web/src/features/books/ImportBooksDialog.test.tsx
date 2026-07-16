import { fireEvent, screen, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { api } from '@/api/client'
import type { BookImportFinalizeResult, BookImportSessionDto } from '@/api/types'
import { toast } from 'sonner'
import { renderWithProviders } from '@/test/render'
import { getImportSessionStats, ImportBooksDialog } from './ImportBooksDialog'

vi.mock('@/api/client', () => ({
  api: {
    createBookImportSession: vi.fn(),
    downloadBookImportTemplate: vi.fn(),
    updateBookImportRow: vi.fn(),
    deleteBookImportRow: vi.fn(),
    deleteInvalidBookImportRows: vi.fn(),
    finalizeBookImport: vi.fn(),
    cancelBookImport: vi.fn(),
  },
}))

vi.mock('react-virtuoso', () => ({
  Virtuoso: ({ data, itemContent }: { data: unknown[]; itemContent: (index: number, item: unknown) => ReactNode }) => (
    <div>{data.map((item, index) => <div key={(item as { rowId: string }).rowId}>{itemContent(index, item)}</div>)}</div>
  ),
}))

vi.mock('sonner', () => ({
  toast: {
    error: vi.fn(),
    success: vi.fn(),
  },
}))

describe('ImportBooksDialog', () => {
  it('calculates import session stats without dividing empty sessions by zero', () => {
    const stats = getImportSessionStats({
      ...importSessionWithFieldErrors,
      invalidRows: 0,
      rows: [],
      totalRows: 0,
      validRows: 0,
    })

    expect(stats.invalidRows).toEqual([])
    expect(stats.invalidRowsCount).toBe(0)
    expect(stats.progressPercent).toBe(0)
  })

  it('creates an import session when a csv file is dropped into the dialog', async () => {
    vi.mocked(api.createBookImportSession).mockResolvedValue(importSessionWithFieldErrors)

    renderWithProviders(
      <ImportBooksDialog open onClose={vi.fn()} onImported={vi.fn()} />,
    )

    const dropzone = screen.getByText(/drop a csv here or use file selection/i).closest('div')
    expect(dropzone).not.toBeNull()

    fireEvent.drop(dropzone!, {
      dataTransfer: {
        files: [new File(['primaryTitle,contentType,status'], 'books.csv', { type: 'text/csv' })],
      },
    })

    await waitFor(() => {
      expect(api.createBookImportSession).toHaveBeenCalledTimes(1)
    })
  })

  it('rejects dropped files that are not csv', async () => {
    renderWithProviders(
      <ImportBooksDialog open onClose={vi.fn()} onImported={vi.fn()} />,
    )

    const dropzone = screen.getByText(/drop a csv here or use file selection/i).closest('div')
    expect(dropzone).not.toBeNull()

    fireEvent.drop(dropzone!, {
      dataTransfer: {
        files: [new File(['not,csv'], 'books.txt', { type: 'text/plain' })],
      },
    })

    expect(api.createBookImportSession).not.toHaveBeenCalled()
    expect(toast.error).toHaveBeenCalledWith('Choose a .csv file.')
  })

  it('asks for confirmation before dismissing an active import session', async () => {
    vi.mocked(api.createBookImportSession).mockResolvedValue(importSessionWithFieldErrors)

    const onClose = vi.fn()
    const { container } = renderWithProviders(
      <ImportBooksDialog open onClose={onClose} onImported={vi.fn()} />,
    )

    const input = container.querySelector('input[type="file"]')
    expect(input).not.toBeNull()

    fireEvent.change(input!, {
      target: {
        files: [new File(['primaryTitle,contentType,status'], 'books.csv', { type: 'text/csv' })],
      },
    })

    await screen.findByText('Line 2')
    fireEvent.click(screen.getByRole('button', { name: /^close$/i }))

    expect(screen.getByRole('heading', { name: /cancel import\?/i })).toBeInTheDocument()
    expect(api.cancelBookImport).not.toHaveBeenCalled()
    expect(onClose).not.toHaveBeenCalled()

    fireEvent.click(screen.getByRole('button', { name: /keep editing/i }))

    await waitFor(() => {
      expect(screen.queryByRole('heading', { name: /cancel import\?/i })).not.toBeInTheDocument()
    })
    expect(onClose).not.toHaveBeenCalled()
  })

  it('cancels the import session after confirmation', async () => {
    vi.mocked(api.createBookImportSession).mockResolvedValue(importSessionWithFieldErrors)
    vi.mocked(api.cancelBookImport).mockResolvedValue(undefined)

    const onClose = vi.fn()
    const { container } = renderWithProviders(
      <ImportBooksDialog open onClose={onClose} onImported={vi.fn()} />,
    )

    const input = container.querySelector('input[type="file"]')
    expect(input).not.toBeNull()

    fireEvent.change(input!, {
      target: {
        files: [new File(['primaryTitle,contentType,status'], 'books.csv', { type: 'text/csv' })],
      },
    })

    await screen.findByText('Line 2')
    fireEvent.click(screen.getByRole('button', { name: /^close$/i }))
    fireEvent.click(screen.getByRole('button', { name: /cancel import/i }))

    await waitFor(() => {
      expect(api.cancelBookImport).toHaveBeenCalledWith('session-1')
      expect(onClose).toHaveBeenCalledTimes(1)
    })
  })

  it('downloads the CSV template from the import dialog', async () => {
    const createObjectUrl = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:template')
    const revokeObjectUrl = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {})
    const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    vi.mocked(api.downloadBookImportTemplate).mockResolvedValue(new Blob(['primaryTitle,contentType,status\n'], { type: 'text/csv' }))

    renderWithProviders(
      <ImportBooksDialog open onClose={vi.fn()} onImported={vi.fn()} />,
    )

    fireEvent.click(screen.getByRole('button', { name: /download template/i }))

    await waitFor(() => {
      expect(api.downloadBookImportTemplate).toHaveBeenCalledTimes(1)
      expect(clickSpy).toHaveBeenCalledTimes(1)
      expect(createObjectUrl).toHaveBeenCalledTimes(1)
      expect(revokeObjectUrl).toHaveBeenCalledWith('blob:template')
    })

    createObjectUrl.mockRestore()
    revokeObjectUrl.mockRestore()
    clickSpy.mockRestore()
  })

  it('keeps row actions visible for long invalid row titles', async () => {
    vi.mocked(api.createBookImportSession).mockResolvedValue(importSessionWithLongTitle)

    const { container } = renderWithProviders(
      <ImportBooksDialog open onClose={vi.fn()} onImported={vi.fn()} />,
    )

    const input = container.querySelector('input[type="file"]')
    expect(input).not.toBeNull()

    fireEvent.change(input!, {
      target: {
        files: [new File(['primaryTitle,contentType,status'], 'books.csv', { type: 'text/csv' })],
      },
    })

    const title = await screen.findByTitle(importSessionWithLongTitle.rows[0].primaryTitle!)
    expect(title).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /edit row/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /revalidate row/i })).toBeDisabled()
    expect(screen.getByRole('button', { name: /remove row/i })).toBeInTheDocument()
  })

  it('mounts import row form controls only after opening a row for editing', async () => {
    vi.mocked(api.createBookImportSession).mockResolvedValue(importSessionWithFieldErrors)

    const { container } = renderWithProviders(
      <ImportBooksDialog open onClose={vi.fn()} onImported={vi.fn()} />,
    )

    const input = container.querySelector('input[type="file"]')
    expect(input).not.toBeNull()

    fireEvent.change(input!, {
      target: {
        files: [new File(['primaryTitle,contentType,status'], 'books.csv', { type: 'text/csv' })],
      },
    })

    expect(await screen.findByRole('button', { name: /edit row/i })).toBeInTheDocument()
    expect(screen.queryByLabelText(/^Title/)).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /revalidate row/i })).toBeDisabled()

    fireEvent.click(screen.getByRole('button', { name: /edit row/i }))

    expect(screen.getByRole('button', { name: /collapse/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /revalidate row/i })).not.toBeDisabled()
    expect(screen.getByLabelText(/^Title/)).toBeInTheDocument()
  })

  it('keeps the import dialog inside the viewport and gives invalid rows the scroll area', async () => {
    vi.mocked(api.createBookImportSession).mockResolvedValue(importSessionWithFieldErrors)

    const { container } = renderWithProviders(
      <ImportBooksDialog open onClose={vi.fn()} onImported={vi.fn()} />,
    )

    const input = container.querySelector('input[type="file"]')
    expect(input).not.toBeNull()

    fireEvent.change(input!, {
      target: {
        files: [new File(['primaryTitle,contentType,status'], 'books.csv', { type: 'text/csv' })],
      },
    })

    expect(await screen.findByTestId('import-dialog-panel')).toHaveClass('h-[calc(100vh-0.5rem)]', 'overflow-hidden')
    expect(screen.getByRole('dialog')).toHaveClass('overflow-hidden')
    expect(screen.getByTestId('import-invalid-rows-panel')).toHaveClass('min-h-0', 'flex-1', 'overflow-hidden')
  })

  it('marks import row fields with field errors', async () => {
    vi.mocked(api.createBookImportSession).mockResolvedValue(importSessionWithFieldErrors)

    const { container } = renderWithProviders(
      <ImportBooksDialog open onClose={vi.fn()} onImported={vi.fn()} />,
    )

    const input = container.querySelector('input[type="file"]')
    expect(input).not.toBeNull()

    fireEvent.change(input!, {
      target: {
        files: [new File(['primaryTitle,contentType,status'], 'books.csv', { type: 'text/csv' })],
      },
    })

    expect(await screen.findAllByText(/Content type is required and must exist\./)).toHaveLength(1)
    fireEvent.click(screen.getByRole('button', { name: /edit row/i }))

    const typeInput = screen.getByLabelText(/^Type/)
    const titleInput = screen.getByLabelText(/^Title/)

    expect(typeInput).toHaveAttribute('aria-invalid', 'true')
    expect(typeInput).toHaveClass('!border-rose-500')
    expect(titleInput).not.toHaveAttribute('aria-invalid')
  })

  it('shows type and status suggestions for invalid import rows', async () => {
    vi.mocked(api.createBookImportSession).mockResolvedValue(importSessionWithFieldErrors)

    const { container } = renderWithProviders(
      <ImportBooksDialog open onClose={vi.fn()} onImported={vi.fn()} />,
    )

    const input = container.querySelector('input[type="file"]')
    expect(input).not.toBeNull()

    fireEvent.change(input!, {
      target: {
        files: [new File(['primaryTitle,contentType,status'], 'books.csv', { type: 'text/csv' })],
      },
    })

    await screen.findByRole('button', { name: /edit row/i })
    fireEvent.click(screen.getByRole('button', { name: /edit row/i }))

    const typeInput = await screen.findByRole('combobox', { name: /^Type/ })
    const statusInput = screen.getByRole('combobox', { name: /^Status/ })

    fireEvent.focus(typeInput)
    expect(await screen.findByRole('listbox')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Novel' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Manga' })).toBeInTheDocument()

    fireEvent.focus(statusInput)
    expect(screen.getByRole('button', { name: 'Reading' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Completed' })).toBeInTheDocument()
  })

  it('does not repeat field error text under the inputs', async () => {
    vi.mocked(api.createBookImportSession).mockResolvedValue(importSessionWithFieldErrors)

    const { container } = renderWithProviders(
      <ImportBooksDialog open onClose={vi.fn()} onImported={vi.fn()} />,
    )

    const input = container.querySelector('input[type="file"]')
    expect(input).not.toBeNull()

    fireEvent.change(input!, {
      target: {
        files: [new File(['primaryTitle,contentType,status'], 'books.csv', { type: 'text/csv' })],
      },
    })

    expect(await screen.findAllByText(/Content type is required and must exist\./)).toHaveLength(1)
    expect(screen.queryByText('Suggestions: Novel, Manga')).not.toBeInTheDocument()
    expect(screen.queryByText('Suggestions: Reading, Completed')).not.toBeInTheDocument()
  })

  it('discards all invalid rows and keeps valid rows in session summary', async () => {
    vi.mocked(api.createBookImportSession).mockResolvedValue(importSessionWithMixedRows)
    vi.mocked(api.deleteInvalidBookImportRows).mockResolvedValue(importSessionAfterDiscardInvalid)

    const { container } = renderWithProviders(
      <ImportBooksDialog open onClose={vi.fn()} onImported={vi.fn()} />,
    )

    const input = container.querySelector('input[type="file"]')
    expect(input).not.toBeNull()

    fireEvent.change(input!, {
      target: {
        files: [new File(['primaryTitle,contentType,status'], 'books.csv', { type: 'text/csv' })],
      },
    })

    const discardInvalidButton = await screen.findByRole('button', { name: /discard all invalid/i })
    const closeButton = screen.getByRole('button', { name: /^close$/i })
    const finalizeButton = screen.getByRole('button', { name: /finalize import/i })

    expect((closeButton.compareDocumentPosition(discardInvalidButton) & Node.DOCUMENT_POSITION_FOLLOWING)).toBeTruthy()
    expect((discardInvalidButton.compareDocumentPosition(finalizeButton) & Node.DOCUMENT_POSITION_FOLLOWING)).toBeTruthy()

    fireEvent.click(discardInvalidButton)

    await waitFor(() => {
      expect(api.deleteInvalidBookImportRows).toHaveBeenCalledWith('session-mixed')
    })

    expect(screen.queryByRole('button', { name: /discard all invalid/i })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /finalize import/i })).toBeEnabled()
    expect(screen.getByText('All rows are valid')).toBeInTheDocument()
  })

  it('shows imported titles with type and list-formatted progress after finalizing', async () => {
    vi.mocked(api.createBookImportSession).mockResolvedValue(importSessionAfterDiscardInvalid)
    vi.mocked(api.finalizeBookImport).mockResolvedValue(importFinalizeFullSuccess)
    const onClose = vi.fn()
    const onImported = vi.fn()

    const { container } = renderWithProviders(
      <ImportBooksDialog open onClose={onClose} onImported={onImported} />,
    )

    const input = container.querySelector('input[type="file"]')
    expect(input).not.toBeNull()

    fireEvent.change(input!, {
      target: {
        files: [new File(['primaryTitle,contentType,status'], 'books.csv', { type: 'text/csv' })],
      },
    })

    expect(await screen.findByText('All rows are valid')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: /finalize import/i }))

    expect(await screen.findByText('Imported books')).toBeInTheDocument()
    expect(screen.getByText('Imported')).toBeInTheDocument()
    expect(screen.getByText('Skipped')).toBeInTheDocument()
    expect(screen.getByText('The Novel')).toBeInTheDocument()
    expect(screen.getByText('Novel')).toBeInTheDocument()
    expect(screen.getByText('Progress: 49 / 200')).toBeInTheDocument()
    expect(onImported).toHaveBeenCalledWith(importFinalizeFullSuccess)
    expect(onClose).not.toHaveBeenCalled()

    fireEvent.click(screen.getByRole('button', { name: /^close$/i }))

    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('keeps the imported books summary table scrollable for large imports', async () => {
    vi.mocked(api.createBookImportSession).mockResolvedValue(importSessionAfterDiscardInvalid)
    vi.mocked(api.finalizeBookImport).mockResolvedValue({
      ...importFinalizeFullSuccess,
      importedCount: 1_000,
      importedBooks: Array.from({ length: 30 }, (_unused, index) => ({
        ...importFinalizeFullSuccess.importedBooks[0],
        primaryTitle: `Imported Book ${index + 1}`,
      })),
    })

    const { container } = renderWithProviders(
      <ImportBooksDialog open onClose={vi.fn()} onImported={vi.fn()} />,
    )

    const input = container.querySelector('input[type="file"]')
    expect(input).not.toBeNull()

    fireEvent.change(input!, {
      target: {
        files: [new File(['primaryTitle,contentType,status'], 'books.csv', { type: 'text/csv' })],
      },
    })

    fireEvent.click(await screen.findByRole('button', { name: /finalize import/i }))

    expect(await screen.findByText('1,000')).toBeInTheDocument()
    expect(screen.getByText('Imported Book 30')).toBeInTheDocument()

    const summaryTable = screen.getByRole('table')
    expect(summaryTable.parentElement).toHaveClass('overflow-auto')
    expect(summaryTable.parentElement).toHaveClass('max-h-[min(28rem,55vh)]')
  })

  it('keeps partial finalize messages visible on the success screen', async () => {
    vi.mocked(api.createBookImportSession).mockResolvedValue(importSessionAfterDiscardInvalid)
    vi.mocked(api.finalizeBookImport).mockResolvedValue(importFinalizePartialSuccess)

    const { container } = renderWithProviders(
      <ImportBooksDialog open onClose={vi.fn()} onImported={vi.fn()} />,
    )

    const input = container.querySelector('input[type="file"]')
    expect(input).not.toBeNull()

    fireEvent.change(input!, {
      target: {
        files: [new File(['primaryTitle,contentType,status'], 'books.csv', { type: 'text/csv' })],
      },
    })

    fireEvent.click(await screen.findByRole('button', { name: /finalize import/i }))

    expect(await screen.findByText('Partial import messages')).toBeInTheDocument()
    expect(screen.getByText("Line 3: title 'Existing Book' already exists for content type 'Novel'.")).toBeInTheDocument()
    expect(screen.getByText('Skipped')).toBeInTheDocument()
  })

  it('shows an empty imported list when finalize imports zero books', async () => {
    vi.mocked(api.createBookImportSession).mockResolvedValue(importSessionAfterDiscardInvalid)
    vi.mocked(api.finalizeBookImport).mockResolvedValue(importFinalizeEmptySuccess)

    const { container } = renderWithProviders(
      <ImportBooksDialog open onClose={vi.fn()} onImported={vi.fn()} />,
    )

    const input = container.querySelector('input[type="file"]')
    expect(input).not.toBeNull()

    fireEvent.change(input!, {
      target: {
        files: [new File(['primaryTitle,contentType,status'], 'books.csv', { type: 'text/csv' })],
      },
    })

    fireEvent.click(await screen.findByRole('button', { name: /finalize import/i }))

    expect(await screen.findByText('No books were imported.')).toBeInTheDocument()
    expect(screen.getByText('Skipped')).toBeInTheDocument()
  })
})

const importSessionWithFieldErrors: BookImportSessionDto = {
  sessionId: 'session-1',
  fileName: 'books.csv',
  totalRows: 1,
  validRows: 0,
  invalidRows: 1,
  canFinalize: false,
  availableContentTypes: ['Novel', 'Manga'],
  availableStatuses: ['Reading', 'Completed'],
  rows: [
    {
      rowId: 'row-1',
      lineNumber: 2,
      isValid: false,
      primaryTitle: 'Book',
      authorName: null,
      contentType: 'Wrong',
      status: 'Reading',
      tags: null,
      totalChapters: null,
      currentChapterNumber: null,
      currentChapterLabel: null,
      rating: null,
      priority: null,
      description: null,
      notes: null,
      rawImportedLine: null,
      errors: ['Content type is required and must exist.'],
      fieldErrors: {
        contentType: ['Content type is required and must exist.'],
      },
    },
  ],
}

const importSessionWithLongTitle: BookImportSessionDto = {
  ...importSessionWithFieldErrors,
  rows: [
    {
      ...importSessionWithFieldErrors.rows[0],
      primaryTitle: 'A Very Long Imported Book Title That Should Stay Inside Its Header Without Pushing The Action Buttons Below Or Off The Right Edge',
    },
  ],
}

const importSessionWithMixedRows: BookImportSessionDto = {
  sessionId: 'session-mixed',
  fileName: 'books.csv',
  totalRows: 2,
  validRows: 1,
  invalidRows: 1,
  canFinalize: false,
  availableContentTypes: ['Novel', 'Manga'],
  availableStatuses: ['Reading', 'Completed'],
  rows: [
    {
      rowId: 'row-valid',
      lineNumber: 2,
      isValid: true,
      primaryTitle: 'Valid book',
      authorName: null,
      contentType: 'Novel',
      status: 'Reading',
      tags: null,
      totalChapters: null,
      currentChapterNumber: null,
      currentChapterLabel: null,
      rating: null,
      priority: null,
      description: null,
      notes: null,
      rawImportedLine: null,
      errors: [],
      fieldErrors: {},
    },
    {
      ...importSessionWithFieldErrors.rows[0],
      rowId: 'row-invalid',
      lineNumber: 3,
    },
  ],
}

const importSessionAfterDiscardInvalid: BookImportSessionDto = {
  ...importSessionWithMixedRows,
  totalRows: 1,
  validRows: 1,
  invalidRows: 0,
  canFinalize: true,
  rows: [importSessionWithMixedRows.rows[0]],
}

const importFinalizeFullSuccess: BookImportFinalizeResult = {
  importedCount: 1,
  skippedCount: 0,
  importedBooks: [
    {
      primaryTitle: 'The Novel',
      contentType: 'Novel',
      status: 'Reading',
      currentChapterNumber: 49,
      currentChapterLabel: 'Progress: 49',
      totalChapters: 200,
    },
  ],
  errors: [],
}

const importFinalizePartialSuccess: BookImportFinalizeResult = {
  importedCount: 1,
  skippedCount: 1,
  importedBooks: importFinalizeFullSuccess.importedBooks,
  errors: ["Line 3: title 'Existing Book' already exists for content type 'Novel'."],
}

const importFinalizeEmptySuccess: BookImportFinalizeResult = {
  importedCount: 0,
  skippedCount: 1,
  importedBooks: [],
  errors: ["Line 2: title 'Existing Book' already exists for content type 'Novel'."],
}
