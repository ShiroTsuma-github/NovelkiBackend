export const nsfwPreferenceStorageKey = 'novelki.nsfw-enabled.v1'
export const nsfwTagExclusion = '-tag:h-manhwa'

const nsfwExclusionPattern = /(^|\s)-tag:(?:"h-manhwa"|h-manhwa)(?=\s|$)/i

export function isNsfwEnabled() {
  if (typeof window === 'undefined') {
    return false
  }

  try {
    return window.localStorage.getItem(nsfwPreferenceStorageKey) === 'true'
  } catch {
    return false
  }
}

export function setNsfwEnabled(enabled: boolean) {
  if (typeof window === 'undefined') {
    return false
  }

  try {
    window.localStorage.setItem(nsfwPreferenceStorageKey, String(enabled))
    return enabled
  } catch {
    // A blocked localStorage should fail closed and keep NSFW content hidden.
    return false
  }
}

export function withNsfwBookFilter(query?: string) {
  const trimmedQuery = query?.trim() ?? ''

  if (isNsfwEnabled() || nsfwExclusionPattern.test(trimmedQuery)) {
    return trimmedQuery || undefined
  }

  return trimmedQuery ? `${nsfwTagExclusion} ${trimmedQuery}` : nsfwTagExclusion
}
