import { fireEvent, screen, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { api } from '@/api/client'
import type { BookImportSessionDto } from '@/api/types'
import { renderWithProviders } from '@/test/render'
import { ImportBooksDialog } from './ImportBooksDialog'

vi.mock('@/api/client', () => ({
  api: {
    createBookImportSession: vi.fn(),
    downloadBookImportTemplate: vi.fn(),
    updateBookImportRow: vi.fn(),
    deleteBookImportRow: vi.fn(),
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
    expect(screen.getByRole('button', { name: /collapse/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /revalidate row/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /remove row/i })).toBeInTheDocument()
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

    expect(await screen.findAllByText('Content type is required and must exist.')).toHaveLength(3)

    const typeInput = screen.getByLabelText(/^Type/)
    const titleInput = screen.getByLabelText(/^Title/)

    expect(typeInput).toHaveAttribute('aria-invalid', 'true')
    expect(typeInput).toHaveClass('border-rose-500')
    expect(titleInput).not.toHaveAttribute('aria-invalid')
  })
})

const importSessionWithFieldErrors: BookImportSessionDto = {
  sessionId: 'session-1',
  fileName: 'books.csv',
  totalRows: 1,
  validRows: 0,
  invalidRows: 1,
  canFinalize: false,
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
