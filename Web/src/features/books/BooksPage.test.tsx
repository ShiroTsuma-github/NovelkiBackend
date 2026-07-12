import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { api } from '@/api/client'
import { books, paginated } from '@/test/fixtures'
import { renderWithProviders } from '@/test/render'
import { BooksPage, defaultColumnPreferences, formatProgress, getColumnPopupPosition, getVisibleColumns } from './BooksPage'

vi.mock('@/api/client', () => ({
  api: {
    getBooks: vi.fn(),
  },
}))

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
  },
}))

describe('BooksPage', () => {
  it('renders books in table view and fetches with default list params', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))

    renderWithProviders(<BooksPage />, { route: '/books' })

    expect(await screen.findByText('Lord of Mysteries')).toBeInTheDocument()
    expect(screen.getByText('Cuttlefish')).toBeInTheDocument()
    expect(api.getBooks).toHaveBeenCalledWith({
      skip: 0,
      take: 20,
      query: '',
      sortBy: 'lastModified',
      sortDirection: 'desc',
    })
  })

  it('switches to cards view and persists the preference', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    await user.click(screen.getByRole('button', { name: /cards/i }))

    expect(window.localStorage.getItem('novelki.books.layout.v1')).toBe('cards')
    expect(screen.getByRole('heading', { name: /A Very Long Book Title/ })).toBeInTheDocument()
  })

  it('shows rating overlays only on cards with a rating', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    await user.click(screen.getByRole('button', { name: /cards/i }))

    const ratingBadge = screen.getAllByText('9')[0]
    expect(ratingBadge).toHaveClass('min-h-10', 'min-w-10', 'bg-emerald-500/95')
  })

  it('shows status overlays on cards with status-specific colors', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    await user.click(screen.getByRole('button', { name: /cards/i }))

    expect(screen.getAllByText('Reading')[0].parentElement).toHaveClass('bg-emerald-50/95', 'text-emerald-700')
    expect(screen.getAllByText('Completed')[0].parentElement).toHaveClass('bg-cyan-50/95', 'text-cyan-700')
  })

  it('updates query params and refetches when sorting by title', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    await user.click(screen.getByRole('button', { name: /title/i }))

    await waitFor(() => expect(api.getBooks).toHaveBeenLastCalledWith({
      skip: 0,
      take: 20,
      query: '',
      sortBy: 'title',
      sortDirection: 'asc',
    }))
  })

  it('shows page numbers and supports jumping to a specific page', async () => {
    vi.mocked(api.getBooks).mockResolvedValue({
      skip: 0,
      take: 20,
      total: 120,
      data: books,
    })
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    expect(screen.getByRole('button', { name: '1' })).toHaveAttribute('aria-current', 'page')
    expect(screen.getByRole('button', { name: '2' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /last/i })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: '2' }))

    await waitFor(() => expect(api.getBooks).toHaveBeenLastCalledWith({
      skip: 20,
      take: 20,
      query: '',
      sortBy: 'lastModified',
      sortDirection: 'desc',
    }))

    const jumpInput = screen.getByRole('textbox', { name: /jump to page/i })
    await user.clear(jumpInput)
    await user.type(jumpInput, '999')
    await user.click(screen.getByRole('button', { name: /^go$/i }))

    await waitFor(() => expect(api.getBooks).toHaveBeenLastCalledWith({
      skip: 100,
      take: 20,
      query: '',
      sortBy: 'lastModified',
      sortDirection: 'desc',
    }))
  })

  it('shows a back to top button after scrolling and scrolls to the top on click', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))
    const user = userEvent.setup()
    const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => {})

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    expect(screen.queryByRole('button', { name: /back to top/i })).not.toBeInTheDocument()

    Object.defineProperty(window, 'scrollY', { configurable: true, value: 640 })
    window.dispatchEvent(new Event('scroll'))

    const button = await screen.findByRole('button', { name: /back to top/i })
    await user.click(button)

    expect(scrollTo).toHaveBeenCalledWith({ top: 0, behavior: 'smooth' })
    scrollTo.mockRestore()
  })

  it('keeps column visibility helpers deterministic', () => {
    const columns = [
      { id: 'title', label: 'Title', defaultVisible: true, render: () => 'Title' },
      { id: 'notes', label: 'Notes', defaultVisible: false, render: () => 'Notes' },
    ]

    const preferences = defaultColumnPreferences(columns)

    expect(preferences).toEqual([
      { id: 'title', visible: true },
      { id: 'notes', visible: false },
    ])
    expect(getVisibleColumns(columns, preferences).map((column) => column.id)).toEqual(['title'])
  })

  it('calculates a predictable columns popup position on desktop and mobile', () => {
    expect(getColumnPopupPosition({ left: 400, right: 520, bottom: 100 } as DOMRect, 1280, 720)).toEqual({
      left: '200px',
      top: '110px',
    })

    expect(getColumnPopupPosition({ left: 40, right: 180, bottom: 80 } as DOMRect, 390, 844)).toEqual({
      left: '16px',
      top: '90px',
    })
  })

  it('formats progress with chapter label and total', () => {
    expect(formatProgress(books[0])).toBe('348 / 1432')
    expect(formatProgress({ ...books[0], currentChapterLabel: 'Side Story', currentChapterNumber: null })).toBe('Side Story / 1432')
    expect(formatProgress({ ...books[0], currentChapterLabel: null, currentChapterNumber: null, totalChapters: null })).toBe('-')
  })
})
