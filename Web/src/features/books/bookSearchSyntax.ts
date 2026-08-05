export type BookSearchFilterKind = 'text' | 'number' | 'date' | 'missing'

export type BookSearchFilterDefinition = {
  canonical: string
  aliases: string[]
  description: string
  kind: BookSearchFilterKind
  remote?: boolean
  suggestible?: boolean
  displayValueHint?: string
  supportsNone?: boolean
  fixedValue?: 'none'
}

export const bookSearchFilters: BookSearchFilterDefinition[] = [
  { canonical: 'title', aliases: [], description: 'Search primary and alternative titles', kind: 'text', displayValueHint: '#' },
  { canonical: 'author', aliases: [], description: 'Filter by author or author alias', kind: 'text', remote: true, supportsNone: true },
  { canonical: 'description', aliases: [], description: 'Search book descriptions', kind: 'text', supportsNone: true, displayValueHint: '#' },
  { canonical: 'tag', aliases: ['tags'], description: 'Filter by tag', kind: 'text', remote: true, supportsNone: true },
  { canonical: 'genre', aliases: ['genres'], description: 'Filter by genre', kind: 'text', remote: true, supportsNone: true },
  { canonical: 'status', aliases: [], description: 'Filter by reading status', kind: 'text', remote: true },
  { canonical: 'type', aliases: ['contentType'], description: 'Filter by content type', kind: 'text', remote: true },
  { canonical: 'rating', aliases: [], description: 'Compare rating', kind: 'number', supportsNone: true },
  { canonical: 'priority', aliases: [], description: 'Compare priority', kind: 'number', supportsNone: true },
  { canonical: 'progress', aliases: ['current', 'currentChapter'], description: 'Compare current chapter', kind: 'number', supportsNone: true },
  { canonical: 'chapters', aliases: ['chapter', 'total', 'total-chapters', 'totalChapters'], description: 'Compare total chapters', kind: 'number', supportsNone: true },
  { canonical: 'created', aliases: ['createDate', 'createdDate'], description: 'Compare creation date', kind: 'date' },
  { canonical: 'updated', aliases: ['updateDate', 'updatedDate', 'lastModified'], description: 'Compare last update date', kind: 'date' },
  { canonical: 'alternativeTitle', aliases: ['alternateTitle'], description: 'Books without an alternative title', kind: 'missing', suggestible: false, fixedValue: 'none' },
  { canonical: 'cover', aliases: [], description: 'Books without a usable cover', kind: 'missing', suggestible: false, fixedValue: 'none' },
  { canonical: 'links', aliases: ['link'], description: 'Books without links', kind: 'missing', suggestible: false, fixedValue: 'none' },
]

const definitionsByAlias = new Map<string, BookSearchFilterDefinition>()
for (const definition of bookSearchFilters) {
  definitionsByAlias.set(definition.canonical.toLocaleLowerCase(), definition)
  for (const alias of definition.aliases) {
    definitionsByAlias.set(alias.toLocaleLowerCase(), definition)
  }
}

export type BookSearchTokenContext = {
  start: number
  end: number
  text: string
  excluded: boolean
  fieldText: string
  definition?: BookSearchFilterDefinition
  hasColon: boolean
  valueText: string
  valueStart: number
  valueEnd: number
  complete: boolean
}

export type BookSearchSuggestionItem = {
  id: string
  kind: 'filter' | 'operator' | 'value' | 'wildcard' | 'none' | 'exclude' | 'include' | 'remove'
  label: string
  description: string
  value?: string
  definition?: BookSearchFilterDefinition
}

export function analyzeBookSearch(value: string, requestedCaret: number): BookSearchTokenContext {
  const caret = Math.max(0, Math.min(requestedCaret, value.length))
  const tokens = tokenize(value)
  const token = tokens.find((candidate) =>
    caret > candidate.start && caret <= candidate.end ||
    caret === candidate.start,
  ) ?? { start: caret, end: caret }
  const text = value.slice(token.start, token.end)
  const excluded = text.startsWith('-') && text.length > 1
  const bodyOffset = excluded ? 1 : 0
  const colonOffset = text.indexOf(':', bodyOffset)
  const hasColon = colonOffset >= 0
  const fieldText = text.slice(bodyOffset, hasColon ? colonOffset : text.length)
  const definition = definitionsByAlias.get(fieldText.toLocaleLowerCase())
  const rawValueStart = hasColon ? token.start + colonOffset + 1 : token.end
  const segment = hasColon
    ? findActiveValueSegment(value, rawValueStart, token.end, caret)
    : { start: token.end, end: token.end }
  const valueText = value.slice(segment.start, segment.end).trim()

  return {
    start: token.start,
    end: token.end,
    text,
    excluded,
    fieldText,
    definition,
    hasColon,
    valueText,
    valueStart: segment.start,
    valueEnd: segment.end,
    complete: Boolean(definition && hasColon && isCompleteFilterValue(definition, valueText)),
  }
}

