import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { addDays, addMonths, addWeeks, format, parseISO } from 'date-fns'

export const chartColors = ['#0891b2', '#2563eb', '#7c3aed', '#db2777', '#ea580c', '#ca8a04', '#65a30d', '#0f766e']

export function formatCount(value: number) {
  return new Intl.NumberFormat('en-US').format(value)
}

export function formatPercent(value: number) {
  const normalized = Math.min(100, Math.max(0, Number.isFinite(value) ? value : 0))
  const rounded = Math.round(normalized * 10) / 10
  return `${rounded.toFixed(rounded >= 10 || rounded === 0 ? 0 : 1)}%`
}

export function percent(part: number, total: number) {
  return total > 0 ? (part / total) * 100 : 0
}

export function normalizedPercent(part: number, total: number) {
  return Math.round(percent(part, total) * 10) / 10
}

export function booksHref(query: string) {
  return `/books?query=${encodeURIComponent(query)}`
}

export function fieldQuery(field: string, value: string) {
  return `${field}:${quoteQueryValue(value)}`
}

export function numberQuery(field: string, value: string | number) {
  return `${field}:${value}`
}

export function noneQuery(field: string) {
  return `${field}:none`
}

export function quoteQueryValue(value: string) {
  return /^[A-Za-z0-9_-]+$/.test(value) ? value : `"${value.replaceAll('"', '\\"')}"`
}

export function DrilldownLink({ children, query }: { children: ReactNode; query: string }) {
  return (
    <Link
      className="inline-flex min-h-11 items-center gap-1 rounded-md px-2 font-semibold text-cyan-700 underline decoration-cyan-700/60 underline-offset-4 hover:text-cyan-900 hover:decoration-cyan-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyan-500 focus-visible:ring-offset-2"
      title={`Open books filtered by ${query}`}
      to={booksHref(query)}
    >
      {children}
      <span aria-hidden="true" className="text-xs">↗</span>
    </Link>
  )
}

export type AnalyticsDateBucket = 'day' | 'week' | 'month'

export function dateRangeForBucket(date: string, bucket: string): { start: string; end: string } {
  const start = parseISO(date)
  const end = bucket === 'month'
    ? addMonths(start, 1)
    : bucket === 'week'
      ? addWeeks(start, 1)
      : addDays(start, 1)

  return {
    start: format(start, 'yyyy-MM-dd'),
    end: format(end, 'yyyy-MM-dd'),
  }
}

export function formatDateRange(startText: string, endExclusiveText?: string) {
  const start = parseISO(startText)
  const endInclusive = endExclusiveText ? addDays(parseISO(endExclusiveText), -1) : start

  if (format(start, 'yyyy-MM-dd') === format(endInclusive, 'yyyy-MM-dd')) {
    return format(start, 'MMM d, yyyy')
  }

  if (start.getFullYear() !== endInclusive.getFullYear()) {
    return `${format(start, 'MMM d, yyyy')} – ${format(endInclusive, 'MMM d, yyyy')}`
  }

  if (start.getMonth() !== endInclusive.getMonth()) {
    return `${format(start, 'MMM d')} – ${format(endInclusive, 'MMM d, yyyy')}`
  }

  return `${format(start, 'MMMM d')}–${format(endInclusive, 'd, yyyy')}`
}
