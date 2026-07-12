import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { api } from '@/api/client'
import { dictionaries, genres, paginated, statuses } from '@/test/fixtures'
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
    expect(currentChapterInput).toHaveAttribute('inputmode', 'decimal')
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
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.createBook).toHaveBeenCalled())
    expect(vi.mocked(api.createBook).mock.calls[0][0].totalChapters).toBeNull()
  })

  it('blocks non-numeric characters in number fields while typing', async () => {
    const user = userEvent.setup()
    renderWithProviders(<BookFormPage mode="create" />, { route: '/books/new' })

    await screen.findByText('Add book')

    const currentChapterInput = screen.getByLabelText('Current chapter')
    const totalChaptersInput = screen.getByLabelText('Total chapters')
    const priorityInput = screen.getByLabelText('Priority 1-5')

    await user.type(currentChapterInput, '12e-3abc')
    await user.type(totalChaptersInput, '4..5x')
    await user.type(priorityInput, '2-4a')

    expect(currentChapterInput).toHaveValue('123')
    expect(totalChaptersInput).toHaveValue('4.5')
    expect(priorityInput).toHaveValue('24')
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
})
