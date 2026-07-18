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
    createTag: vi.fn(),
    createAuthor: vi.fn(),
    updateTag: vi.fn(),
    deleteTag: vi.fn(),
    updateAuthor: vi.fn(),
    updateAuthorVisibility: vi.fn(),
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
  isGlobal: false,
}

const author = {
  id: '22222222-2222-2222-2222-222222222222',
  primaryName: 'Er Gen',
  otherNames: ['耳根'],
  isPublic: false,
}

describe('ManagePage', () => {
  beforeEach(() => {
    vi.mocked(api.searchTags).mockReset().mockResolvedValue([tag])
    vi.mocked(api.searchAuthors).mockReset().mockResolvedValue([author])
    vi.mocked(api.createTag).mockReset().mockResolvedValue(tag)
    vi.mocked(api.createAuthor).mockReset().mockResolvedValue(author)
    vi.mocked(api.updateTag).mockReset().mockResolvedValue(tag)
    vi.mocked(api.deleteTag).mockReset().mockResolvedValue(undefined)
    vi.mocked(api.updateAuthor).mockReset().mockResolvedValue(author)
    vi.mocked(api.updateAuthorVisibility).mockReset().mockResolvedValue(author)
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

  it('creates tags and authors from the active section', async () => {
    const user = userEvent.setup()
    renderWithProviders(<ManagePage />)

    await user.click(await screen.findByRole('button', { name: 'Add tag' }))
    await user.type(screen.getByLabelText('Tag name'), 'to review')
    await user.type(screen.getByLabelText('Description'), 'Needs another look')
    await user.click(screen.getByRole('button', { name: 'Create tag' }))
    await waitFor(() => expect(api.createTag).toHaveBeenCalledWith({
      name: 'to review',
      description: 'Needs another look',
    }))

    await user.click(screen.getByRole('tab', { name: 'Authors' }))
    await user.click(await screen.findByRole('button', { name: 'Add author' }))
    await user.type(screen.getByLabelText('Primary name'), 'New Author')
    await user.type(screen.getByLabelText('Alternative names'), 'Pen Name')
    await user.click(screen.getByRole('button', { name: 'Create author' }))
    await waitFor(() => expect(api.createAuthor).toHaveBeenCalledWith({
      primaryName: 'New Author',
      otherNames: ['Pen Name'],
    }))
  })

  it('shows global tags as read-only', async () => {
    vi.mocked(api.searchTags).mockResolvedValue([{ ...tag, id: 'global-tag', name: 'official', isGlobal: true }])
    renderWithProviders(<ManagePage />)

    expect(await screen.findByText('Global')).toBeInTheDocument()
    expect(screen.getByText('Managed by admin')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Edit tag official' })).not.toBeInTheDocument()
  })

  it('publishes a private author from the editor', async () => {
    const user = userEvent.setup()
    renderWithProviders(<ManagePage />)

    await user.click(screen.getByRole('tab', { name: 'Authors' }))
    expect(await screen.findByText('Private')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Edit author Er Gen' }))
    await user.click(screen.getByRole('button', { name: 'Make public' }))

    await waitFor(() => expect(api.updateAuthorVisibility).toHaveBeenCalledWith(author.id, true))
  })
})
