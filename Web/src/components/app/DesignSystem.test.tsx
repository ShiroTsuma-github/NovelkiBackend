import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import {
  Badge,
  buttonVariants,
  controlClass,
  DialogPanel,
  PageHeader,
  Surface,
} from './DesignSystem'

describe('quiet structure design system', () => {
  it('provides semantic variants instead of page-specific color strings', () => {
    expect(buttonVariants.primary).toContain('ui-button--primary')
    expect(buttonVariants.secondary).toContain('ui-button--secondary')
    expect(buttonVariants.destructive).toContain('ui-button--destructive')
    expect(controlClass).toBe('ui-control')
  })

  it('renders a consistent page hierarchy and optional actions', () => {
    render(
      <PageHeader
        actions={<button type="button">Add book</button>}
        description="Search and navigate the library."
        eyebrow="Library workspace"
        title="Books"
      />,
    )

    expect(screen.getByRole('heading', { name: 'Books', level: 1 })).toHaveClass('ui-display-title')
    expect(screen.getByText('Library workspace')).toHaveClass('ui-eyebrow')
    expect(screen.getByRole('button', { name: 'Add book' })).toBeInTheDocument()
  })

  it('uses shared sharp surfaces, badges, and dialog panels', () => {
    render(
      <>
        <Surface>Data surface</Surface>
        <Badge tone="accent">Reading</Badge>
        <DialogPanel>Dialog content</DialogPanel>
      </>,
    )

    expect(screen.getByText('Data surface')).toHaveClass('ui-surface')
    expect(screen.getByText('Reading')).toHaveClass('ui-badge', 'ui-badge--accent')
    expect(screen.getByText('Dialog content')).toHaveClass('ui-dialog-panel')
  })

  it('provides semantic feedback surfaces without page-specific palette classes', () => {
    render(
      <>
        <Surface tone="danger">Danger state</Surface>
        <Surface tone="success">Success state</Surface>
        <Surface tone="warning">Warning state</Surface>
      </>,
    )

    expect(screen.getByText('Danger state')).toHaveClass('ui-surface--danger')
    expect(screen.getByText('Success state')).toHaveClass('ui-surface--success')
    expect(screen.getByText('Warning state')).toHaveClass('ui-surface--warning')
  })
})