export function getLocalBookSearchSuggestions(context: BookSearchTokenContext): BookSearchSuggestionItem[] {
  if (!context.hasColon || !context.definition) {
    const search = context.fieldText.toLocaleLowerCase()
    return bookSearchFilters
      .filter((definition) =>
        definition.suggestible !== false &&
        (!search || [
          definition.canonical,
          ...definition.aliases,
          definition.description,
        ].some((candidate) => candidate.toLocaleLowerCase().includes(search))))
      .map((definition) => ({
        id: `filter:${definition.canonical}`,
        kind: 'filter',
        label: `${definition.canonical}:${definition.displayValueHint ?? ''}`,
        description: definition.description,
        definition,
      }))
  }

  const items = getTokenActions(context)
  const definition = context.definition
  const normalizedValue = unquote(context.valueText).toLocaleLowerCase()

  if (
    definition.kind === 'text' &&
    (definition.canonical === 'title' || definition.canonical === 'description') &&
    !normalizedValue
  ) {
    items.push({
      id: `wildcard:${definition.canonical}`,
      kind: 'wildcard',
      label: '* wildcard',
      description: `Matches any sequence of characters, for example ${definition.canonical}:"Lord *"`,
      value: '*',
      definition,
    })
  }

  if ((definition.kind === 'number' || definition.kind === 'date') && /^[<>=]*$/.test(normalizedValue)) {
    for (const operator of ['>=', '<=', '>', '<', '=']) {
      if (!normalizedValue || operator.startsWith(normalizedValue)) {
        items.push({
          id: `operator:${operator}`,
          kind: 'operator',
          label: `${context.fieldText}:${operator}${definition.kind === 'date' ? 'YYYY-MM-DD' : 'N'}`,
          description: operatorDescription(operator, definition.kind),
          value: operator,
        })
      }
    }
  }

  return items
}

export function applyBookSearchSuggestion(
  query: string,
  context: BookSearchTokenContext,
  suggestion: BookSearchSuggestionItem,
): { value: string; caret: number } {
  if (suggestion.kind === 'remove') {
    return removeToken(query, context.start, context.end)
  }

  if (suggestion.kind === 'exclude' || suggestion.kind === 'include') {
    const token = query.slice(context.start, context.end)
    const replacement = suggestion.kind === 'exclude'
      ? token.startsWith('-') ? token : `-${token}`
      : token.startsWith('-') ? token.slice(1) : token
    return replaceRange(query, context.start, context.end, replacement)
  }

  if (suggestion.kind === 'filter' && suggestion.definition) {
    const prefix = context.excluded ? '-' : ''
    if (suggestion.definition.fixedValue) {
      return replaceRange(
        query,
        context.start,
        context.end,
        `${prefix}${suggestion.definition.canonical}:none`,
      )
    }

    if (suggestion.definition.kind === 'text') {
      const result = replaceRange(
        query,
        context.start,
        context.end,
        `${prefix}${suggestion.definition.canonical}:""`,
      )
      return { value: result.value, caret: result.caret - 1 }
    }

    return replaceRange(
      query,
      context.start,
      context.end,
      `${prefix}${suggestion.definition.canonical}:`,
    )
  }

  if (suggestion.kind === 'operator' && suggestion.value != null) {
    return replaceRange(query, context.valueStart, context.valueEnd, suggestion.value)
  }

  if (suggestion.kind === 'none') {
    return replaceRange(query, context.valueStart, context.valueEnd, 'none')
  }

  if (suggestion.kind === 'wildcard') {
    const result = replaceRange(query, context.valueStart, context.valueEnd, '"*"')
    return { value: result.value, caret: result.caret - 1 }
  }

  if (suggestion.kind === 'value' && suggestion.value != null) {
    return replaceValueAndAdvance(
      query,
      context,
      `"${sanitizeQuotedValue(suggestion.value)}"`,
    )
  }

  return { value: query, caret: context.end }
}

export function getBookSearchValue(context: BookSearchTokenContext) {
  return unquote(context.valueText)
}

export function getBookSearchScopeQuery(query: string, context: BookSearchTokenContext) {
  const before = query.slice(0, context.start).trimEnd()
  const after = query.slice(context.end).trimStart()
  if (!before) {
    return after
  }
  if (!after) {
    return before
  }
  return `${before} ${after}`
}

