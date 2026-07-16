import type { ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { addDays, addMonths, addWeeks, format, parseISO } from 'date-fns'

export const chartColors = ['#8b92d8', '#75b69c', '#d1aa6e', '#8e7ea8', '#6f91aa', '#b47f8d', '#879b70', '#657b78']

export const analyticsTooltipProps = {
  contentStyle: {
    backgroundColor: '#121925',
    border: '1px solid #3a465b',
    borderRadius: '3px',
    boxShadow: 'none',
    color: '#eef2f7',
  },
  itemStyle: {
    color: '#eef2f7',
  },
  labelStyle: {
    color: '#eef2f7',
    fontWeight: 700,
  },
}

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

export function DrilldownLink({ children, className = '', query }: { children: ReactNode; className?: string; query: string }) {
  return (
    <Link
      className={`ui-drilldown-link ${className}`}
      title={`Open books filtered by ${query}`}
      to={booksHref(query)}
    >
      {children}
      <span aria-hidden="true" className="text-xs">↗</span>
    </Link>
  )
}

export function useBooksDrilldown() {
  const navigate = useNavigate()
  return (query: string) => navigate(booksHref(query))
}

export function getActiveChartLabel(event: unknown) {
  if (event && typeof event === 'object' && 'activeLabel' in event) {
    const label = event.activeLabel
    return typeof label === 'string' || typeof label === 'number' ? label : null
  }

  return null
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
