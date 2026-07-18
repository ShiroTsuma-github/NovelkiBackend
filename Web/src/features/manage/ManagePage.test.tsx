import { fireEvent, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '@/api/client'
import { renderWithProviders } from '@/test/render'
import { ManagePage } from './ManagePage'

vi.mock('@/api/client', () => ({
  api: {
    searchTags: vi.fn(),
    searchAuthors: vi.fn(),
    updateTag: vi.fn(),
    deleteTag: vi.fn(),
    updateAuthor: vi.fn(),
    deleteAuthor: vi.fn(),
  },
}))

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}))

const tag = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'favorite',
  description: 'The best ones',
  color: null,
}

const author = {
  id: '22222222-2222-2222-2222-222222222222',
  primaryName: 'Er Gen',
  otherNames: ['耳根'],
}

describe('ManagePage', () => {
  beforeEach(() => {
    vi.mocked(api.searchTags).mockReset().mockResolvedValue([tag])
    vi.mocked(api.searchAuthors).mockReset().mockResolvedValue([author])
    vi.mocked(api.updateTag).mockReset().mockResolvedValue(tag)
    vi.mocked(api.deleteTag).mockReset().mockResolvedValue(undefined)
    vi.mocked(api.updateAuthor).mockReset().mockResolvedValue(author)
    vi.mocked(api.deleteAuthor).mockReset().mockResolvedValue(undefined)
  })

  it('opens a tag on double click and saves its description', async () => {
    const user = userEvent.setup()
    renderWithProviders(<ManagePage />)

    fireEvent.doubleClick(await screen.findByText('favorite'))
    const description = screen.getByLabelText('Description')
    await user.clear(description)
    await user.type(description, 'Worth rereading')
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() => expect(api.updateTag).toHaveBeenCalledWith(tag.id, {
      description: 'Worth rereading',
    }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('searches authors by alias and saves one alternative name per line', async () => {
    const user = userEvent.setup()
    renderWithProviders(<ManagePage />)

    await user.click(screen.getByRole('tab', { name: 'Authors' }))
    const search = screen.getByPlaceholderText('Search authors or aliases…')
    await user.type(search, 'Cuttlefish')
    await waitFor(() => expect(api.searchAuthors).toHaveBeenCalledWith('Cuttlefish', 50, true))

    await user.click(await screen.findByRole('button', { name: 'Edit author Er Gen' }))
    const aliases = screen.getByLabelText('Alternative names')
    await user.clear(aliases)
    await user.type(aliases, '耳根{enter}Ergen')
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() => expect(api.updateAuthor).toHaveBeenCalledWith(author.id, {
      otherNames: ['耳根', 'Ergen'],
    }))
  })

  it('requires a second click before deleting a tag', async () => {
    const user = userEvent.setup()
    renderWithProviders(<ManagePage />)

    await user.click(await screen.findByRole('button', { name: 'Edit tag favorite' }))
    await user.click(screen.getByRole('button', { name: 'Delete tag' }))
    expect(api.deleteTag).not.toHaveBeenCalled()

    await user.click(screen.getByRole('button', { name: 'Confirm delete' }))
    await waitFor(() => expect(api.deleteTag).toHaveBeenCalledWith(tag.id))
  })
})