function getTokenActions(context: BookSearchTokenContext): BookSearchSuggestionItem[] {
  if (!context.complete) {
    return []
  }

  return [
    {
      id: context.excluded ? 'action:include' : 'action:exclude',
      kind: context.excluded ? 'include' : 'exclude',
      label: context.excluded ? `Include ${context.text.slice(1)}` : `Exclude ${context.text}`,
      description: context.excluded ? 'Remove the exclusion prefix' : 'Prefix this filter with -',
    },
    {
      id: 'action:remove',
      kind: 'remove',
      label: `Remove ${context.text}`,
      description: 'Delete this filter from the search',
    },
  ]
}

function tokenize(value: string) {
  const tokens: Array<{ start: number; end: number }> = []
  let start: number | null = null
  let quote: '"' | "'" | null = null

  for (let index = 0; index < value.length; index += 1) {
    const character = value[index]
    if (start == null && !/\s/.test(character)) {
      start = index
    }

    if ((character === '"' || character === "'") && start != null) {
      quote = quote === character ? null : quote == null ? character : quote
      continue
    }

    if (start != null && /\s/.test(character) && quote == null) {
      const current = value.slice(start, index)
      if (current.includes(':') && current.trimEnd().endsWith(',')) {
        continue
      }

      tokens.push({ start, end: index })
      start = null
    }
  }

  if (start != null) {
    tokens.push({ start, end: value.length })
  }

  return tokens
}

function findActiveValueSegment(value: string, start: number, end: number, caret: number) {
  let quote: '"' | "'" | null = null
  let segmentStart = start
  let segmentEnd = end

  for (let index = start; index < end; index += 1) {
    const character = value[index]
    if (character === '"' || character === "'") {
      quote = quote === character ? null : quote == null ? character : quote
      continue
    }

    if (character === ',' && quote == null) {
      if (index < caret) {
        segmentStart = index + 1
      } else {
        segmentEnd = index
        break
      }
    }
  }

  while (segmentStart < segmentEnd && /\s/.test(value[segmentStart])) {
    segmentStart += 1
  }
  while (segmentEnd > segmentStart && /\s/.test(value[segmentEnd - 1])) {
    segmentEnd -= 1
  }

  return { start: segmentStart, end: segmentEnd }
}

function replaceRange(query: string, start: number, end: number, replacement: string) {
  const value = `${query.slice(0, start)}${replacement}${query.slice(end)}`
  return { value, caret: start + replacement.length }
}

function replaceValueAndAdvance(
  query: string,
  context: BookSearchTokenContext,
  replacement: string,
) {
  const result = replaceRange(query, context.valueStart, context.valueEnd, replacement)
  if (context.valueEnd !== context.end) {
    return result
  }

  if (context.end === query.length) {
    return {
      value: `${result.value} `,
      caret: result.caret + 1,
    }
  }

  if (/\s/.test(query[context.end])) {
    return {
      value: result.value,
      caret: result.caret + 1,
    }
  }

  return result
}

function removeToken(query: string, start: number, end: number) {
  let removeStart = start
  let removeEnd = end
  while (removeEnd < query.length && /\s/.test(query[removeEnd])) {
    removeEnd += 1
  }
  if (removeEnd === end) {
    while (removeStart > 0 && /\s/.test(query[removeStart - 1])) {
      removeStart -= 1
    }
  }
  return { value: query.slice(0, removeStart) + query.slice(removeEnd), caret: removeStart }
}

function unquote(value: string) {
  const trimmed = value.trim()
  if ((trimmed.startsWith('"') && trimmed.endsWith('"')) ||
      (trimmed.startsWith("'") && trimmed.endsWith("'"))) {
    return trimmed.slice(1, -1)
  }
  return trimmed.replace(/^["']/, '')
}

function sanitizeQuotedValue(value: string) {
  return value.replaceAll('"', '').trim()
}

function isCompleteFilterValue(definition: BookSearchFilterDefinition, value: string) {
  const normalized = unquote(value)
  if (!normalized) {
    return false
  }
  if (definition.supportsNone && normalized.toLocaleLowerCase() === 'none') {
    return true
  }
  if (definition.kind === 'missing') {
    return normalized.toLocaleLowerCase() === 'none'
  }
  if (definition.kind === 'number') {
    return /^(?:>=|<=|>|<|=)?-?\d+(?:\.\d+)?$/.test(normalized)
  }
  if (definition.kind === 'date') {
    return /^(?:>=|<=|>|<|=)(?:\d{4}(?:-\d{1,2}(?:-\d{1,2})?)?|\d{1,2}[./]\d{1,2}[./]\d{4})$/.test(normalized)
  }
  return true
}

function operatorDescription(operator: string, kind: 'number' | 'date') {
  const noun = kind === 'date' ? 'date' : 'value'
  return ({
    '>': `More than the entered ${noun}`,
    '>=': `At least the entered ${noun}`,
    '<': `Less than the entered ${noun}`,
    '<=': `At most the entered ${noun}`,
    '=': `Exactly the entered ${noun}`,
  } as Record<string, string>)[operator]
}
