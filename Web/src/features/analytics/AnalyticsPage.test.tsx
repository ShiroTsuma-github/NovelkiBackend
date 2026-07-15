import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '@/api/client'
import type { BookAnalyticsDto } from '@/api/types'
import { readingTimeStorageKey } from '@/features/analytics/readingTimeSettings'
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
    await user.click(screen.getByRole('button', { name: /view data for status by type/i }))

    expect(screen.getByRole('columnheader', { name: 'Type' })).toBeInTheDocument()
    expect(document.querySelector('.overflow-x-hidden')).toBeTruthy()
  })

  it('rounds status percentages before rendering table values', async () => {
    vi.mocked(api.getBookAnalytics).mockResolvedValue(createAnalytics({
      composition: {
        statusByType: [{
          type: 'Novel',
          totalBooks: 10,
          statuses: [{ status: 'Reading', bookCount: 10 }],
        }],
      },
    }))
    const user = userEvent.setup()

    renderWithProviders(<AnalyticsPage />, { route: '/analytics?from=2026-01-01&to=2026-02-01' })

    await user.click(await screen.findByRole('button', { name: /view data for status by type/i }))

    expect(screen.getByRole('cell', { name: '100%' })).toBeInTheDocument()
    expect(screen.queryByText(/100\.00000000000001%/)).not.toBeInTheDocument()
  })

  it('exposes text-equivalent data tables where raw data adds value', async () => {
    vi.mocked(api.getBookAnalytics).mockResolvedValue(createAnalytics())
    const user = userEvent.setup()

    renderWithProviders(<AnalyticsPage />, { route: '/analytics?from=2026-01-01&to=2026-02-01' })

    const buttons = await screen.findAllByRole('button', { name: /view data for/i })
    expect(buttons).toHaveLength(10)
    expect(screen.queryByRole('button', { name: /view data for rating distribution/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /view data for priority by status/i })).not.toBeInTheDocument()

    for (const button of buttons) {
      await user.click(button)
      expect(button).toHaveAttribute('aria-expanded', 'true')
    }

    expect(screen.getByText('Status by type data table')).toBeInTheDocument()
    expect(screen.getByText('Cover availability data table')).toBeInTheDocument()
  })

  it('toggles card data with keyboard and keeps screen-reader names specific', async () => {
    vi.mocked(api.getBookAnalytics).mockResolvedValue(createAnalytics())
    const user = userEvent.setup()

    renderWithProviders(<AnalyticsPage />, { route: '/analytics?from=2026-01-01&to=2026-02-01' })

    const button = await screen.findByRole('button', { name: /view data for reading activity/i })
    button.focus()
    await user.keyboard('{Enter}')

    expect(button).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getByText('Reading activity data table')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /hide data for reading activity/i })).toBeInTheDocument()
  })

  it('renders composition charts with Other buckets and drill-down links', async () => {
    vi.mocked(api.getBookAnalytics).mockResolvedValue(createAnalytics({
      composition: {
        genres: [
          { name: 'Fantasy', bookCount: 8, shareOfBooks: 80 },
          { name: 'Drama "Special" / 2026', bookCount: 7, shareOfBooks: 70 },
          { name: 'Slice of Life', bookCount: 6, shareOfBooks: 60 },
          { name: 'Mystery', bookCount: 4, shareOfBooks: 40 },
          { name: 'Action', bookCount: 3, shareOfBooks: 30 },
          { name: 'Drama', bookCount: 2, shareOfBooks: 20 },
          { name: 'Very Long Genre Name That Must Stay Readable', bookCount: 1, shareOfBooks: 10 },
        ],
        tags: [
          { name: 'favorite', bookCount: 5, shareOfBooks: 50 },
          { name: 'slow burn', bookCount: 3, shareOfBooks: 30 },
        ],
      },
    }))
    const user = userEvent.setup()

    renderWithProviders(<AnalyticsPage />, { route: '/analytics?from=2026-01-01&to=2026-02-01' })

    expect(await screen.findByRole('link', { name: 'Fantasy' })).toHaveAttribute('href', '/books?query=genre%3AFantasy')
    expect(await screen.findByRole('link', { name: 'Drama "Special" / 2026' })).toHaveAttribute('href', '/books?query=genre%3A%22Drama%20%5C%22Special%5C%22%20%2F%202026%22')
    expect(await screen.findByRole('link', { name: 'slow burn' })).toHaveAttribute('href', '/books?query=tag%3A%22slow%20burn%22')
    expect(await screen.findByText('Other')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /view data for top genres/i }))
    expect(screen.queryByRole('columnheader', { name: /bucket/i })).not.toBeInTheDocument()
  })

  it('renders empty composition card states without hiding other cards', async () => {
    vi.mocked(api.getBookAnalytics).mockResolvedValue(createAnalytics({
      composition: {
        statusByType: [{
          type: 'Novel',
          totalBooks: 1,
          statuses: [{ status: 'Reading', bookCount: 1 }],
        }],
        genres: [],
        tags: [],
      },
    }))

    renderWithProviders(<AnalyticsPage />, { route: '/analytics?from=2026-01-01&to=2026-02-01' })

    expect((await screen.findAllByText('Novel')).length).toBeGreaterThan(0)
    expect(screen.getByText('No genre data for this analytics scope.')).toBeInTheDocument()
    expect(screen.getByText('No tag data for this analytics scope.')).toBeInTheDocument()
  })

  it('renders rating distribution with a separate unrated drill-down', async () => {
    vi.mocked(api.getBookAnalytics).mockResolvedValue(createAnalytics({
      ratings: {
        ratedBooks: 1,
        unratedBooks: 9,
        averageRating: 10,
        counts: Array.from({ length: 10 }, (_unused, index) => ({
          rating: index + 1,
          bookCount: index === 9 ? 1 : 0,
        })),
      },
    }))
    renderWithProviders(<AnalyticsPage />, { route: '/analytics?from=2026-01-01&to=2026-02-01' })

    expect(await screen.findByText('10%')).toBeInTheDocument()
    expect(screen.getAllByText('Unrated: 9').length).toBeGreaterThan(1)
    expect(screen.getByRole('link', { name: /unrated: 9/i })).toHaveAttribute('href', '/books?query=rating%3Anone')
    expect(screen.queryByRole('link', { name: /^10: 1/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /view data for rating distribution/i })).not.toBeInTheDocument()
  })

  it('renders priority heatmap with unset drill-down', async () => {
    vi.mocked(api.getBookAnalytics).mockResolvedValue(createAnalytics({
      planning: {
        prioritiesByStatus: [{
          status: 'Plan to Read',
          totalBooks: 4,
          priorities: [
            { priority: '1', bookCount: 1 },
            { priority: 'Unset', bookCount: 3 },
          ],
        }],
      },
    }))

    renderWithProviders(<AnalyticsPage />, { route: '/analytics?from=2026-01-01&to=2026-02-01' })

    expect(await screen.findByTestId('priority-heatmap')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /view data for priority by status/i })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Plan to Read' })).toHaveAttribute('href', '/books?query=status%3A%22Plan%20to%20Read%22')
    expect(screen.getByRole('link', { name: '3' })).toHaveAttribute('href', '/books?query=status%3A%22Plan%20to%20Read%22%20priority%3Anone')
  })

  it('shows rating and priority empty states when all analytics rows are zero', async () => {
    vi.mocked(api.getBookAnalytics).mockResolvedValue(createAnalytics({ totalBooks: 0 }))

    renderWithProviders(<AnalyticsPage />, { route: '/analytics?from=2026-01-01&to=2026-02-01' })

    expect(await screen.findByText('No rating data for this analytics scope.')).toBeInTheDocument()
    expect(screen.getByText('No priority data for this analytics scope.')).toBeInTheDocument()
  })

  it('renders chapter volume with separate count and chapter sections', async () => {
    vi.mocked(api.getBookAnalytics).mockResolvedValue(createAnalytics({
      progress: {
        typeVolumes: [
          { type: 'Novel', bookCount: 2, currentChapters: 120.5, averageCurrentChapter: 60.25, medianCurrentChapter: 55 },
          { type: 'Manga', bookCount: 1, currentChapters: 0, averageCurrentChapter: null, medianCurrentChapter: null },
        ],
      },
    }))

    renderWithProviders(<AnalyticsPage />, { route: '/analytics?from=2026-01-01&to=2026-02-01' })

    expect(await screen.findByText('Book count by type')).toBeInTheDocument()
    expect(screen.getByText('Current chapters by type')).toBeInTheDocument()
    expect(screen.getByText(/Current chapters: 120.5/)).toBeInTheDocument()
  })

  it('estimates reading time from shared localStorage without refetching', async () => {
    window.localStorage.setItem(readingTimeStorageKey, JSON.stringify({ Novel: 2 }))
    vi.mocked(api.getBookAnalytics).mockResolvedValue(createAnalytics({
      progress: {
        typeVolumes: [
          { type: 'Novel', bookCount: 1, currentChapters: 468, averageCurrentChapter: 468, medianCurrentChapter: 468 },
        ],
      },
    }))
    const user = userEvent.setup()

    renderWithProviders(<AnalyticsPage />, { route: '/analytics?from=2026-01-01&to=2026-02-01' })

    expect(await screen.findByText('15.6 h')).toBeInTheDocument()
    const requestCount = vi.mocked(api.getBookAnalytics).mock.calls.length
    const input = screen.getByRole('spinbutton', { name: /novel minutes per chapter/i })

    await user.clear(input)
    await user.type(input, '0')

    expect(await screen.findByText('0.0 h')).toBeInTheDocument()
    expect(window.localStorage.getItem(readingTimeStorageKey)).toContain('"Novel":0')
    expect(vi.mocked(api.getBookAnalytics).mock.calls.length).toBe(requestCount)
  })

  it('shows chapter volume and estimated time empty states when no chapter rows exist', async () => {
    vi.mocked(api.getBookAnalytics).mockResolvedValue(createAnalytics({
      progress: {
        typeVolumes: [],
      },
    }))

    renderWithProviders(<AnalyticsPage />, { route: '/analytics?from=2026-01-01&to=2026-02-01' })

    expect(await screen.findByText('No chapter volume data for this analytics scope.')).toBeInTheDocument()
    expect(screen.getByText('No chapter data to estimate reading time.')).toBeInTheDocument()
  })

  it('renders reading activity and library growth trends for empty buckets, one point, and import jumps', async () => {
    vi.mocked(api.getBookAnalytics).mockResolvedValue(createAnalytics({
      activity: {
        points: [
          { date: '2026-01-01', progressEvents: 0, booksTouched: 0, chaptersAdvanced: 0 },
          { date: '2026-01-08', progressEvents: 0, booksTouched: 0, chaptersAdvanced: 0 },
          {
            date: '2026-01-15',
            progressEvents: 1,
            booksTouched: 1,
            chaptersAdvanced: 9_999,
          },
        ],
      },
      libraryGrowth: {
        openingCount: 1,
        points: [
          { date: '2026-01-01', booksAdded: 0, cumulativeBooks: 1, byType: [] },
          { date: '2026-01-08', booksAdded: 0, cumulativeBooks: 1, byType: [] },
          { date: '2026-01-15', booksAdded: 1_000_000, cumulativeBooks: 1_000_001, byType: [{ type: 'Novel', bookCount: 1_000_000 }] },
        ],
      },
    }))
    const user = userEvent.setup()

    renderWithProviders(<AnalyticsPage />, { route: '/analytics?from=2026-01-01&to=2026-02-01&bucket=week' })

    expect(await screen.findByText(/Chapters advanced: 9,999/i)).toBeInTheDocument()
    expect(screen.getAllByText('January 1–14, 2026').length).toBeGreaterThan(1)
    expect(screen.getByText(/\+1,000,000 added/i)).toBeInTheDocument()
    expect(screen.getByText(/No additions by type/i)).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /January 15/i })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Novel: 1,000,000/i })).toHaveAttribute(
      'href',
      '/books?query=type%3ANovel%20created%3A%3E%3D2026-01-15%20created%3A%3C2026-01-22',
    )

    await user.click(screen.getByRole('button', { name: /view data for library growth/i }))
    expect(screen.getByRole('cell', { name: 'January 1–14, 2026' })).toBeInTheDocument()
    expect(screen.getByRole('cell', { name: 'No additions' })).toBeInTheDocument()
  })

  it('renders quality charts with zero, full, unknown, and long-label buckets', async () => {
    vi.mocked(api.getBookAnalytics).mockResolvedValue(createAnalytics({
      totalBooks: 10,
      quality: {
        fieldCompleteness: [
          { field: 'author', bookCount: 10, shareOfBooks: 100 },
          { field: 'usableCover', bookCount: 0, shareOfBooks: 0 },
        ],
        linkSources: [
          { source: 'Very Long Source Name With Symbols ~!@#$%^&*() And Spaces', linkCount: 99, bookCount: 4, shareOfBooks: 40 },
          { source: '', linkCount: 1, bookCount: 1, shareOfBooks: 10 },
        ],
        coverStatuses: [
          { status: 'Found', bookCount: 3, shareOfBooks: 30 },
          { status: 'Uploaded', bookCount: 2, shareOfBooks: 20 },
          { status: 'Failed', bookCount: 1, shareOfBooks: 10 },
          { status: 'NotFound', bookCount: 1, shareOfBooks: 10 },
          { status: 'Pending', bookCount: 1, shareOfBooks: 10 },
          { status: 'Mystery', bookCount: 2, shareOfBooks: 20 },
        ],
        coverSources: [
          { source: 'NovelUpdates', bookCount: 5, shareOfBooks: 50 },
          { source: '', bookCount: 1, shareOfBooks: 10 },
        ],
      },
    }))
    const user = userEvent.setup()

    renderWithProviders(<AnalyticsPage />, { route: '/analytics?from=2026-01-01&to=2026-02-01' })

    expect(await screen.findByText(/Cleanup queue/i)).toBeInTheDocument()
    expect(screen.getByText(/Usable covers are counted only/i)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: '10 books' })).toHaveAttribute('href', '/books?query=cover%3Anone')
    expect(screen.getByText(/Unknown status bucket: 2 books/i)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Very Long Source Name/i })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /view data for link sources/i }))
    expect(screen.getByRole('cell', { name: '99' })).toBeInTheDocument()
    expect(screen.getByRole('cell', { name: '4' })).toBeInTheDocument()
  })

  it('keeps analytics type labels readable against their calculated background', async () => {
    vi.mocked(api.getBookAnalytics).mockResolvedValue(createAnalytics())

    renderWithProviders(<AnalyticsPage />, { route: '/analytics?from=2026-01-01&to=2026-02-01' })

    expectReadableTextContrast((await screen.findAllByText('Novel'))[0])
  })
})

