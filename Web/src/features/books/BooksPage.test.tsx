import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { api } from '@/api/client'
import { books, paginated } from '@/test/fixtures'
import { renderWithProviders } from '@/test/render'
import { BooksPage, defaultColumnPreferences, formatProgress, getColumnPopupPosition, getVisibleColumns, readCardsPerRow } from './BooksPage'

vi.mock('@/api/client', () => ({
  api: {
    getBooks: vi.fn(),
    downloadBooksExport: vi.fn(),
  },
}))

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
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

  it('keeps top action buttons with balanced icon spacing', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    expect(screen.getByRole('button', { name: /export filtered csv/i })).toHaveClass('gap-2.5', 'pl-3.5', 'pr-4')
    expect(screen.getByRole('button', { name: /import csv/i })).toHaveClass('gap-2.5', 'pl-3.5', 'pr-4')
    expect(screen.getByRole('link', { name: /add book/i })).toHaveClass('gap-2.5', 'pl-3.5', 'pr-4')
  })

  it('exports the current filtered and sorted books to csv', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))
    vi.mocked(api.downloadBooksExport).mockResolvedValue(new Blob(['csv'], { type: 'text/csv' }))
    const createObjectUrl = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:books-export')
    const revokeObjectUrl = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {})
    const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books?query=author%3AToika&sortBy=title&sortDirection=asc' })

    await screen.findByText('Lord of Mysteries')
    await user.click(screen.getByRole('button', { name: /export filtered csv/i }))

    await waitFor(() => expect(api.downloadBooksExport).toHaveBeenCalledWith({
      query: 'author:Toika',
      sortBy: 'title',
      sortDirection: 'asc',
    }))
    expect(clickSpy).toHaveBeenCalledTimes(1)
    expect(createObjectUrl).toHaveBeenCalledTimes(1)
    expect(revokeObjectUrl).toHaveBeenCalledWith('blob:books-export')

    createObjectUrl.mockRestore()
    revokeObjectUrl.mockRestore()
    clickSpy.mockRestore()
  })

  it('lets the user change cards per row and persists the preference', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))
    const user = userEvent.setup()

    const { container } = renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    await user.click(screen.getByRole('button', { name: /cards/i }))
    await user.selectOptions(screen.getByRole('combobox', { name: /cards per row/i }), '6')

    expect(window.localStorage.getItem('novelki.books.cards-per-row.v1')).toBe('6')
    expect(container.querySelector('.lg\\:grid-cols-6')).toBeTruthy()
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

    expect(screen.getAllByText('Reading')[0].parentElement).toHaveClass('bg-indigo-600/95', 'text-white')
    expect(screen.getAllByText('Completed')[0].parentElement).toHaveClass('bg-emerald-600/95', 'text-white')
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
      total: 220,
      data: books,
    })
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    expect(screen.getByRole('button', { name: '1' })).toHaveAttribute('aria-current', 'page')
    expect(screen.getByRole('button', { name: '2' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /last page/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /jump between pages/i })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: '2' }))

    await waitFor(() => expect(api.getBooks).toHaveBeenLastCalledWith({
      skip: 20,
      take: 20,
      query: '',
      sortBy: 'lastModified',
      sortDirection: 'desc',
    }))

    await user.click(screen.getByRole('button', { name: /next page/i }))

    await waitFor(() => expect(api.getBooks).toHaveBeenLastCalledWith({
      skip: 40,
      take: 20,
      query: '',
      sortBy: 'lastModified',
      sortDirection: 'desc',
    }))

    await user.click(screen.getByRole('button', { name: /jump between pages/i }))
    const jumpInput = screen.getByRole('textbox', { name: /page number/i })
    await user.type(jumpInput, '11')
    await user.keyboard('{Enter}')

    await waitFor(() => expect(api.getBooks).toHaveBeenLastCalledWith({
      skip: 200,
      take: 20,
      query: '',
      sortBy: 'lastModified',
      sortDirection: 'desc',
    }))
  })

  it('renders the list footer after table and cards content without sticky spacing', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))
    const user = userEvent.setup()

    const { container } = renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    let footer = container.querySelector('section > .border-t.border-slate-200.bg-white')
    expect(footer).toHaveTextContent('Per page')
    expect(container.querySelector('.overflow-x-auto.pb-24')).toBeFalsy()
    expect(container.querySelector('section > .sticky.bottom-0')).toBeFalsy()

    await user.click(screen.getByRole('button', { name: /cards/i }))

    footer = container.querySelector('section > .border-t.border-slate-200.bg-white')
    expect(footer).toHaveTextContent('Per page')
    expect(container.querySelector('.grid.gap-4.p-4.pb-24')).toBeFalsy()
    expect(container.querySelector('section > .sticky.bottom-0')).toBeFalsy()
  })

  it('keeps the page jump popover open for invalid page numbers', async () => {
    vi.mocked(api.getBooks).mockResolvedValue({
      skip: 0,
      take: 20,
      total: 220,
      data: books,
    })
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    await user.click(screen.getByRole('button', { name: /jump between pages/i }))

    const jumpInput = screen.getByRole('textbox', { name: /page number/i })
    await user.type(jumpInput, '0')
    await user.keyboard('{Enter}')

    expect(jumpInput).toHaveAttribute('aria-invalid', 'true')
    expect(api.getBooks).toHaveBeenCalledTimes(1)
  })

  it('focuses the page jump input immediately after opening the gap popover', async () => {
    vi.mocked(api.getBooks).mockResolvedValue({
      skip: 0,
      take: 20,
      total: 220,
      data: books,
    })
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    await user.click(screen.getByRole('button', { name: /jump between pages/i }))

    expect(screen.getByRole('textbox', { name: /page number/i })).toHaveFocus()
  })

  it('keeps only one page gap popover open at a time', async () => {
    vi.mocked(api.getBooks).mockResolvedValue({
      skip: 100,
      take: 20,
      total: 420,
      data: books,
    })
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books?skip=100&take=20' })

    await screen.findByText('Lord of Mysteries')
    const gapButtons = screen.getAllByRole('button', { name: /jump between pages/i })
    expect(gapButtons).toHaveLength(2)

    await user.click(gapButtons[0])
    expect(screen.getByRole('textbox', { name: /page number/i })).toBeInTheDocument()
    expect(gapButtons[0]).toHaveAttribute('aria-expanded', 'true')
    expect(gapButtons[1]).toHaveAttribute('aria-expanded', 'false')

    await user.click(gapButtons[1])
    expect(screen.getByRole('textbox', { name: /page number/i })).toBeInTheDocument()
    expect(gapButtons[0]).toHaveAttribute('aria-expanded', 'false')
    expect(gapButtons[1]).toHaveAttribute('aria-expanded', 'true')
  })

  it('preserves bottom anchoring when changing pages near the page bottom', async () => {
    vi.mocked(api.getBooks).mockResolvedValue({
      skip: 0,
      take: 20,
      total: 220,
      data: books,
    })
    const user = userEvent.setup()
    const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => {})

    Object.defineProperty(window, 'innerHeight', { configurable: true, value: 800 })
    Object.defineProperty(window, 'scrollY', { configurable: true, value: 1190 })
    Object.defineProperty(document.documentElement, 'scrollHeight', { configurable: true, value: 2000 })

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    await user.click(screen.getByRole('button', { name: '2' }))

    await waitFor(() => {
      expect(scrollTo).toHaveBeenCalledWith({ top: 1190 })
    })

    scrollTo.mockRestore()
  })

  it('shows scroll shortcut buttons and scrolls to the top or bottom on click', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))
    const user = userEvent.setup()
    const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => {})
    Object.defineProperty(window, 'innerHeight', { configurable: true, value: 800 })
    Object.defineProperty(document.documentElement, 'scrollHeight', { configurable: true, value: 2400 })
    Object.defineProperty(window, 'scrollY', { configurable: true, value: 0 })

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    expect(screen.queryByRole('button', { name: /back to top/i })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /go to bottom/i })).toBeInTheDocument()

    Object.defineProperty(window, 'scrollY', { configurable: true, value: 640 })
    window.dispatchEvent(new Event('scroll'))

    const backToTopButton = await screen.findByRole('button', { name: /back to top/i })
    const goToBottomButton = screen.getByRole('button', { name: /go to bottom/i })
    await user.click(backToTopButton)
    await user.click(goToBottomButton)

    expect(scrollTo).toHaveBeenCalledWith({ top: 0, behavior: 'smooth' })
    expect(scrollTo).toHaveBeenCalledWith({ top: 2400, behavior: 'smooth' })
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

  it('falls back to four cards per row for invalid stored values', () => {
    window.localStorage.setItem('novelki.books.cards-per-row.v1', '99')

    expect(readCardsPerRow('novelki.books.cards-per-row.v1')).toBe(4)
  })
})
