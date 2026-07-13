export type BookFilters = {
  text: string
  title: string
  author: string
  tag: string
  genre: string
  status: string
  type: string
  ratingMin: string
  progressMin: string
  chaptersMax: string
  totalChapters: string
  priority: string
}

export const emptyFilters: BookFilters = {
  text: '',
  title: '',
  author: '',
  tag: '',
  genre: '',
  status: '',
  type: '',
  ratingMin: '',
  progressMin: '',
  chaptersMax: '',
  totalChapters: '',
  priority: '',
}

export function buildBookQuery(filters: BookFilters) {
  const parts: string[] = []
  addFreeText(parts, filters.text)
  addField(parts, 'title', filters.title)
  addField(parts, 'author', filters.author)
  addField(parts, 'tag', filters.tag)
  addField(parts, 'genre', filters.genre)
  addField(parts, 'status', filters.status)
  addField(parts, 'type', filters.type)
  addNumber(parts, 'rating', '>=', filters.ratingMin)
  addNumber(parts, 'progress', '>=', filters.progressMin)
  addNumber(parts, 'chapters', '<=', filters.chaptersMax)
  addNumber(parts, 'totalChapters', null, filters.totalChapters)
  addNumber(parts, 'priority', null, filters.priority)
  return parts.join(' ')
}

function addNumber(parts: string[], field: string, operator: string | null, value: string) {
  const trimmed = value.trim()
  if (trimmed) {
    parts.push(`${field}:${operator ?? ''}${trimmed}`)
  }
}

function addField(parts: string[], field: string, value: string) {
  const trimmed = value.trim()
  if (trimmed) {
    const values = trimmed
      .split(',')
      .map((item) => item.trim())
      .filter(Boolean)
      .map(quoteIfNeeded)

    parts.push(`${field}:${values.join(',')}`)
  }
}

function addFreeText(parts: string[], value: string) {
  const trimmed = value.trim()
  if (trimmed) {
    parts.push(quoteIfNeeded(trimmed))
  }
}

function quoteIfNeeded(value: string) {
  return /\s/.test(value) ? `"${value.replaceAll('"', '')}"` : value
}
