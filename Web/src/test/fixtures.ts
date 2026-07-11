import type { BookDto, DictionaryDto, PaginatedResult, TokenResponse } from '@/api/types'

export const testSession: TokenResponse = {
  accessToken: createJwt({ role: 'User' }),
  refreshToken: 'refresh-token',
  tokenType: 'Bearer',
  expiresAt: '2099-01-01T00:00:00Z',
  refreshTokenExpiresAt: '2099-02-01T00:00:00Z',
  userId: '11111111-1111-1111-1111-111111111111',
}

export const dictionaries: DictionaryDto[] = [
  { id: '10000000-0000-0000-0000-000000000001', name: 'Novel' },
  { id: '10000000-0000-0000-0000-000000000002', name: 'Manga' },
]

export const statuses: DictionaryDto[] = [
  { id: '20000000-0000-0000-0000-000000000001', name: 'Reading' },
  { id: '20000000-0000-0000-0000-000000000002', name: 'Completed' },
]

export const genres: DictionaryDto[] = [
  { id: '30000000-0000-0000-0000-000000000001', name: 'Fantasy' },
  { id: '30000000-0000-0000-0000-000000000002', name: 'Slice of Life' },
]

export const books: BookDto[] = [
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

export function paginated<T>(data: T[]): PaginatedResult<T> {
  return {
    skip: 0,
    take: 20,
    total: data.length,
    data,
  }
}

function createJwt(payload: Record<string, unknown>) {
  const encodedPayload = btoa(JSON.stringify(payload))
    .replaceAll('+', '-')
    .replaceAll('/', '_')
    .replaceAll('=', '')
  return `header.${encodedPayload}.signature`
}
