import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { DescribedMetadataPills, MetadataSummary } from './MetadataBadges'

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

describe('DescribedMetadataPills', () => {
  it('uses the API total to count metadata values omitted from the list projection', () => {
    render(
      <DescribedMetadataPills
        totalCount={26}
        values={['visible-one', 'visible-two', 'visible-three', 'known-hidden']}
      />,
    )

    expect(screen.getByLabelText('23 more: known-hidden')).toHaveTextContent('+23 more')
    expect(screen.getByLabelText('23 more: known-hidden')).toHaveAttribute(
      'title',
      'known-hidden\n+22 more',
    )
  })
})
