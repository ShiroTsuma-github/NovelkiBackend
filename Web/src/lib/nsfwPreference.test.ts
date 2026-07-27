import { describe, expect, it } from 'vitest'
import {
  isNsfwEnabled,
  nsfwPreferenceStorageKey,
  setNsfwEnabled,
  withNsfwBookFilter,
} from './nsfwPreference'

describe('NSFW preference', () => {
  it('fails closed and excludes h-manhwa by default', () => {
    expect(isNsfwEnabled()).toBe(false)
    expect(withNsfwBookFilter()).toBe('-tag:h-manhwa')
    expect(withNsfwBookFilter('author:Toika')).toBe('author:Toika -tag:h-manhwa')
  })

  it('does not duplicate an explicit exclusion', () => {
    expect(withNsfwBookFilter('genre:fantasy -tag:"h-manhwa"')).toBe('genre:fantasy -tag:"h-manhwa"')
  })

  it('leaves the request unchanged after the user enables NSFW content', () => {
    setNsfwEnabled(true)

    expect(window.localStorage.getItem(nsfwPreferenceStorageKey)).toBe('true')
    expect(isNsfwEnabled()).toBe(true)
    expect(withNsfwBookFilter('  tag:h-manhwa  ')).toBe('tag:h-manhwa')
    expect(withNsfwBookFilter()).toBeUndefined()
  })
})
