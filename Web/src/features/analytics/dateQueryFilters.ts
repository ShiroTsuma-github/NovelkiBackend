type AnalyticsDateFilters = {
  query: string
  from?: string
  to?: string
}

const dateFilterAliases = new Set([
  'created',
  'createdate',
  'createddate',
  'updated',
  'updatedate',
  'updateddate',
  'lastmodified',
])

export function extractAnalyticsDateFilters(query: string): AnalyticsDateFilters {
  const keptTokens: string[] = []
  let from: string | undefined
  let to: string | undefined

  for (const token of tokenizeQuery(query)) {
    const parsed = parseDateFilterToken(token)
    if (!parsed) {
      keptTokens.push(token)
      continue
    }

    if (parsed.from) {
      from = maxDate(from, parsed.from)
    }
    if (parsed.to) {
      to = minDate(to, parsed.to)
    }
  }

  return {
    query: keptTokens.join(' ').trim(),
    from,
    to,
  }
}

function parseDateFilterToken(token: string) {
  const separatorIndex = token.indexOf(':')
  if (separatorIndex <= 0 || separatorIndex === token.length - 1) {
    return null
  }

  const field = token.slice(0, separatorIndex).trim().toLowerCase()
  if (!dateFilterAliases.has(field)) {
    return null
  }

  let value = unquote(token.slice(separatorIndex + 1).trim())
  let operator = '='
  for (const candidate of ['>=', '<=', '>', '<', '=']) {
    if (value.startsWith(candidate)) {
      operator = candidate
      value = unquote(value.slice(candidate.length).trim())
      break
    }
  }

  const period = parseDatePeriod(value)
  if (!period) {
    return null
  }

  switch (operator) {
    case '>':
      return { from: period.endExclusive }
    case '>=':
      return { from: period.start }
    case '<':
      return { to: period.start }
    case '<=':
      return { to: period.endExclusive }
    default:
      return { from: period.start, to: period.endExclusive }
  }
}

function parseDatePeriod(value: string) {
  const day = value.match(/^(\d{4})-(\d{2})-(\d{2})$/)
    ?? value.match(/^(\d{1,2})[./](\d{1,2})[./](\d{4})$/)
  if (day) {
    const year = day[1].length === 4 ? Number(day[1]) : Number(day[3])
    const month = day[1].length === 4 ? Number(day[2]) : Number(day[2])
    const date = day[1].length === 4 ? Number(day[3]) : Number(day[1])
    const start = toDateValue(year, month, date)
    return start ? { start, endExclusive: addUtcDays(start, 1) } : null
  }

  const month = value.match(/^(\d{4})[-/](\d{1,2})$/)
    ?? value.match(/^(\d{1,2})[./](\d{4})$/)
  if (month) {
    const year = month[1].length === 4 ? Number(month[1]) : Number(month[2])
    const monthNumber = month[1].length === 4 ? Number(month[2]) : Number(month[1])
    const start = toDateValue(year, monthNumber, 1)
    return start ? { start, endExclusive: addUtcMonths(start, 1) } : null
  }

  if (/^\d{4}$/.test(value)) {
    const year = Number(value)
    const start = toDateValue(year, 1, 1)
    return start ? { start, endExclusive: toDateValue(year + 1, 1, 1) } : null
  }

  return null
}

function tokenizeQuery(query: string) {
  const tokens: string[] = []
  let token = ''
  let quote: string | null = null

  for (const char of query) {
    if ((char === '"' || char === "'") && (!quote || quote === char)) {
      quote = quote ? null : char
      token += char
      continue
    }

    if (/\s/.test(char) && !quote) {
      if (token.trim()) {
        tokens.push(token.trim())
      }
      token = ''
      continue
    }

    token += char
  }

  if (token.trim()) {
    tokens.push(token.trim())
  }

  return tokens
}

function unquote(value: string) {
  if (value.length >= 2 && ((value[0] === '"' && value.at(-1) === '"') || (value[0] === "'" && value.at(-1) === "'"))) {
    return value.slice(1, -1)
  }

  return value
}

function toDateValue(year: number, month: number, day: number) {
  const date = new Date(Date.UTC(year, month - 1, day))
  if (date.getUTCFullYear() !== year || date.getUTCMonth() !== month - 1 || date.getUTCDate() !== day) {
    return null
  }

  return date.toISOString().slice(0, 10)
}

function addUtcDays(value: string, days: number) {
  const date = new Date(`${value}T00:00:00Z`)
  date.setUTCDate(date.getUTCDate() + days)
  return date.toISOString().slice(0, 10)
}

function addUtcMonths(value: string, months: number) {
  const date = new Date(`${value}T00:00:00Z`)
  date.setUTCMonth(date.getUTCMonth() + months)
  return date.toISOString().slice(0, 10)
}

function maxDate(left: string | undefined, right: string) {
  return !left || right > left ? right : left
}

function minDate(left: string | undefined, right: string) {
  return !left || right < left ? right : left
}
