export const readingTimeStorageKey = 'novelki.books.summary.time-per-chapter.v1'

export function readReadingTimeSettings(storageKey = readingTimeStorageKey) {
  const stored = window.localStorage.getItem(storageKey)
  if (!stored) {
    return {}
  }

  try {
    const parsed = JSON.parse(stored) as Record<string, number>
    return Object.fromEntries(
      Object.entries(parsed).filter((entry): entry is [string, number] => typeof entry[0] === 'string' && Number.isFinite(entry[1])),
    )
  } catch {
    return {}
  }
}

export function writeReadingTimeSettings(settings: Record<string, number>, storageKey = readingTimeStorageKey) {
  window.localStorage.setItem(storageKey, JSON.stringify(settings))
}
