import { fireEvent, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { api } from '@/api/client'
import { renderWithProviders } from '@/test/render'
import { books } from '@/test/fixtures'
import { ProgressDialog } from './ProgressDialog'

vi.mock('@/api/client', () => ({
  api: {
    updateProgress: vi.fn(),
  },
}))

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}))

describe('ProgressDialog', () => {
  it.each(['e', '-1', '1e9'])('rejects invalid chapter value %s before submit', async (value) => {
    const user = userEvent.setup()
    renderWithProviders(<ProgressDialog book={books[0]} />)

    await user.click(screen.getByRole('button', { name: /progress/i }))
    const chapterInput = screen.getByPlaceholderText('Chapter number')
    await user.clear(chapterInput)
    await user.type(chapterInput, value)

    expect(chapterInput).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByText('Chapter number must be a non-negative number without exponent notation.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
    expect(api.updateProgress).not.toHaveBeenCalled()
  })

  it('shows a field error when chapter is greater than total chapters', async () => {
    const user = userEvent.setup()
    renderWithProviders(<ProgressDialog book={books[0]} />)

    await user.click(screen.getByRole('button', { name: /progress/i }))
    const chapterInput = screen.getByPlaceholderText('Chapter number')
    await user.clear(chapterInput)
    await user.type(chapterInput, '2000')

    expect(screen.getByText('Current chapter cannot be greater than total chapters.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
  })

  it('limits label and comment length in the browser', async () => {
    const user = userEvent.setup()
    renderWithProviders(<ProgressDialog book={books[0]} />)

    await user.click(screen.getByRole('button', { name: /progress/i }))
    const labelInput = screen.getByPlaceholderText('Chapter label')
    const commentInput = screen.getByPlaceholderText('Comment')

    expect(labelInput).toHaveAttribute('maxlength', '100')
    expect(commentInput).toHaveAttribute('maxlength', '1000')

    fireEvent.change(labelInput, { target: { value: 'x'.repeat(100) } })
    fireEvent.change(commentInput, { target: { value: 'x'.repeat(1000) } })

    expect(labelInput).toHaveValue('x'.repeat(100))
    expect(commentInput).toHaveValue('x'.repeat(1000))
  })

  it('submits a comment without changing chapter', async () => {
    vi.mocked(api.updateProgress).mockResolvedValue(undefined)
    const user = userEvent.setup()
    renderWithProviders(<ProgressDialog book={books[0]} />)

    await user.click(screen.getByRole('button', { name: /progress/i }))
    await user.type(screen.getByPlaceholderText('Comment'), 'same chapter note')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.updateProgress).toHaveBeenCalled())
    expect(vi.mocked(api.updateProgress).mock.calls[0]).toEqual([books[0].id, {
      currentChapterNumber: books[0].currentChapterNumber,
      currentChapterLabel: books[0].currentChapterLabel,
      comment: 'same chapter note',
    }])
  })
})
