import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '@/api/client'
import { readReadingTimeSettings, readingTimeStorageKey } from '@/features/analytics/readingTimeSettings'
import { expectReadableTextContrast } from '@/test/contrast'
import { bookListItems, books, dictionaries, paginated, statuses } from '@/test/fixtures'
import { renderWithProviders } from '@/test/render'
import { BooksPage, defaultColumnPreferences, formatAverageRating, formatProgress, getCardDetailRowClass, getCardTextSizeClasses, getColumnPopupPosition, getVisibleColumns, readCardsPerRow } from './BooksPage'

vi.mock('@/api/client', () => ({
  api: {
    getBooks: vi.fn(),
    getBooksSummary: vi.fn(),
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
  beforeEach(() => {
    vi.mocked(api.getBooks).mockReset()
    vi.mocked(api.getBooksSummary).mockReset()
  })

  it('renders books in table view and fetches with default list params', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(bookListItems))

    renderWithProviders(<BooksPage />, { route: '/books' })

    expect(await screen.findByText('Lord of Mysteries')).toBeInTheDocument()
    expect(screen.getByText('Cuttlefish')).toBeInTheDocument()
    expect(api.getBooks).toHaveBeenCalledWith(expect.objectContaining({
      skip: 0,
      take: 20,
      query: '',
      sortBy: 'lastModified',
      sortDirection: 'desc',
    }))
  })

  it('keeps table actions isolated from crowded columns', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(bookListItems))

    const { container } = renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    expect(container.querySelector('table')).toHaveClass('min-w-[72rem]')
    expect(screen.getByRole('columnheader', { name: /actions/i })).toHaveClass('sticky', 'right-0', 'w-32')
  })

  it('shows the colon-based rating operator syntax in advanced search help', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(bookListItems))

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    expect(screen.getByPlaceholderText(/rating:>=8/i)).toBeInTheDocument()
    expect(screen.getByText('rating:>=8')).toBeInTheDocument()
    expect(screen.getByText('rating:8')).toBeInTheDocument()
    expect(screen.getByText('progress:>=50')).toBeInTheDocument()
    expect(screen.getByText('chapters:<200')).toBeInTheDocument()
    expect(screen.getByText('total:>500')).toBeInTheDocument()
    expect(screen.getByText('total-chapters:>500')).toBeInTheDocument()
  })

  it('preserves spaces in the search input while sending a trimmed query to the api', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(bookListItems))
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    const searchInput = screen.getByPlaceholderText(/rating:>=8/i)
    await user.clear(searchInput)
    await user.type(searchInput, 'status:plan to read ')

    expect(searchInput).toHaveValue('status:plan to read ')
    await waitFor(() => expect(api.getBooks).toHaveBeenLastCalledWith(expect.objectContaining({
      query: 'status:plan to read',
    })))
  })

  it('keeps the caret position when inserting a space in the middle of the search', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(bookListItems))
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books?query=title%3ALordofMysteries' })

    await screen.findByText('Lord of Mysteries')
    const searchInput = screen.getByPlaceholderText(/rating:>=8/i) as HTMLInputElement
    searchInput.focus()
    searchInput.setSelectionRange('title:Lord'.length, 'title:Lord'.length)
    await user.keyboard(' ')

    expect(searchInput).toHaveValue('title:Lord ofMysteries')
    expect(searchInput.selectionStart).toBe('title:Lord '.length)
    expect(searchInput.selectionEnd).toBe('title:Lord '.length)
  })

  it('switches to cards view and persists the preference', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(bookListItems))
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    await user.click(screen.getByRole('button', { name: /cards/i }))

    expect(window.localStorage.getItem('novelki.books.layout.v1')).toBe('cards')
    expect(screen.getByRole('heading', { name: /A Very Long Book Title/ })).toBeInTheDocument()
  })

  it('keeps top action buttons with balanced icon spacing', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(bookListItems))

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    const topButtons = screen.getAllByRole('button')
    expect(topButtons.findIndex((button) => /summary/i.test(button.textContent ?? ''))).toBeLessThan(
      topButtons.findIndex((button) => /export filtered csv/i.test(button.textContent ?? '')),
    )
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

  it('does not fetch books summary before the panel is expanded', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))
    vi.mocked(api.getBooksSummary).mockResolvedValue(createSummary())

    renderWithProviders(<BooksPage />, { route: '/books?query=author%3AToika&sortBy=title&sortDirection=asc' })

    await screen.findByText('Lord of Mysteries')
    expect(api.getBooksSummary).not.toHaveBeenCalled()
  })

  it('expands the summary panel above search and fetches summary by query only', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))
    vi.mocked(api.getBooksSummary).mockResolvedValue(createSummary())
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books?query=author%3AToika&sortBy=title&sortDirection=asc' })

    await screen.findByText('Lord of Mysteries')
    await user.click(screen.getByRole('button', { name: /summary/i }))

    expect(await screen.findByRole('heading', { name: /library summary/i })).toBeInTheDocument()
    expect(api.getBooksSummary).toHaveBeenCalledWith({ query: 'author:Toika' })
    expect(screen.getByText('Total books')).toBeInTheDocument()
    expect(screen.getByText('Average rating')).toBeInTheDocument()
    const summarySection = screen.getByRole('heading', { name: /library summary/i }).closest('section')
    expect(summarySection).not.toBeNull()
    expect(screen.getByText('Status distribution')).toBeInTheDocument()
    expect(screen.getByText('Book types')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /open analytics/i })).toBeInTheDocument()
    const searchText = screen.getByText(/supports filters like/i)
    expect((summarySection?.compareDocumentPosition(searchText) ?? 0) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })

  it('shows a summary empty state when no books match the current filters', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated([]))
    vi.mocked(api.getBooksSummary).mockResolvedValue({
      totalBooks: 0,
      ratedBooks: 0,
      unratedBooks: 0,
      averageRating: null,
      currentChapters: 0,
      booksWithKnownCurrentChapter: 0,
      booksWithoutKnownCurrentChapter: 0,
      statusCounts: [],
      typeCounts: [],
      genreCounts: [],
      ratingCounts: [],
    })
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books?query=author%3AMissing' })

    await screen.findByText('No books match the current filters.')
    await user.click(screen.getByRole('button', { name: /summary/i }))

    expect(await screen.findByRole('heading', { name: /library summary/i })).toBeInTheDocument()
    expect(screen.getAllByText('No books match the current filters.')).toHaveLength(2)
    expect(screen.getAllByText('0')).toHaveLength(4)
  })

  it('links summary to analytics with the current complex query', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))
    vi.mocked(api.getBooksSummary).mockResolvedValue(createSummary())
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books?query=author%3AToika%20title%3A%22Lord%20of%20Mysteries%22' })

    await screen.findByText('Lord of Mysteries')
    await user.click(screen.getByRole('button', { name: /summary/i }))

    const link = await screen.findByRole('link', { name: /open analytics/i })
    expect(link).toHaveAttribute('href', '/analytics?query=author%3AToika+title%3A%22Lord+of+Mysteries%22')
    expect(screen.getByText('Status distribution')).toBeInTheDocument()
    expect(screen.getByText('Book types')).toBeInTheDocument()
    expect(screen.queryByText('Estimated reading time')).not.toBeInTheDocument()
  })

  it('moves date query filters into analytics from/to parameters', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))
    vi.mocked(api.getBooksSummary).mockResolvedValue(createSummary())
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books?query=author%3AToika%20created%3A%3D2026%20updated%3A%3C%3D07.2026' })

    await screen.findByText('Lord of Mysteries')
    await user.click(screen.getByRole('button', { name: /summary/i }))

    expect(await screen.findByRole('link', { name: /open analytics/i })).toHaveAttribute(
      'href',
      '/analytics?query=author%3AToika&from=2026-01-01&to=2026-08-01',
    )
  })

  it('keeps summary book type labels readable against their calculated background', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))
    vi.mocked(api.getBooksSummary).mockResolvedValue(createSummary())
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    await user.click(screen.getByRole('button', { name: /summary/i }))

    expectReadableTextContrast((await screen.findAllByText('Novel'))[0])
  })

  it('links summary to analytics without query and preserves shared reading-time settings', async () => {
    window.localStorage.setItem(readingTimeStorageKey, JSON.stringify({ Novel: 2 }))
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))
    vi.mocked(api.getBooksSummary).mockResolvedValue(createSummary())
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    await user.click(screen.getByRole('button', { name: /summary/i }))

    expect(await screen.findByRole('link', { name: /open analytics/i })).toHaveAttribute('href', '/analytics')
    expect(readReadingTimeSettings()).toEqual({ Novel: 2 })
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

  it('shows card-specific field options in the columns popup for cards view', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    await user.click(screen.getByRole('button', { name: /cards/i }))
    await user.click(screen.getByRole('button', { name: /^columns$/i }))

    expect(screen.getByText('Visible card fields')).toBeInTheDocument()
    expect(screen.getByText('Alternative titles')).toBeInTheDocument()
    expect(screen.getByText('Type')).toBeInTheDocument()
    expect(screen.queryByText('Tags')).not.toBeInTheDocument()
    expect(screen.queryByText('Description')).not.toBeInTheDocument()
    expect(screen.queryByText('Chapters')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Up' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Down' })).not.toBeInTheDocument()
  })

  it('hides and shows card fields based on cards view preferences', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    await user.click(screen.getByRole('button', { name: /cards/i }))

    expect(screen.getByText('Cuttlefish')).toBeInTheDocument()
    expect(screen.queryByText('LOTM')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /^columns$/i }))
    await user.click(screen.getByRole('button', { name: /author/i }))
    await user.click(screen.getByRole('button', { name: /alternative titles/i }))
    await user.click(screen.getByRole('button', { name: /type/i }))

    expect(screen.queryByText('Cuttlefish')).not.toBeInTheDocument()
    expect(screen.getByText('LOTM')).toBeInTheDocument()
    expect(screen.getAllByText('Novel')[0]).toHaveClass('font-semibold', 'italic')
    expect(window.localStorage.getItem('novelki.books.card-fields.v1')).toContain('"alternativeTitles"')
  })

  it('updates query params and refetches when sorting by title', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    await user.click(screen.getByRole('button', { name: /title/i }))

    await waitFor(() => expect(api.getBooks).toHaveBeenLastCalledWith(expect.objectContaining({
      skip: 0,
      take: 20,
      query: '',
      sortBy: 'title',
      sortDirection: 'asc',
    })))
  })

  it('sorts by total chapters from the table column', async () => {
    window.localStorage.setItem('novelki.books.columns.v1', JSON.stringify([
      { id: 'totalChapters', visible: true },
    ]))
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    await user.click(screen.getByRole('button', { name: /total chapters|total/i }))

    await waitFor(() => expect(api.getBooks).toHaveBeenLastCalledWith(expect.objectContaining({
      skip: 0,
      take: 20,
      query: '',
      sortBy: 'chapters',
      sortDirection: 'asc',
    })))
  })

  it('renders the total chapters column label in the shared column settings', async () => {
    window.localStorage.setItem('novelki.books.columns.v1', JSON.stringify([
      { id: 'totalChapters', visible: true },
    ]))
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    await user.click(screen.getByRole('button', { name: /^columns$/i }))

    expect(screen.getAllByText('Total chapters').length).toBeGreaterThan(0)
  })

  it('cycles status sorting through the domain order on repeated clicks', async () => {
    vi.mocked(api.getBooks).mockImplementation(async (params) => {
      if (params.advanceCycle) {
        return {
          ...paginated(books),
          data: params.sortDirection === statuses[0].name ? [books[1], books[0]] : [books[0], books[1]],
        }
      }

      return paginated(books)
    })
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    const statusHeader = screen.getByRole('button', { name: /status/i })

    await user.click(statusHeader)
    await waitFor(() => expect(api.getBooks).toHaveBeenLastCalledWith(expect.objectContaining({
      sortBy: 'status',
      advanceCycle: true,
    })))

    await user.click(statusHeader)
    await waitFor(() => expect(api.getBooks).toHaveBeenLastCalledWith(expect.objectContaining({
      sortBy: 'status',
      sortDirection: statuses[0].name,
      advanceCycle: true,
    })))

    await user.click(statusHeader)
    await waitFor(() => expect(api.getBooks).toHaveBeenLastCalledWith(expect.objectContaining({
      sortBy: 'status',
      sortDirection: statuses[1].name,
      advanceCycle: true,
    })))
  })

  it('does not refetch after syncing the resolved cyclic sort direction into the url', async () => {
    vi.mocked(api.getBooks).mockImplementation(async (params) => {
      if (params.advanceCycle) {
        return {
          ...paginated(books),
          data: [books[0], books[1]],
        }
      }

      return paginated(books)
    })
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    const initialCalls = vi.mocked(api.getBooks).mock.calls.length

    await user.click(screen.getByRole('button', { name: /status/i }))

    await waitFor(() => expect(api.getBooks).toHaveBeenCalledWith(expect.objectContaining({
      sortBy: 'status',
      advanceCycle: true,
    })))
    await waitFor(() => expect(vi.mocked(api.getBooks).mock.calls.length).toBe(initialCalls + 1))
  })

  it('cycles type sorting through the domain order on repeated clicks', async () => {
    const typeBooks = [
      books[0],
      {
        ...books[1],
        contentType: 'Manga',
      },
    ]
    vi.mocked(api.getBooks).mockImplementation(async (params) => {
      if (params.advanceCycle) {
        return {
          ...paginated(typeBooks),
          data: params.sortDirection === dictionaries[0].name ? [typeBooks[1], typeBooks[0]] : [typeBooks[0], typeBooks[1]],
        }
      }

      return paginated(typeBooks)
    })
    const user = userEvent.setup()

    renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    const typeHeader = screen.getByRole('button', { name: /type/i })

    await user.click(typeHeader)
    await waitFor(() => expect(api.getBooks).toHaveBeenLastCalledWith(expect.objectContaining({
      sortBy: 'type',
      advanceCycle: true,
    })))

    await user.click(typeHeader)
    await waitFor(() => expect(api.getBooks).toHaveBeenLastCalledWith(expect.objectContaining({
      sortBy: 'type',
      sortDirection: dictionaries[0].name,
      advanceCycle: true,
    })))

    await user.click(typeHeader)
    await waitFor(() => expect(api.getBooks).toHaveBeenLastCalledWith(expect.objectContaining({
      sortBy: 'type',
      sortDirection: dictionaries[1].name,
      advanceCycle: true,
    })))
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

    await waitFor(() => expect(api.getBooks).toHaveBeenLastCalledWith(expect.objectContaining({
      skip: 20,
      take: 20,
      query: '',
      sortBy: 'lastModified',
      sortDirection: 'desc',
    })))

    await user.click(screen.getByRole('button', { name: /next page/i }))

    await waitFor(() => expect(api.getBooks).toHaveBeenLastCalledWith(expect.objectContaining({
      skip: 40,
      take: 20,
      query: '',
      sortBy: 'lastModified',
      sortDirection: 'desc',
    })))

    await user.click(screen.getByRole('button', { name: /jump between pages/i }))
    const jumpInput = screen.getByRole('textbox', { name: /page number/i })
    await user.type(jumpInput, '11')
    await user.keyboard('{Enter}')

    await waitFor(() => expect(api.getBooks).toHaveBeenLastCalledWith(expect.objectContaining({
      skip: 200,
      take: 20,
      query: '',
      sortBy: 'lastModified',
      sortDirection: 'desc',
    })))
  })

  it('renders the list footer after table and cards content without sticky spacing', async () => {
    vi.mocked(api.getBooks).mockResolvedValue(paginated(books))
    const user = userEvent.setup()

    const { container } = renderWithProviders(<BooksPage />, { route: '/books' })

    await screen.findByText('Lord of Mysteries')
    expect(container.querySelector('.app-scrollbar.overflow-x-auto')).toBeTruthy()
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

  it('formats average rating for summary cards', () => {
    expect(formatAverageRating(8)).toBe('8.0')
    expect(formatAverageRating(8.25)).toBe('8.3')
    expect(formatAverageRating(null)).toBe('-')
  })

  it('falls back to four cards per row for invalid stored values', () => {
    window.localStorage.setItem('novelki.books.cards-per-row.v1', '99')

    expect(readCardsPerRow('novelki.books.cards-per-row.v1')).toBe(4)
  })

  it('scales card text sizes with the cards-per-row setting', () => {
    expect(getCardTextSizeClasses(4)).toEqual({
      title: 'text-lg',
      meta: 'text-base',
    })
    expect(getCardTextSizeClasses(6)).toEqual({
      title: 'text-base',
      meta: 'text-sm',
    })
    expect(getCardTextSizeClasses(8)).toEqual({
      title: 'text-sm',
      meta: 'text-xs',
    })
  })

  it('builds card detail rows only from globally enabled card fields', () => {
    expect(getCardDetailRowClass({
      showAlternativeTitles: false,
      showAuthor: false,
      showProgress: false,
      showTitle: true,
      showType: false,
    })).toBe('grid-rows-[minmax(0,2.5rem)]')

    expect(getCardDetailRowClass({
      showAlternativeTitles: false,
      showAuthor: true,
      showProgress: true,
      showTitle: true,
      showType: true,
    })).toBe('grid-rows-[minmax(0,2.5rem)_minmax(0,1.5rem)_minmax(0,1.75rem)]')
  })

  function createSummary() {
    return {
      totalBooks: 2,
      ratedBooks: 1,
      unratedBooks: 1,
      averageRating: 9,
      currentChapters: 468,
      booksWithKnownCurrentChapter: 2,
      booksWithoutKnownCurrentChapter: 0,
      statusCounts: [
        { status: 'Reading', count: 1 },
        { status: 'Completed', count: 1 },
      ],
      typeCounts: [
        { type: 'Novel', bookCount: 2, currentChapters: 468 },
      ],
      genreCounts: [
        { genre: 'Fantasy', bookCount: 1 },
        { genre: 'Slice of Life', bookCount: 1 },
      ],
      ratingCounts: [
        { rating: 9, bookCount: 1 },
      ],
    }
  }
})