type AnalyticsOverrides = Partial<BookAnalyticsDto['overview']> & {
  composition?: Partial<BookAnalyticsDto['composition']>
  planning?: Partial<BookAnalyticsDto['planning']>
  progress?: Partial<BookAnalyticsDto['progress']>
  ratings?: Partial<BookAnalyticsDto['ratings']>
  activity?: Partial<BookAnalyticsDto['activity']>
  libraryGrowth?: Partial<BookAnalyticsDto['libraryGrowth']>
  quality?: Partial<BookAnalyticsDto['quality']>
}

function createAnalytics(overrides: AnalyticsOverrides = {}): BookAnalyticsDto {
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
  const composition = {
    statusByType: overview.totalBooks > 0 ? [{
      type: 'Novel',
      totalBooks: 6,
      statuses: [
        { status: 'Reading', bookCount: 4 },
        { status: 'Completed', bookCount: 2 },
      ],
    }] : [],
    genres: overview.totalBooks > 0 ? [{ name: 'Fantasy', bookCount: 6, shareOfBooks: 60 }] : [],
    tags: overview.totalBooks > 0 ? [{ name: 'favorite', bookCount: 4, shareOfBooks: 40 }] : [],
    ...overrides.composition,
  }
  const ratings = {
    ratedBooks: overview.ratedBooks,
    unratedBooks: overview.unratedBooks,
    averageRating: overview.averageRating,
    counts: Array.from({ length: 10 }, (_unused, index) => ({
      rating: index + 1,
      bookCount: index === 8 ? 1 : 0,
    })),
    ...overrides.ratings,
  }
  const planning = {
    prioritiesByStatus: overview.totalBooks > 0 ? [{
      status: 'Reading',
      totalBooks: 6,
      priorities: [
        { priority: '1', bookCount: 2 },
        { priority: 'Unset', bookCount: 4 },
      ],
    }] : [],
    ...overrides.planning,
  }
  const progress = {
    typeVolumes: overview.totalBooks > 0 ? [{
      type: 'Novel',
      bookCount: 6,
      currentChapters: 468,
      averageCurrentChapter: 78,
      medianCurrentChapter: 64,
    }] : [],
    ...overrides.progress,
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
    composition,
    ratings,
    planning,
    progress,
    activity: {
      points: overview.totalBooks > 0 ? [{
        date: '2026-01-05',
        progressEvents: 3,
        booksTouched: 2,
        chaptersAdvanced: 18,
      }] : [],
      ...overrides.activity,
    },
    libraryGrowth: {
      openingCount: 0,
      points: overview.totalBooks > 0 ? [
        { date: '2026-01-01', booksAdded: 0, cumulativeBooks: 4, byType: [] },
        { date: '2026-01-08', booksAdded: 6, cumulativeBooks: 10, byType: [{ type: 'Novel', bookCount: 6 }] },
      ] : [],
      ...overrides.libraryGrowth,
    },
    quality: {
      fieldCompleteness: overview.totalBooks > 0 ? [
        { field: 'author', bookCount: 9, shareOfBooks: 90 },
        { field: 'genre', bookCount: 6, shareOfBooks: 60 },
        { field: 'usableCover', bookCount: 4, shareOfBooks: 40 },
      ] : [],
      linkSources: overview.totalBooks > 0 ? [
        { source: 'NovelUpdates', linkCount: 8, bookCount: 6, shareOfBooks: 60 },
      ] : [],
      coverStatuses: overview.totalBooks > 0 ? [
        { status: 'Found', bookCount: 4, shareOfBooks: 40 },
        { status: 'Pending', bookCount: 2, shareOfBooks: 20 },
      ] : [],
      coverSources: overview.totalBooks > 0 ? [
        { source: 'NovelUpdates', bookCount: 4, shareOfBooks: 40 },
      ] : [],
      ...overrides.quality,
    },
  }
}
