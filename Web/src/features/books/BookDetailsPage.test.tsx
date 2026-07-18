import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { Route, Routes } from 'react-router-dom'
import { api } from '@/api/client'
import { books, genres } from '@/test/fixtures'
import { renderWithProviders } from '@/test/render'
import { BookDetailsPage, getProgressHistoryWithDeltas } from './BookDetailsPage'

vi.mock('@/api/client', () => ({
  api: {
    getBook: vi.fn(),
    getGenres: vi.fn(),
    deleteBook: vi.fn(),
    refreshBookCover: vi.fn(),
  },
}))

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}))

vi.mock('./BookCoverSection', () => ({
  BookCoverArtwork: ({ title, hint }: { title: string; hint?: string }) => <div>{title} cover{hint}</div>,
  CoverLightbox: () => null,
  useResolvedCoverImage: () => null,
}))

vi.mock('./ProgressDialog', () => ({
  ProgressDialog: () => <button type="button">Progress</button>,
}))

describe('BookDetailsPage', () => {
  it('computes chapter deltas from sorted progress history entries', () => {
    expect(getProgressHistoryWithDeltas([
      {
        id: 'older',
        changedAt: '2026-01-01T10:00:00Z',
        chapterNumber: 10,
      },
      {
        id: 'latest',
        changedAt: '2026-01-03T10:00:00Z',
        chapterNumber: 16,
      },
      {
        id: 'middle',
        changedAt: '2026-01-02T10:00:00Z',
        chapterNumber: 12,
      },
      {
        id: 'label-only',
        changedAt: '2026-01-04T10:00:00Z',
        chapterLabel: 'Side story',
      },
    ])).toEqual([
      {
        id: 'label-only',
        changedAt: '2026-01-04T10:00:00Z',
        chapterLabel: 'Side story',
        delta: null,
      },
      {
        id: 'latest',
        changedAt: '2026-01-03T10:00:00Z',
        chapterNumber: 16,
        delta: 4,
      },
      {
        id: 'middle',
        changedAt: '2026-01-02T10:00:00Z',
        chapterNumber: 12,
        delta: 2,
      },
      {
        id: 'older',
        changedAt: '2026-01-01T10:00:00Z',
        chapterNumber: 10,
        delta: null,
      },
    ])
  })

  it('renders positive and negative deltas in the changelog header', async () => {
    vi.mocked(api.getGenres).mockResolvedValue({ skip: 0, take: 100, total: genres.length, data: genres })
    vi.mocked(api.getBook).mockResolvedValue({
      ...books[0],
      progressHistory: [
        {
          id: 'latest',
          changedAt: '2026-02-03T10:00:00Z',
          chapterNumber: 20,
          chapterLabel: '20',
        },
        {
          id: 'mid',
          changedAt: '2026-02-02T10:00:00Z',
          chapterNumber: 14,
          chapterLabel: '14',
        },
        {
          id: 'older',
          changedAt: '2026-02-01T10:00:00Z',
          chapterNumber: 18,
          chapterLabel: '18',
        },
      ],
    })

    renderWithProviders(
      <Routes>
        <Route element={<BookDetailsPage />} path="/books/:id" />
      </Routes>,
      { route: '/books/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' },
    )

    expect(await screen.findByText('+6')).toHaveClass('bg-emerald-100', 'text-emerald-700')
    expect(screen.getByText('-4')).toHaveClass('bg-rose-100', 'text-rose-700')
  })

  it('uses the shared dialog surface and readable title hierarchy', async () => {
    vi.mocked(api.getGenres).mockResolvedValue({ skip: 0, take: 100, total: genres.length, data: genres })
    vi.mocked(api.getBook).mockResolvedValue(books[0])
    const user = userEvent.setup()

    renderWithProviders(
      <Routes>
        <Route element={<BookDetailsPage />} path="/books/:id" />
      </Routes>,
      { route: '/books/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' },
    )

    await screen.findByText('Lord of Mysteries')
    await user.click(screen.getByRole('button', { name: /^delete$/i }))

    const dialog = screen.getByRole('dialog')
    const panel = dialog.firstElementChild

    expect(screen.getByRole('heading', { name: /delete book/i })).toHaveClass('text-slate-950')
    expect(within(dialog).getByText('Lord of Mysteries')).toHaveClass('text-slate-950')
    expect(panel).toHaveClass('ui-dialog-panel')
  })

  it('replaces a technical cover download error with a helpful generic message', async () => {
    vi.mocked(api.getGenres).mockResolvedValue({ skip: 0, take: 100, total: genres.length, data: genres })
    vi.mocked(api.getBook).mockResolvedValue({
      ...books[0],
      cover: {
        id: 'cover-1',
        status: 'Failed',
        failureReason: 'Response status code does not indicate success: 301 (Moved Permanently).',
      },
    })

    renderWithProviders(
      <Routes>
        <Route element={<BookDetailsPage />} path="/books/:id" />
      </Routes>,
      { route: '/books/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' },
    )

    expect(await screen.findByText(/A cover was found, but the image could not be downloaded/)).toBeInTheDocument()
    expect(screen.queryByText(/301 \(Moved Permanently\)/)).not.toBeInTheDocument()
  })
})
