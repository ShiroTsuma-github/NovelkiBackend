import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { AppShell } from './AppShell'

vi.mock('@/features/auth/AuthProvider', () => ({
  useAuth: () => ({
    isAdmin: false,
    logout: vi.fn(),
  }),
}))

describe('AppShell', () => {
  it('keeps only the matching top-level nav item active on the add-book route', () => {
    renderAt('/books/new')

    expect(screen.getByRole('link', { name: /books/i })).not.toHaveAttribute('aria-current')
    expect(screen.getByRole('link', { name: /add/i })).toHaveAttribute('aria-current', 'page')
  })

  it('marks only books as active on the books list route', () => {
    renderAt('/books')

    expect(screen.getByRole('link', { name: /books/i })).toHaveAttribute('aria-current', 'page')
    expect(screen.getByRole('link', { name: /add/i })).not.toHaveAttribute('aria-current')
  })
})

function renderAt(route: string) {
  return render(
    <MemoryRouter initialEntries={[route]}>
      <Routes>
        <Route element={<AppShell />}>
          <Route element={<div>Page</div>} path="/books" />
          <Route element={<div>Page</div>} path="/books/new" />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}
