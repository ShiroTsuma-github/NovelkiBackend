import type { Page } from '@playwright/test'

export const layoutBooks = [
  {
    id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    created: '2026-01-01T10:00:00Z',
    lastModified: '2026-02-01T10:00:00Z',
    primaryTitle: 'Lord of Mysteries',
    description: 'A long fantasy mystery.',
    alternativeTitles: ['LOTM'],
    authorId: 'author-1',
    author: 'Cuttlefish',
    contentType: 'Novel',
    status: 'Reading',
    currentChapterNumber: 348,
    currentChapterLabel: '348',
    totalChapters: 1432,
    rating: 9,
    priority: 1,
    notes: 'Private note',
    progressHistory: [],
    genres: ['Fantasy'],
    tags: ['favorite', 'mystery'],
    links: [],
  },
  {
    id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    created: '2026-01-02T10:00:00Z',
    lastModified: '2026-02-02T10:00:00Z',
    primaryTitle: 'A Very Long Book Title That Should Stay Inside Its Card Container Without Pushing Actions Away',
    alternativeTitles: [],
    author: 'Toika',
    contentType: 'Novel',
    status: 'Completed',
    currentChapterNumber: 120,
    totalChapters: 120,
    rating: null,
    priority: 2,
    progressHistory: [],
    genres: ['Slice of Life'],
    tags: ['completed'],
    links: [],
  },
]

const dictionaries = {
  type: [
    { id: '10000000-0000-0000-0000-000000000001', name: 'Novel' },
    { id: '10000000-0000-0000-0000-000000000002', name: 'Manga' },
  ],
  status: [
    { id: '20000000-0000-0000-0000-000000000001', name: 'Reading' },
    { id: '20000000-0000-0000-0000-000000000002', name: 'Completed' },
  ],
  genre: [
    { id: '30000000-0000-0000-0000-000000000001', name: 'Fantasy' },
    { id: '30000000-0000-0000-0000-000000000002', name: 'Slice of Life' },
  ],
}

export const invalidImportSession = {
  sessionId: 'layout-session',
  fileName: 'invalid-books.csv',
  totalRows: 80,
  validRows: 0,
  invalidRows: 80,
  canFinalize: false,
  availableContentTypes: ['Novel', 'Manga'],
  availableStatuses: ['Reading', 'Completed'],
  rows: Array.from({ length: 80 }, (_unused, index) => ({
    rowId: `layout-row-${index + 1}`,
    lineNumber: index + 2,
    isValid: false,
    primaryTitle: `Broken imported title ${index + 1}`,
    authorName: null,
    contentType: 'Wrong',
    status: 'Reading',
    tags: null,
    totalChapters: null,
    currentChapterNumber: null,
    currentChapterLabel: null,
    rating: null,
    priority: null,
    description: null,
    notes: null,
    rawImportedLine: null,
    errors: ['Content type is required and must exist.'],
    fieldErrors: {
      contentType: ['Content type is required and must exist.'],
    },
  })),
}

const analytics = {
  generatedAt: '2026-07-15T12:00:00Z',
  scope: {
    query: null,
    from: '2026-01-01',
    to: '2026-02-01',
    bucket: 'week',
  },
  overview: {
    totalBooks: 10,
    ratedBooks: 8,
    unratedBooks: 2,
    averageRating: 8.4,
    currentChapters: 468,
    booksWithKnownCurrentChapter: 9,
    booksWithoutKnownCurrentChapter: 1,
  },
  composition: {
    statusByType: [{
      type: 'Novel',
      totalBooks: 6,
      statuses: [
        { status: 'Reading', bookCount: 4 },
        { status: 'Completed', bookCount: 2 },
      ],
    }],
    genres: [{ name: 'Fantasy', bookCount: 6, shareOfBooks: 60 }],
    tags: [{ name: 'favorite', bookCount: 4, shareOfBooks: 40 }],
  },
  ratings: {
    ratedBooks: 8,
    unratedBooks: 2,
    averageRating: 8.4,
    counts: Array.from({ length: 10 }, (_unused, index) => ({
      rating: index + 1,
      bookCount: index === 8 ? 1 : 0,
    })),
  },
  planning: {
    prioritiesByStatus: [{
      status: 'Reading',
      totalBooks: 6,
      priorities: [
        { priority: '1', bookCount: 2 },
        { priority: 'Unset', bookCount: 4 },
      ],
    }],
  },
  progress: {
    typeVolumes: [{
      type: 'Novel',
      bookCount: 6,
      currentChapters: 468,
      averageCurrentChapter: 78,
      medianCurrentChapter: 64,
    }],
  },
  activity: {
    points: [{
      date: '2026-01-05',
      progressEvents: 3,
      booksTouched: 2,
      chaptersAdvanced: 18,
    }],
  },
  libraryGrowth: {
    openingCount: 0,
    points: [
      { date: '2026-01-01', booksAdded: 0, cumulativeBooks: 4, byType: [] },
      { date: '2026-01-08', booksAdded: 6, cumulativeBooks: 10, byType: [{ type: 'Novel', bookCount: 6 }] },
    ],
  },
  quality: {
    fieldCompleteness: [
      { field: 'author', bookCount: 9, shareOfBooks: 90 },
      { field: 'genre', bookCount: 6, shareOfBooks: 60 },
      { field: 'usableCover', bookCount: 4, shareOfBooks: 40 },
    ],
    linkSources: [
      { source: 'NovelUpdates', linkCount: 8, bookCount: 6, shareOfBooks: 60 },
    ],
    coverStatuses: [
      { status: 'Found', bookCount: 4, shareOfBooks: 40 },
      { status: 'Pending', bookCount: 2, shareOfBooks: 20 },
    ],
    coverSources: [
      { source: 'NovelUpdates', bookCount: 4, shareOfBooks: 40 },
    ],
  },
}

export async function seedAuthenticatedSession(page: Page) {
  await page.addInitScript(() => {
    window.localStorage.setItem('novelki.session', JSON.stringify({
      accessToken: 'header.eyJyb2xlIjoiVXNlciJ9.signature',
      refreshToken: 'refresh-token',
      tokenType: 'Bearer',
      expiresAt: '2099-01-01T00:00:00Z',
      refreshTokenExpiresAt: '2099-02-01T00:00:00Z',
      userId: '11111111-1111-1111-1111-111111111111',
      createdAt: '2026-01-01T00:00:00Z',
    }))
  })
}

export async function installLayoutApiMocks(page: Page) {
  await page.route('**/api/v1/**', async (route) => {
    const url = new URL(route.request().url())
    const path = url.pathname.replace('/api/v1/', '')

    if (path === 'book/import/sessions') {
      await route.fulfill({ json: invalidImportSession })
      return
    }

    if (path === 'book/analytics') {
      await route.fulfill({ json: analytics })
      return
    }

    if (path === 'book') {
      await route.fulfill({ json: { skip: 0, take: 20, total: layoutBooks.length, data: layoutBooks } })
      return
    }

    if (path.startsWith('book/')) {
      await route.fulfill({ json: layoutBooks[0] })
      return
    }

    if (path in dictionaries) {
      const data = dictionaries[path as keyof typeof dictionaries]
      await route.fulfill({
        json: {
          skip: 0,
          take: 100,
          total: data.length,
          data,
        },
      })
      return
    }

    await route.fulfill({ status: 204 })
  })
}
