import { describe, expect, it } from 'vitest'
import {
  isNsfwEnabled,
  nsfwPreferenceStorageKey,
  setNsfwEnabled,
  withNsfwBookFilter,
} from './nsfwPreference'

describe('NSFW preference', () => {
  it('fails closed and excludes all configured NSFW metadata by default', () => {
    expect(isNsfwEnabled()).toBe(false)
    expect(withNsfwBookFilter()).toBe('-tag:h-manhwa -genre:Adult -tag:R-18')
    expect(withNsfwBookFilter('author:Toika')).toBe(
      '-tag:h-manhwa -genre:Adult -tag:R-18 author:Toika',
    )
  })

  it('keeps an unfinished quoted user filter at the end of the query', () => {
    expect(withNsfwBookFilter('title:"space')).toBe(
      '-tag:h-manhwa -genre:Adult -tag:R-18 title:"space',
    )
  })

  it('does not duplicate explicit exclusions', () => {
    expect(withNsfwBookFilter('genre:fantasy -tag:"h-manhwa"')).toBe(
      '-genre:Adult -tag:R-18 genre:fantasy -tag:"h-manhwa"',
    )
    expect(withNsfwBookFilter("-tag:h-manhwa -genre:'adult' -tag:R-18")).toBe(
      "-tag:h-manhwa -genre:'adult' -tag:R-18",
    )
  })

  it('leaves the request unchanged after the user enables NSFW content', () => {
    setNsfwEnabled(true)

    expect(window.localStorage.getItem(nsfwPreferenceStorageKey)).toBe('true')
    expect(isNsfwEnabled()).toBe(true)
    expect(withNsfwBookFilter('  tag:h-manhwa  ')).toBe('tag:h-manhwa')
    expect(withNsfwBookFilter()).toBeUndefined()
  })
})
