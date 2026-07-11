import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { toast } from 'sonner'
import { api } from '@/api/client'
import { HttpError } from '@/api/http'
import { renderWithProviders } from '@/test/render'
import { RegisterPage } from './RegisterPage'

vi.mock('@/api/client', () => ({
  api: {
    register: vi.fn(),
  },
}))

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}))

describe('RegisterPage', () => {
  it('submits registration fields to the API', async () => {
    vi.mocked(api.register).mockResolvedValue({
      id: 'user-id',
      name: 'reader',
      createdAt: '2026-01-01T00:00:00Z',
    })
    const user = userEvent.setup()
    renderWithProviders(<RegisterPage />, { route: '/register' })

    await user.type(screen.getByPlaceholderText('Username'), 'reader')
    await user.type(screen.getByPlaceholderText('Email'), 'reader@example.com')
    await user.type(screen.getByPlaceholderText('Password'), 'Password123!')
    await user.click(screen.getByRole('button', { name: /register/i }))

    await waitFor(() => expect(api.register).toHaveBeenCalled())
    expect(vi.mocked(api.register).mock.calls[0]?.[0]).toEqual({
      username: 'reader',
      email: 'reader@example.com',
      password: 'Password123!',
    })
    expect(toast.success).toHaveBeenCalledWith('Account created. You can log in now.')
  })

  it('shows backend registration errors', async () => {
    vi.mocked(api.register).mockRejectedValue(new HttpError({
      type: 'Validation',
      title: 'Validation failed',
      status: 400,
      detail: 'Email is invalid.',
      instance: '/account/register',
    }))
    const user = userEvent.setup()
    renderWithProviders(<RegisterPage />, { route: '/register' })

    await user.click(screen.getByRole('button', { name: /register/i }))

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Email is invalid.'))
  })
})
