import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { BookStatusPill } from './BookStatusPill'

describe('BookStatusPill', () => {
  it.each([
    ['Reading', 'reading'],
    ['Completed', 'completed'],
    ['Plan to Read', 'planned'],
    ['On Hold', 'paused'],
    ['Dropped', 'dropped'],
  ])('uses the semantic %s tone', (status, tone) => {
    render(<BookStatusPill status={status} />)

    expect(screen.getByLabelText(`Book status: ${status}`)).toHaveClass(
      `book-details-status--${tone}`,
    )
  })

  it('keeps on-hold distinct from the completed treatment', () => {
    render(
      <>
        <BookStatusPill status="On Hold" />
        <BookStatusPill status="Completed" />
      </>,
    )

    expect(screen.getByLabelText('Book status: On Hold')).toHaveClass(
      'book-details-status--paused',
    )
    expect(screen.getByLabelText('Book status: On Hold')).not.toHaveClass(
      'book-details-status--completed',
    )
  })
})
