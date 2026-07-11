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
  it('shows local field errors and password checklist before API submit', async () => {
    const user = userEvent.setup()
    renderWithProviders(<RegisterPage />, { route: '/register' })

    await user.type(screen.getByPlaceholderText('Username'), 'ab')
    await user.type(screen.getByPlaceholderText('Email'), 'not-an-email')
    await user.type(screen.getByPlaceholderText('Password'), 'weak')
    await user.click(screen.getByRole('button', { name: /register/i }))

    expect(screen.getByText('Username must be at least 3 characters long.')).toBeInTheDocument()
    expect(screen.getByText('A valid email address is required.')).toBeInTheDocument()
    expect(screen.getByRole('listitem', { name: 'Missing: At least 8 characters' })).toBeInTheDocument()
    expect(screen.getByRole('listitem', { name: 'Missing: One uppercase letter' })).toBeInTheDocument()
    expect(screen.getByRole('listitem', { name: 'Missing: One number' })).toBeInTheDocument()
    expect(api.register).not.toHaveBeenCalled()
  })

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

  it('maps backend field errors to registration fields', async () => {
    vi.mocked(api.register).mockRejectedValue(new HttpError({
      type: 'Validation',
      title: 'Validation failed',
      status: 400,
      detail: 'One or more validation errors occurred.',
      instance: '/account/register',
      errors: {
        Email: ['Email is invalid.'],
        Password: ['Password must contain at least one uppercase letter.'],
      },
    }))
    const user = userEvent.setup()
    renderWithProviders(<RegisterPage />, { route: '/register' })

    await user.type(screen.getByPlaceholderText('Username'), 'reader')
    await user.type(screen.getByPlaceholderText('Email'), 'reader@example.com')
    await user.type(screen.getByPlaceholderText('Password'), 'Password123!')
    await user.click(screen.getByRole('button', { name: /register/i }))

    expect(await screen.findByText('Email is invalid.')).toBeInTheDocument()
    expect(screen.getByText('Password must contain at least one uppercase letter.')).toBeInTheDocument()
    expect(toast.error).toHaveBeenCalledWith('One or more validation errors occurred.')
  })

  it('maps username conflict to the username field', async () => {
    vi.mocked(api.register).mockRejectedValue(new HttpError({
      type: 'UsernameTakenException',
      title: 'Conflict',
      status: 409,
      detail: "Account with username 'reader' already exists.",
      instance: '/account/register',
      errors: {
        Username: ["Account with username 'reader' already exists."],
      },
    }))
    const user = userEvent.setup()
    renderWithProviders(<RegisterPage />, { route: '/register' })

    await user.type(screen.getByPlaceholderText('Username'), 'reader')
    await user.type(screen.getByPlaceholderText('Email'), 'reader@example.com')
    await user.type(screen.getByPlaceholderText('Password'), 'Password123!')
    await user.click(screen.getByRole('button', { name: /register/i }))

    expect(await screen.findByText("Account with username 'reader' already exists.")).toBeInTheDocument()
  })
})
