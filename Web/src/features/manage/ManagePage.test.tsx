import { fireEvent, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '@/api/client'
import { renderWithProviders } from '@/test/render'
import { bookListItems, paginated } from '@/test/fixtures'
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
    getBooks: vi.fn(),
    searchPublicBooks: vi.fn(),
    publishBook: vi.fn(),
    refreshPublishedBook: vi.fn(),
    unlistPublishedBook: vi.fn(),
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
  isOwned: true,
}

const publishedBook = {
  id: 'snapshot-1',
  sourceBookId: bookListItems[0].id,
  primaryTitle: bookListItems[0].primaryTitle,
  description: 'A snapshot',
  alternativeTitles: ['LOTM'],
  author: 'Cuttlefish',
  authorOtherNames: ['Cuttlefish That Loves Diving'],
  contentType: 'Novel',
  genres: [{ name: 'Fantasy', description: 'Magic' }],
  tags: [{ name: 'mystery', description: 'Secrets' }],
  coverUrl: null,
  snapshotAt: '2026-07-18T12:00:00Z',
  isOwner: true,
}

const managedBooks = [
  {
    ...bookListItems[0],
    cover: {
      status: 'Found' as const,
      imageUrl: '/api/book/cover/first',
      thumbnailImageUrl: '/api/book/cover/first/thumbnail',
    },
  },
  {
    ...bookListItems[1],
    description: 'A complete listing candidate',
    cover: {
      status: 'Uploaded' as const,
      imageUrl: '/api/book/cover',
      thumbnailImageUrl: '/api/book/cover/thumbnail',
    },
  },
]

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
    vi.mocked(api.getBooks).mockReset().mockResolvedValue(paginated(managedBooks))
    vi.mocked(api.searchPublicBooks).mockReset().mockResolvedValue(paginated([publishedBook]))
    vi.mocked(api.publishBook).mockReset().mockResolvedValue(publishedBook)
    vi.mocked(api.refreshPublishedBook).mockReset().mockResolvedValue(publishedBook)
    vi.mocked(api.unlistPublishedBook).mockReset().mockResolvedValue(undefined)
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
    await waitFor(() => expect(api.searchAuthors).toHaveBeenCalledWith('Cuttlefish', 50))

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
    expect(await screen.findByText('Yours')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Edit author Er Gen' }))
    await user.click(screen.getByRole('button', { name: 'Make public' }))

    await waitFor(() => expect(api.updateAuthorVisibility).toHaveBeenCalledWith(author.id, true))
  })

  it('shows cloned global author identities as read-only known authors', async () => {
    vi.mocked(api.searchAuthors).mockResolvedValue([{ ...author, id: 'global-author', isPublic: true, isOwned: false }])
    const user = userEvent.setup()
    renderWithProviders(<ManagePage />)

    await user.click(screen.getByRole('tab', { name: 'Authors' }))

    expect(await screen.findByText('Global')).toBeInTheDocument()
    expect(screen.getByText('Global identity')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Edit author Er Gen' })).not.toBeInTheDocument()
  })

  it('keeps the current users public authors editable', async () => {
    vi.mocked(api.searchAuthors).mockResolvedValue([{ ...author, isPublic: true, isOwned: true }])
    const user = userEvent.setup()
    renderWithProviders(<ManagePage />)

    await user.click(screen.getByRole('tab', { name: 'Authors' }))

    expect(await screen.findByText('Yours')).toBeInTheDocument()
    expect(screen.getByText('Public')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Edit author Er Gen' })).toBeInTheDocument()
  })

  it('lists, refreshes, and unlists books from the books section', async () => {
    const user = userEvent.setup()
    renderWithProviders(<ManagePage />)

    await user.click(screen.getByRole('tab', { name: 'Books' }))
    await user.click(await screen.findByRole('button', { name: `Refresh listing ${bookListItems[0].primaryTitle}` }))
    await waitFor(() => expect(api.refreshPublishedBook).toHaveBeenCalledWith(publishedBook.id))

    await user.click(screen.getByRole('button', { name: `Unlist ${bookListItems[0].primaryTitle}` }))
    await waitFor(() => expect(api.unlistPublishedBook).toHaveBeenCalledWith(publishedBook.id))

    await user.click(screen.getByRole('button', { name: `List ${bookListItems[1].primaryTitle}` }))
    await waitFor(() => expect(api.publishBook).toHaveBeenCalledWith(bookListItems[1].id))
  })

  it('keeps listed books first, sorts the rest, and blocks incomplete listings', async () => {
    const incomplete = {
      ...bookListItems[1],
      id: 'incomplete-book',
      primaryTitle: 'Aardvark incomplete',
      description: null,
      author: null,
      genres: [],
      genresCount: 0,
      tags: [],
      tagsCount: 0,
      cover: null,
    }
    vi.mocked(api.getBooks).mockResolvedValue(paginated([incomplete, ...managedBooks]))
    renderWithProviders(<ManagePage />)

    await userEvent.click(screen.getByRole('tab', { name: 'Books' }))
    const rows = await screen.findAllByText(/Listed|Private/)
    expect(rows[0]).toHaveTextContent('Listed')
    expect(screen.getByRole('button', { name: 'List Aardvark incomplete' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'List Aardvark incomplete' })).toHaveAttribute(
      'title',
      expect.stringContaining('description'),
    )
  })

  it('allows listing when a legacy cover has a stored image despite a stale NotFound status', async () => {
    const legacyCoverBook = {
      ...managedBooks[1],
      id: 'legacy-cover-book',
      primaryTitle: 'Legacy cover book',
      cover: {
        status: 'NotFound' as const,
        source: 'ManualUpload',
        imageUrl: '/api/book/legacy/cover/file',
        thumbnailImageUrl: '/api/book/legacy/cover/thumbnail',
      },
    }
    vi.mocked(api.getBooks).mockResolvedValue(paginated([legacyCoverBook]))
    const user = userEvent.setup()
    renderWithProviders(<ManagePage />)

    await user.click(screen.getByRole('tab', { name: 'Books' }))
    const listButton = await screen.findByRole('button', { name: 'List Legacy cover book' })
    expect(listButton).toBeEnabled()
    await user.click(listButton)
    await waitFor(() => expect(api.publishBook).toHaveBeenCalledWith(legacyCoverBook.id))
  })

  it('fetches further library books on scroll and all listing pages automatically', async () => {
    const laterBook = { ...managedBooks[1], id: 'later-book', primaryTitle: 'Later book' }
    const firstLibraryPage = Array.from({ length: 50 }, (_, index) => ({
      ...managedBooks[index % managedBooks.length],
      id: `book-${index}`,
      primaryTitle: `Book ${index.toString().padStart(2, '0')}`,
    }))
    vi.mocked(api.getBooks).mockImplementation(async ({ skip = 0 }) => skip === 0
      ? { skip: 0, take: 50, total: 51, data: firstLibraryPage }
      : { skip: 50, take: 50, total: 51, data: [laterBook] })
    const firstListingPage = Array.from({ length: 50 }, (_, index) => ({
      ...publishedBook,
      id: `snapshot-${index}`,
      sourceBookId: `source-${index}`,
    }))
    vi.mocked(api.searchPublicBooks).mockImplementation(async ({ skip = 0 }) => skip === 0
      ? { skip: 0, take: 50, total: 51, data: firstListingPage }
      : { skip: 50, take: 50, total: 51, data: [publishedBook] })
    const user = userEvent.setup()
    renderWithProviders(<ManagePage />)

    await user.click(screen.getByRole('tab', { name: 'Books' }))
    const list = await screen.findByLabelText('Books')
    Object.defineProperties(list, {
      clientHeight: { configurable: true, value: 300 },
      scrollHeight: { configurable: true, value: 900 },
      scrollTop: { configurable: true, value: 560 },
    })
    fireEvent.scroll(list)

    await waitFor(() => expect(api.getBooks).toHaveBeenCalledWith(expect.objectContaining({ skip: 50, take: 50 })))
    expect(await screen.findByText('Later book')).toBeInTheDocument()
    await waitFor(() => expect(api.searchPublicBooks).toHaveBeenCalledWith({ skip: 50, take: 50, mineOnly: true }))
  })
})
