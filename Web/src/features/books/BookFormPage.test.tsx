import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { Route, Routes } from 'react-router-dom'
import { api } from '@/api/client'
import { books, dictionaries, genres, paginated, statuses } from '@/test/fixtures'
import { renderWithProviders } from '@/test/render'
import { BookFormPage } from './BookFormPage'

vi.mock('@/api/client', () => ({
  api: {
    getBook: vi.fn(),
    getAdminBook: vi.fn(),
    getTypes: vi.fn(),
    getStatuses: vi.fn(),
    getGenres: vi.fn(),
    searchAuthors: vi.fn(),
    searchTags: vi.fn(),
    getBooks: vi.fn(),
    createBook: vi.fn(),
    updateBook: vi.fn(),
    updateAdminBook: vi.fn(),
    uploadBookCover: vi.fn(),
    setBookCoverFromUrl: vi.fn(),
    deleteBookCover: vi.fn(),
  },
}))

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    warning: vi.fn(),
  },
}))

describe('BookFormPage', () => {
  beforeEach(() => {
    vi.mocked(api.getTypes).mockResolvedValue(paginated(dictionaries))
    vi.mocked(api.getStatuses).mockResolvedValue(paginated(statuses))
    vi.mocked(api.getGenres).mockResolvedValue(paginated(genres))
    vi.mocked(api.searchAuthors).mockResolvedValue([])
    vi.mocked(api.searchTags).mockResolvedValue([])
    vi.mocked(api.getBooks).mockResolvedValue({ skip: 0, take: 10, total: 0, data: [] })
    vi.mocked(api.createBook).mockResolvedValue({ id: 'book-1' })
  })

  it('disables native browser validation for numeric fields and keeps the controlled inputs text-based', async () => {
    renderWithProviders(<BookFormPage mode="create" />, { route: '/books/new' })

    const form = await screen.findByRole('button', { name: 'Save' })
    const currentChapterInput = screen.getByLabelText('Current chapter') as HTMLInputElement
    const totalChaptersInput = screen.getByLabelText('Total chapters') as HTMLInputElement
    const priorityInput = screen.getByLabelText('Priority 1-5') as HTMLInputElement

    expect(form.closest('form')).toHaveAttribute('novalidate')
    expect(currentChapterInput.type).toBe('text')
    expect(currentChapterInput).toHaveAttribute('inputmode', 'numeric')
    expect(totalChaptersInput.type).toBe('text')
    expect(totalChaptersInput).toHaveAttribute('inputmode', 'decimal')
    expect(priorityInput.type).toBe('text')
    expect(priorityInput).toHaveAttribute('inputmode', 'numeric')
  })

  it('rejects total chapters equal to zero with a field error', async () => {
    const user = userEvent.setup()
    renderWithProviders(<BookFormPage mode="create" />, { route: '/books/new' })

    await screen.findByText('Add book')
    await user.type(screen.getByLabelText('Primary title'), 'Shadow Slave')
    await user.type(screen.getByLabelText('Author'), 'Guiltythree')
    await user.type(screen.getByLabelText('Current chapter'), '0')
    await user.type(screen.getByLabelText('Total chapters'), '0')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('Total chapters must be greater than 0.')).toBeInTheDocument()
    expect(api.createBook).not.toHaveBeenCalled()
  })

  it('submits blank total chapters as null', async () => {
    const user = userEvent.setup()
    renderWithProviders(<BookFormPage mode="create" />, { route: '/books/new' })

    await screen.findByText('Add book')
    await user.type(screen.getByLabelText('Primary title'), 'Shadow Slave')
    await user.type(screen.getByLabelText('Author'), 'Guiltythree')
    await user.type(screen.getByLabelText('Current chapter'), '0')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.createBook).toHaveBeenCalled())
    expect(vi.mocked(api.createBook).mock.calls[0][0].totalChapters).toBeNull()
    expect(vi.mocked(api.createBook).mock.calls[0][0].currentChapterNumber).toBe(0)
  })

  it('requires current chapter and only accepts digits', async () => {
    const user = userEvent.setup()
    renderWithProviders(<BookFormPage mode="create" />, { route: '/books/new' })

    await screen.findByText('Add book')
    await user.type(screen.getByLabelText('Primary title'), 'Shadow Slave')
    await user.click(screen.getByLabelText('Current chapter'))
    await user.tab()

    expect(screen.queryByText('Current chapter is required.')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('Current chapter is required.')).toBeInTheDocument()
    expect(api.createBook).not.toHaveBeenCalled()

    const currentChapterInput = screen.getByLabelText('Current chapter')
    await user.type(currentChapterInput, '12e-3.4abc')
    expect(currentChapterInput).toHaveValue('1234')
  })

  it('normalizes optional decimal and integer fields while typing', async () => {
    const user = userEvent.setup()
    renderWithProviders(<BookFormPage mode="create" />, { route: '/books/new' })

    await screen.findByText('Add book')

    const totalChaptersInput = screen.getByLabelText('Total chapters')
    const priorityInput = screen.getByLabelText('Priority 1-5')

    await user.type(totalChaptersInput, '4..5x')
    await user.type(priorityInput, '2-4a')

    expect(totalChaptersInput).toHaveValue('4.5')
    expect(priorityInput).toHaveValue('24')
  })

  it('previews and selects a rating through the compact star scale', async () => {
    const user = userEvent.setup()
    renderWithProviders(<BookFormPage mode="create" />, { route: '/books/new' })

    await screen.findByText('Add book')
    const ratingSeven = screen.getByRole('button', { name: 'Set rating to 7/10' })

    expect(screen.getByText('?/10')).toBeInTheDocument()
    await user.hover(ratingSeven)
    expect(screen.getByText('7/10')).toBeInTheDocument()

    await user.click(ratingSeven)
    await user.unhover(ratingSeven)
    expect(screen.getByText('7/10')).toBeInTheDocument()
    expect(ratingSeven).toHaveAttribute('aria-pressed', 'true')

    await user.click(ratingSeven)
    expect(screen.getByText('?/10')).toBeInTheDocument()
  })

  it('renders type and status options in domain order', async () => {
    renderWithProviders(<BookFormPage mode="create" />, { route: '/books/new' })

    await screen.findByText('Add book')

    const [typeSelect, statusSelect] = screen.getAllByRole('combobox')
    const typeOptions = typeSelect.querySelectorAll('option')
    const statusOptions = statusSelect.querySelectorAll('option')

    expect(Array.from(typeOptions).map((option) => option.textContent)).toEqual(['Novel', 'Manga'])
    expect(Array.from(statusOptions).map((option) => option.textContent)).toEqual(['Reading', 'Completed'])
  })

  it('orders the editor around identity, cover, reading state, and optional details', async () => {
    renderWithProviders(<BookFormPage mode="create" />, { route: '/books/new' })

    const identity = await screen.findByRole('heading', { name: 'Book identity' })
    const cover = screen.getByTestId('book-cover-editor')
    const reading = screen.getByRole('heading', { name: 'Reading state' })
    const details = screen.getByRole('heading', { name: 'Library details' })

    expect(identity.compareDocumentPosition(cover) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(cover.compareDocumentPosition(reading) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(reading.compareDocumentPosition(details) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(screen.getByTestId('book-form-rail')).toContainElement(screen.getByRole('button', { name: 'Save' }))
  })

  it('shows the matched alias but submits the primary author identity', async () => {
    const user = userEvent.setup()
    vi.mocked(api.searchAuthors).mockResolvedValue([{
      id: 'author-1',
      primaryName: 'Er Gen',
      otherNames: ['Ergen'],
      isPublic: true,
    }])
    renderWithProviders(<BookFormPage mode="create" />, { route: '/books/new' })

    await screen.findByText('Add book')
    await user.type(screen.getByLabelText('Primary title'), 'Renegade Immortal')
    await user.type(screen.getByLabelText('Author'), 'Ergen')
    expect(await screen.findByRole('button', { name: /Er Gen \(Ergen\).*Public/i })).toBeInTheDocument()
    await user.click(screen.getByLabelText('Current chapter'))

    expect(screen.getByLabelText('Author')).toHaveValue('Er Gen (Ergen)')
    await user.type(screen.getByLabelText('Current chapter'), '1')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.createBook).toHaveBeenCalled())
    expect(vi.mocked(api.createBook).mock.calls[0][0]).toMatchObject({
      authorId: 'author-1',
      authorName: 'Er Gen',
    })
  })

  it('hides a technical cover download error on the edit page', async () => {
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
        <Route element={<BookFormPage mode="edit" />} path="/books/:id/edit" />
      </Routes>,
      { route: '/books/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/edit' },
    )

    expect(await screen.findByText(
      'A cover was found, but the image could not be downloaded. Try searching again or upload a cover manually.',
    )).toBeInTheDocument()
    expect(screen.queryByText(/301 \(Moved Permanently\)/)).not.toBeInTheDocument()
  })
})
