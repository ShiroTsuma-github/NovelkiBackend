import { screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '@/api/client'
import type { AdminBookListItemDto } from '@/api/types'
import { paginated } from '@/test/fixtures'
import { renderWithProviders } from '@/test/render'
import { AdminPage } from './AdminPage'

vi.mock('@/api/client', () => ({
  api: {
    getAdminBooks: vi.fn(),
    deleteAdminBooksByOwner: vi.fn(),
    createAdminStatus: vi.fn(),
    createAdminType: vi.fn(),
    createAdminGenre: vi.fn(),
  },
}))

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}))

describe('AdminPage', () => {
  beforeEach(() => {
    vi.mocked(api.getAdminBooks).mockReset()
  })

  it('filters admin books using the normalized query string', async () => {
    vi.mocked(api.getAdminBooks).mockResolvedValue(
      paginated([
        createAdminBook({
          id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
          primaryTitle: 'Lord of Mysteries',
        }),
      ]),
    )

    renderWithProviders(<AdminPage />, {
      route: '/admin?query=%20title%3ALord%20&sortBy=title&sortDirection=asc',
    })

    expect(await screen.findByText('Lord of Mysteries')).toBeInTheDocument()
    expect(screen.queryByText('Shadow Slave')).not.toBeInTheDocument()
    expect(api.getAdminBooks).toHaveBeenCalledWith({
      skip: 0,
      take: 20,
      query: 'title:Lord',
      sortBy: 'title',
      sortDirection: 'asc',
    })
  })

  it('shows an empty state when the admin search returns no matches', async () => {
    vi.mocked(api.getAdminBooks).mockResolvedValue({
      skip: 0,
      take: 20,
      total: 0,
      data: [],
    })

    renderWithProviders(<AdminPage />, {
      route: '/admin?query=title%3AMissing&sortBy=title&sortDirection=asc',
    })

    expect(await screen.findByText('No results.')).toBeInTheDocument()
    await waitFor(() => expect(api.getAdminBooks).toHaveBeenCalledWith({
      skip: 0,
      take: 20,
      query: 'title:Missing',
      sortBy: 'title',
      sortDirection: 'asc',
    }))
  })
})

function createAdminBook(overrides: Partial<AdminBookListItemDto> = {}): AdminBookListItemDto {
  return {
    id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    created: '2026-01-01T10:00:00Z',
    lastModified: '2026-02-01T10:00:00Z',
    primaryTitle: 'Shadow Slave',
    description: null,
    alternativeTitles: [],
    alternativeTitlesCount: 0,
    author: 'Guiltythree',
    contentType: 'Novel',
    status: 'Reading',
    currentChapterNumber: 100,
    currentChapterLabel: '100',
    totalChapters: 500,
    rating: 9,
    priority: 1,
    notes: null,
    cover: null,
    genres: ['Fantasy'],
    genresCount: 1,
    tags: ['favorite'],
    tagsCount: 1,
    linksCount: 0,
    ownerId: '11111111-1111-1111-1111-111111111111',
    ownerUsername: 'admin-user',
    ownerEmail: 'admin@example.com',
    ...overrides,
  }
}
