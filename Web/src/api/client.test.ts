import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from './client'

describe('book API NSFW filtering', () => {
  const fetchMock = vi.fn()

  beforeEach(() => {
    fetchMock.mockImplementation(() => Promise.resolve(new Response(JSON.stringify({
      data: [],
      skip: 0,
      take: 20,
      total: 0,
    }), {
      headers: { 'Content-Type': 'application/json' },
      status: 200,
    })))
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('adds the h-manhwa exclusion to every search-capable book request by default', async () => {
    await api.getBooks({ query: 'author:Toika' })
    await api.getBooksSummary({ query: 'author:Toika' })
    await api.getBookAnalytics({ query: 'author:Toika' })
    await api.getAdminBooks({ query: 'author:Toika' })
    await api.downloadBooksExport({ query: 'author:Toika' })
    await api.downloadBooksFullExport({ query: 'author:Toika' })
    await api.searchPublicBooks({ search: 'author:Toika' })

    const urls = fetchMock.mock.calls.map(([input]) => new URL(String(input), 'http://localhost'))

    expect(urls.slice(0, 6).map((url) => url.searchParams.get('query'))).toEqual(
      Array(6).fill('author:Toika -tag:h-manhwa'),
    )
    expect(urls[6].searchParams.get('search')).toBe('author:Toika -tag:h-manhwa')
  })
})
