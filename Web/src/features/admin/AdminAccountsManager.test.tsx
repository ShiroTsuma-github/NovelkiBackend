import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '@/api/client'
import { renderWithProviders } from '@/test/render'
import { AdminAccountsManager } from './AdminAccountsManager'

vi.mock('@/api/client', () => ({ api: { getAdminUsers: vi.fn(), deleteAdminUser: vi.fn() } }))
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }))

const user = {
  id: '22222222-2222-2222-2222-222222222222',
  username: 'reader',
  email: 'reader@example.com',
  createdAt: '2026-01-01T00:00:00Z',
  booksCount: 4,
  tagsCount: 2,
  authorsCreatedCount: 1,
}

describe('AdminAccountsManager', () => {
  beforeEach(() => {
    vi.mocked(api.getAdminUsers).mockReset().mockResolvedValue({ skip: 0, take: 100, total: 1, data: [user] })
    vi.mocked(api.deleteAdminUser).mockReset().mockResolvedValue({
      userId: user.id, deletedBooks: 4, deletedTags: 2, deletedAuthors: 1,
    })
  })

  it('searches and permanently deletes an account through a confirmation dialog', async () => {
    const actor = userEvent.setup()
    renderWithProviders(<AdminAccountsManager />)

    await actor.type(screen.getByPlaceholderText('Search by username or email…'), 'reader@example.com')
    await waitFor(() => expect(api.getAdminUsers).toHaveBeenLastCalledWith({ skip: 0, take: 100, search: 'reader@example.com' }))
    await actor.click(await screen.findByRole('button', { name: 'Delete account reader' }))
    expect(screen.getByText(/permanently removes the account/i)).toBeInTheDocument()
    await actor.click(screen.getByRole('button', { name: 'Delete account permanently' }))

    await waitFor(() => expect(api.deleteAdminUser).toHaveBeenCalledWith(user.id))
  })
})
