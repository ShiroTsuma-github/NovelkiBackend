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
    await api.getManagedBooks({ query: 'author:Toika' })
    await api.getBooksSummary({ query: 'author:Toika' })
    await api.getBookAnalytics({ query: 'author:Toika' })
    await api.getAdminBooks({ query: 'author:Toika' })
    await api.downloadBooksExport({ query: 'author:Toika' })
    await api.downloadBooksFullExport({ query: 'author:Toika' })
    await api.searchPublicBooks({ search: 'author:Toika' })

    const urls = fetchMock.mock.calls.map(([input]) => new URL(String(input), 'http://localhost'))

    expect(urls.slice(0, 7).map((url) => url.searchParams.get('query'))).toEqual(
      Array(7).fill('-tag:h-manhwa author:Toika'),
    )
    expect(urls[7].searchParams.get('search')).toBe('-tag:h-manhwa author:Toika')
  })

  it('uses the dedicated manage endpoint for the owners listing inventory', async () => {
    await api.getManagedBooks({
      skip: 0,
      take: 50,
    })

    const url = new URL(String(fetchMock.mock.calls[0][0]), 'http://localhost')
    expect(url.pathname).toBe('/api/v1/book/manage')
    expect(url.searchParams.get('query')).toBe('-tag:h-manhwa')
  })
})
