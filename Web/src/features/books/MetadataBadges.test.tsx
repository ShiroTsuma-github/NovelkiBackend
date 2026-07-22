import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { DescribedMetadataPills, MetadataSummary } from './MetadataBadges'

describe('MetadataSummary', () => {
  it('shows every alternative author name on a separate tooltip line', async () => {
    const user = userEvent.setup()
    render(
      <MetadataSummary
        alternatives={['First Alias', 'Second Alias']}
        countNoun="alternative name"
        primary="Primary Author"
      />,
    )

    await user.hover(screen.getByLabelText('2 alternative names'))
    expect(screen.getByRole('tooltip')).toHaveTextContent('Alternative names: First Alias Second Alias')
    expect(screen.getByRole('tooltip')).toHaveClass('metadata-tooltip', 'ui-chart-tooltip')
  })

  it('shows alternative titles and the remaining count on separate tooltip lines', async () => {
    const user = userEvent.setup()
    render(
      <MetadataSummary
        alternatives={['First Title', 'Second Title']}
        countNoun="alternative title"
        primary="Primary Title"
        totalCount={3}
      />,
    )

    await user.hover(screen.getByLabelText('3 alternative titles'))
    expect(screen.getByRole('tooltip')).toHaveTextContent('Alternative titles: First Title Second Title +1 more')
  })
})

describe('DescribedMetadataPills', () => {
  it('lists every hidden metadata value in the count tooltip when the API returns them', async () => {
    const user = userEvent.setup()
    render(
      <DescribedMetadataPills
        maxVisible={3}
        totalCount={6}
        values={['one', 'two', 'three', 'four', 'five', 'six']}
      />,
    )

    await user.hover(screen.getByLabelText('3 more: four, five, six'))
    expect(screen.getByRole('tooltip')).toHaveTextContent('four five six')
  })

  it('uses the API total to count metadata values omitted from the list projection', async () => {
    const user = userEvent.setup()
    render(
      <DescribedMetadataPills
        totalCount={26}
        values={['visible-one', 'visible-two', 'visible-three', 'known-hidden']}
      />,
    )

    expect(screen.getByLabelText('23 more: known-hidden')).toHaveTextContent('+23 more')
    await user.hover(screen.getByLabelText('23 more: known-hidden'))
    expect(screen.getByRole('tooltip')).toHaveTextContent('known-hidden +22 more')
  })

  it('uses the same tooltip surface for a described genre and the remaining count', async () => {
    const user = userEvent.setup()
    render(
      <DescribedMetadataPills
        descriptions={{ Fantasy: 'Magic and strange worlds.' }}
        maxVisible={1}
        values={['Fantasy', 'Mystery']}
      />,
    )

    await user.hover(screen.getByText('Fantasy'))
    expect(screen.getByRole('tooltip')).toHaveClass('metadata-tooltip', 'ui-chart-tooltip')
    await user.unhover(screen.getByText('Fantasy'))
    await user.hover(screen.getByLabelText('1 more: Mystery'))
    expect(screen.getByRole('tooltip')).toHaveClass('metadata-tooltip', 'ui-chart-tooltip')
  })
})
