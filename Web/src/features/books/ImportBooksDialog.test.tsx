import { fireEvent, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { api } from '@/api/client'
import type { BookImportSessionDto } from '@/api/types'
import { renderWithProviders } from '@/test/render'
import { ImportBooksDialog } from './ImportBooksDialog'

vi.mock('@/api/client', () => ({
  api: {
    createBookImportSession: vi.fn(),
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
