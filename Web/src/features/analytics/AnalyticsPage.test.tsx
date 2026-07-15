import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '@/api/client'
import type { BookAnalyticsDto } from '@/api/types'
import { expectReadableTextContrast } from '@/test/contrast'
import { renderWithProviders } from '@/test/render'
import { AnalyticsPage } from './AnalyticsPage'

vi.mock('@/api/client', () => ({
  api: {
    getBookAnalytics: vi.fn(),
  },
}))

describe('AnalyticsPage', () => {
  beforeEach(() => {
    vi.mocked(api.getBookAnalytics).mockReset()
  })

  it('fetches analytics from restored URL filters', async () => {
    vi.mocked(api.getBookAnalytics).mockResolvedValue(createAnalytics())

    renderWithProviders(<AnalyticsPage />, {
      route: '/analytics?query=author%3AToika%20rating%3A%3E%3D8&from=2026-01-01&to=2026-02-01&bucket=month',
    })

    expect(await screen.findByText('Status by type')).toBeInTheDocument()
    expect(api.getBookAnalytics).toHaveBeenCalledWith({
      query: 'author:Toika rating:>=8',
      from: '2026-01-01',
      to: '2026-02-01',
      bucket: 'month',
    })
    expect(screen.getByDisplayValue('author:Toika rating:>=8')).toBeInTheDocument()
    expect(screen.getByDisplayValue('2026-01-01')).toBeInTheDocument()
    expect(screen.getByDisplayValue('2026-02-01')).toBeInTheDocument()
  })

  it('extracts date filters from query into analytics range request', async () => {
    vi.mocked(api.getBookAnalytics).mockResolvedValue(createAnalytics())

    renderWithProviders(<AnalyticsPage />, {
      route: '/analytics?query=author%3AToika%20updated%3A%3D2026-07',
    })

    expect(await screen.findByText('Status by type')).toBeInTheDocument()
    expect(api.getBookAnalytics).toHaveBeenCalledWith(expect.objectContaining({
      query: 'author:Toika',
      from: '2026-07-01',
      to: '2026-08-01',
    }))
    expect(screen.getByDisplayValue('author:Toika')).toBeInTheDocument()
    expect(screen.getByDisplayValue('2026-07-01')).toBeInTheDocument()
    expect(screen.getByDisplayValue('2026-08-01')).toBeInTheDocument()
  })

  it('updates URL-backed filters and creates a new request', async () => {
    vi.mocked(api.getBookAnalytics).mockResolvedValue(createAnalytics())
    const user = userEvent.setup()

    renderWithProviders(<AnalyticsPage />, { route: '/analytics?from=2026-01-01&to=2026-02-01&bucket=week' })

    await screen.findByText('10')
    await user.type(screen.getByLabelText(/query/i), 'status:"Plan to Read"')
    await user.selectOptions(screen.getByLabelText(/bucket/i), 'day')
    await user.click(screen.getByRole('button', { name: /apply filters/i }))

    await waitFor(() => expect(api.getBookAnalytics).toHaveBeenLastCalledWith({
      query: 'status:"Plan to Read"',
      from: '2026-01-01',
      to: '2026-02-01',
      bucket: 'day',
    }))
  })

  it('shows different empty states for empty library and no matches', async () => {
    vi.mocked(api.getBookAnalytics).mockResolvedValueOnce(createAnalytics({ totalBooks: 0 }))

    const { unmount } = renderWithProviders(<AnalyticsPage />, { route: '/analytics?from=2026-01-01&to=2026-02-01' })

    expect(await screen.findByText(/your library is empty/i)).toBeInTheDocument()
    unmount()

    vi.mocked(api.getBookAnalytics).mockResolvedValueOnce(createAnalytics({ totalBooks: 0 }))
    renderWithProviders(<AnalyticsPage />, { route: '/analytics?query=missing&from=2026-01-01&to=2026-02-01' })

    expect(await screen.findByText(/no books match the current analytics filters/i)).toBeInTheDocument()
  })

  it('preserves previous data during refetch', async () => {
    let resolveSecondRequest: (value: BookAnalyticsDto) => void = () => {}
    vi.mocked(api.getBookAnalytics)
      .mockResolvedValueOnce(createAnalytics({ totalBooks: 10 }))
      .mockImplementationOnce(() => new Promise<BookAnalyticsDto>((resolve) => {
        resolveSecondRequest = resolve
      }))
    const user = userEvent.setup()

    renderWithProviders(<AnalyticsPage />, { route: '/analytics?from=2026-01-01&to=2026-02-01&bucket=week' })

    expect(await screen.findByText('10')).toBeInTheDocument()
    await user.selectOptions(screen.getByLabelText(/bucket/i), 'month')
    await user.click(screen.getByRole('button', { name: /apply filters/i }))

    expect(screen.getByText('10')).toBeInTheDocument()
    expect(screen.getByText(/refreshing/i)).toBeInTheDocument()

    resolveSecondRequest(createAnalytics({ totalBooks: 12 }))
    expect(await screen.findByText('12')).toBeInTheDocument()
  })

  it('shows card data tables without forcing horizontal page overflow', async () => {
    vi.mocked(api.getBookAnalytics).mockResolvedValue(createAnalytics())
    const user = userEvent.setup()

    renderWithProviders(<AnalyticsPage />, { route: '/analytics?from=2026-01-01&to=2026-02-01' })

    await screen.findByText('Status by type')
    await user.click(screen.getAllByRole('button', { name: /view data/i })[0])

    expect(screen.getByRole('columnheader', { name: /type/i })).toBeInTheDocument()
    expect(document.querySelector('.overflow-x-hidden')).toBeTruthy()
  })

  it('keeps analytics type labels readable against their calculated background', async () => {
    vi.mocked(api.getBookAnalytics).mockResolvedValue(createAnalytics())

    renderWithProviders(<AnalyticsPage />, { route: '/analytics?from=2026-01-01&to=2026-02-01' })

    expectReadableTextContrast(await screen.findByText('Novel'))
  })
})

function createAnalytics(overrides: Partial<BookAnalyticsDto['overview']> = {}): BookAnalyticsDto {
  const overview = {
    totalBooks: 10,
    ratedBooks: 8,
    unratedBooks: 2,
    averageRating: 8.4,
    currentChapters: 468,
    booksWithKnownCurrentChapter: 9,
    booksWithoutKnownCurrentChapter: 1,
    ...overrides,
  }

  return {
    generatedAt: '2026-07-15T12:00:00Z',
    scope: {
      query: null,
      from: '2026-01-01',
      to: '2026-02-01',
      bucket: 'week',
    },
    overview,
    composition: {
      statusByType: overview.totalBooks > 0 ? [{
        type: 'Novel',
        totalBooks: 6,
        statuses: [
          { status: 'Reading', bookCount: 4 },
          { status: 'Completed', bookCount: 2 },
        ],
      }] : [],
      genres: [],
      tags: [],
    },
    ratings: {
      ratedBooks: overview.ratedBooks,
      unratedBooks: overview.unratedBooks,
      averageRating: overview.averageRating,
      counts: [],
    },
    planning: {
      prioritiesByStatus: [],
    },
    progress: {
      typeVolumes: [],
    },
    activity: {
      points: overview.totalBooks > 0 ? [{
        date: '2026-01-05',
        progressEvents: 3,
        booksTouched: 2,
        chaptersAdvanced: 18,
      }] : [],
    },
    libraryGrowth: {
      openingCount: 0,
      points: [],
    },
    quality: {
      fieldCompleteness: [],
      linkSources: [],
      coverStatuses: [],
      coverSources: [],
    },
  }
}
