import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'

export const chartColors = ['#0891b2', '#2563eb', '#7c3aed', '#db2777', '#ea580c', '#ca8a04', '#65a30d', '#0f766e']

export function formatCount(value: number) {
  return new Intl.NumberFormat('en-US').format(value)
}

export function formatPercent(value: number) {
  return `${value.toFixed(value >= 10 ? 0 : 1)}%`
}

export function percent(part: number, total: number) {
  return total > 0 ? (part / total) * 100 : 0
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
      className="inline-flex min-h-11 items-center rounded-md px-2 font-semibold text-cyan-700 underline-offset-4 hover:text-cyan-900 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyan-500 focus-visible:ring-offset-2"
      to={booksHref(query)}
    >
      {children}
    </Link>
  )
}
