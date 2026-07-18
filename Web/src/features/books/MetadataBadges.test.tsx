import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { MetadataSummary } from './MetadataBadges'

describe('MetadataSummary', () => {
  it('shows every alternative author name on a separate tooltip line', () => {
    render(
      <MetadataSummary
        alternatives={['First Alias', 'Second Alias']}
        countNoun="alternative name"
        primary="Primary Author"
      />,
    )

    expect(screen.getByLabelText('2 alternative names')).toHaveAttribute(
      'title',
      'Alternative names:\nFirst Alias\nSecond Alias',
    )
  })

  it('shows alternative titles and the remaining count on separate tooltip lines', () => {
    render(
      <MetadataSummary
        alternatives={['First Title', 'Second Title']}
        countNoun="alternative title"
        primary="Primary Title"
        totalCount={3}
      />,
    )

    expect(screen.getByLabelText('3 alternative titles')).toHaveAttribute(
      'title',
      'Alternative titles:\nFirst Title\nSecond Title\n+1 more',
    )
  })
})
