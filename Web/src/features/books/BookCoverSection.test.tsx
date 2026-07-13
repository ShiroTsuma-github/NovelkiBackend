import { act, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { BookCoverDto } from '@/api/types'
import { BookCoverArtwork } from './BookCoverSection'

vi.mock('@/api/http', () => ({
  API_BASE_URL: 'https://api.example.com/api/v1',
  getStoredSession: () => ({ accessToken: 'token-123' }),
}))

const baseCover: BookCoverDto = {
  id: 'cover-1',
  status: 'Ready',
  imageUrl: '/covers/1',
  thumbnailImageUrl: '/covers/1/thumb',
  lastAttemptAt: '2026-07-12T10:00:00Z',
}

describe('BookCoverSection', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      blob: async () => new Blob(['cover']),
    }))
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:cover-1')
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {})
  })

  afterEach(() => {
    vi.runOnlyPendingTimers()
    vi.useRealTimers()
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('reuses the cached blob url when the same cover is mounted again', async () => {
    const firstRender = render(<BookCoverArtwork cover={baseCover} title="Book cover" />)

    await act(async () => {
      await Promise.resolve()
      await Promise.resolve()
    })
    expect(screen.getByRole('img', { name: 'Book cover' })).toHaveAttribute('src', 'blob:cover-1')
    firstRender.unmount()

    const secondRender = render(<BookCoverArtwork cover={baseCover} title="Book cover" />)

    await act(async () => {
      await Promise.resolve()
    })
    expect(screen.getByRole('img', { name: 'Book cover' })).toHaveAttribute('src', 'blob:cover-1')
    expect(fetch).toHaveBeenCalledTimes(1)
    expect(URL.createObjectURL).toHaveBeenCalledTimes(1)

    secondRender.unmount()
  })

  it('releases cached object urls after the cache ttl elapses', async () => {
    const result = render(<BookCoverArtwork cover={baseCover} title="Book cover" />)

    await act(async () => {
      await Promise.resolve()
      await Promise.resolve()
    })
    expect(screen.getByRole('img', { name: 'Book cover' })).toHaveAttribute('src', 'blob:cover-1')
    result.unmount()

    act(() => {
      vi.advanceTimersByTime(60_000)
    })

    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:cover-1')
  })

  it('invalidates the cached image when the cover attempt timestamp changes', async () => {
    const { rerender } = render(<BookCoverArtwork cover={baseCover} title="Book cover" />)

    await act(async () => {
      await Promise.resolve()
      await Promise.resolve()
    })
    expect(fetch).toHaveBeenCalledTimes(1)

    vi.mocked(URL.createObjectURL).mockReturnValueOnce('blob:cover-2')
    rerender(
      <BookCoverArtwork
        cover={{ ...baseCover, lastAttemptAt: '2026-07-12T10:05:00Z' }}
        title="Book cover"
      />,
    )

    await act(async () => {
      await Promise.resolve()
      await Promise.resolve()
    })
    expect(fetch).toHaveBeenCalledTimes(2)
    expect(screen.getByRole('img', { name: 'Book cover' })).toHaveAttribute('src', 'blob:cover-2')
  })

  it('uses the thumbnail url when thumbnail variant is requested', async () => {
    render(<BookCoverArtwork cover={baseCover} preferredVariant="thumbnail" title="Book cover" />)

    await act(async () => {
      await Promise.resolve()
      await Promise.resolve()
    })

    expect(fetch).toHaveBeenCalledWith('https://api.example.com/covers/1/thumb', expect.anything())
    expect(screen.getByRole('img', { name: 'Book cover' })).toHaveAttribute('src', 'blob:cover-1')
  })
})
