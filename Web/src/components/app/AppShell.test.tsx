import { QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { createTestQueryClient } from '@/test/render'
import { nsfwPreferenceStorageKey } from '@/lib/nsfwPreference'
import { AppShell } from './AppShell'

vi.mock('@/features/auth/AuthProvider', () => ({
  useAuth: () => ({
    isAdmin: false,
    logout: vi.fn(),
  }),
}))

describe('AppShell', () => {
  it('exposes stable shell landmarks for page refactors', () => {
    renderAt('/books')

    expect(screen.getByRole('banner')).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: /primary/i })).toBeInTheDocument()
    expect(screen.getByRole('main')).toBeInTheDocument()
    expect(screen.getByText('Novelki')).toBeInTheDocument()
    expect(screen.getByText('Personal library system')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /skip to content/i })).toHaveAttribute('href', '#main-content')
    expect(screen.getByRole('button', { name: /enable nsfw content/i })).toHaveAttribute('aria-pressed', 'false')
    expect(screen.getByRole('button', { name: /log out/i })).toBeInTheDocument()
  })

  it('stores the NSFW preference and refreshes cached requests', async () => {
    const user = userEvent.setup()
    const { queryClient } = renderAt('/books')
    const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries')

    await user.click(screen.getByRole('button', { name: /enable nsfw content/i }))

    expect(window.localStorage.getItem(nsfwPreferenceStorageKey)).toBe('true')
    expect(screen.getByRole('button', { name: /disable nsfw content/i })).toHaveAttribute('aria-pressed', 'true')
    expect(invalidateQueries).toHaveBeenCalled()
  })

  it('keeps only the matching top-level nav item active on the add-book route', () => {
    renderAt('/books/new')

    expect(screen.getByRole('link', { name: /books/i })).not.toHaveAttribute('aria-current')
    expect(screen.getByRole('link', { name: /add/i })).toHaveAttribute('aria-current', 'page')
  })

  it('marks only books as active on the books list route', () => {
    renderAt('/books')

    expect(screen.getByRole('link', { name: /books/i })).toHaveAttribute('aria-current', 'page')
    expect(screen.getByRole('link', { name: /analytics/i })).not.toHaveAttribute('aria-current')
    expect(screen.getByRole('link', { name: /add/i })).not.toHaveAttribute('aria-current')
  })

  it('marks analytics as active on the analytics route', () => {
    renderAt('/analytics')

    expect(screen.getByRole('link', { name: /analytics/i })).toHaveAttribute('aria-current', 'page')
    expect(screen.getByRole('link', { name: /books/i })).not.toHaveAttribute('aria-current')
  })

  it('exposes and activates the manage workspace', () => {
    renderAt('/manage')

    expect(screen.getByRole('link', { name: /manage/i })).toHaveAttribute('aria-current', 'page')
    expect(screen.getByRole('link', { name: /books/i })).not.toHaveAttribute('aria-current')
  })
})

function renderAt(route: string) {
  const queryClient = createTestQueryClient()
  return {
    queryClient,
    ...render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={[route]}>
          <Routes>
            <Route element={<AppShell />}>
              <Route element={<div>Page</div>} path="/books" />
              <Route element={<div>Page</div>} path="/analytics" />
              <Route element={<div>Page</div>} path="/books/new" />
              <Route element={<div>Page</div>} path="/manage" />
            </Route>
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    ),
  }
}
