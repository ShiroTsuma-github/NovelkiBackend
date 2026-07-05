export type BookFilters = {
  text: string
  title: string
  author: string
  tag: string
  genre: string
  status: string
  type: string
  ratingMin: string
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
  if (filters.ratingMin) {
    parts.push(`rating>=${filters.ratingMin}`)
  }
  if (filters.priority) {
    parts.push(`priority=${filters.priority}`)
  }
  return parts.join(' ')
}

function addField(parts: string[], field: string, value: string) {
  const trimmed = value.trim()
  if (trimmed) {
    parts.push(`${field}:${quoteIfNeeded(trimmed)}`)
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
