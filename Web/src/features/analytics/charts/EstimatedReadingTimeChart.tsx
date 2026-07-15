import { useMemo, useState } from 'react'
import type { BookAnalyticsDto } from '@/api/types'
import { inputClass } from '@/components/app/FormField'
import { formatChapterCount } from '@/features/books/BooksPage'
import { readReadingTimeSettings, writeReadingTimeSettings } from '../readingTimeSettings'
import { formatCount } from './chartUtils'

type EstimatedReadingTimeChartProps = {
  data: BookAnalyticsDto | undefined
}

export function EstimatedReadingTimeChart({ data }: EstimatedReadingTimeChartProps) {
  const items = data?.progress.typeVolumes ?? []
  const [minutesPerType, setMinutesPerType] = useState<Record<string, number>>(() => readReadingTimeSettings())
  const estimates = useMemo(() => getEstimatedReadingRows(items, minutesPerType), [items, minutesPerType])
  const totalHours = estimates.reduce((sum, item) => sum + item.hours, 0)

  if (!items.length) {
    return <div className="grid min-h-56 place-items-center text-sm text-slate-500">No chapter data to estimate reading time.</div>
  }

  return (
    <div className="grid gap-4">
      <div className="rounded-xl border border-slate-200 bg-white p-4">
        <div className="text-sm font-semibold text-slate-950">Estimated total</div>
        <div className="mt-1 text-3xl font-semibold text-slate-950">{formatHours(totalHours)}</div>
        <div className="mt-2 text-sm text-slate-500">
          {formatDays(totalHours)} · {formatMonths(totalHours)} · {formatYears(totalHours)} based on known current chapters.
        </div>
      </div>
      <div className="grid gap-2">
        {estimates.map((item) => (
          <label className="grid gap-2 rounded-md border border-slate-200 bg-white p-3" key={item.type}>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <span className="font-semibold text-slate-950">{item.type}</span>
              <span className="text-sm text-slate-500">{formatChapterCount(item.currentChapters)} chapters · {formatHours(item.hours)}</span>
            </div>
            <div className="flex flex-wrap items-center gap-3">
              <input
                aria-label={`${item.type} minutes per chapter`}
                className={`${inputClass} h-10 w-28 border-slate-300 bg-white text-slate-950`}
                min="0"
                step="1"
                type="number"
                value={item.minutesPerChapter}
                onChange={(event) => {
                  const nextValue = normalizeMinutes(Number(event.target.value))
                  setMinutesPerType((current) => {
                    const updated = { ...current, [item.type]: nextValue }
                    writeReadingTimeSettings(updated)
                    return updated
                  })
                }}
              />
              <span className="text-sm text-slate-600">minutes per chapter</span>
            </div>
          </label>
        ))}
      </div>
    </div>
  )
}

export function estimatedReadingTimeRows(data?: BookAnalyticsDto, settings: Record<string, number> = readReadingTimeSettings()) {
  return getEstimatedReadingRows(data?.progress.typeVolumes ?? [], settings).map((item) => [
    item.type,
    formatChapterCount(item.currentChapters),
    formatCount(item.minutesPerChapter),
    formatHours(item.hours),
  ])
}

function getEstimatedReadingRows(items: NonNullable<BookAnalyticsDto['progress']>['typeVolumes'], settings: Record<string, number>) {
  return items.map((item) => {
    const minutesPerChapter = normalizeMinutes(settings[item.type] ?? 5)
    return {
      type: item.type,
      currentChapters: item.currentChapters,
      minutesPerChapter,
      hours: (item.currentChapters * minutesPerChapter) / 60,
    }
  })
}

function normalizeMinutes(value: number) {
  return Number.isFinite(value) ? Math.max(0, value) : 0
}

function formatHours(value: number) {
  return `${value.toLocaleString('en-US', { maximumFractionDigits: 1, minimumFractionDigits: 1 })} h`
}

function formatDays(value: number) {
  return `${(value / 24).toLocaleString('en-US', { maximumFractionDigits: 1, minimumFractionDigits: 1 })} days`
}

function formatMonths(value: number) {
  return `${(value / (24 * 30)).toLocaleString('en-US', { maximumFractionDigits: 1, minimumFractionDigits: 1 })} months`
}

function formatYears(value: number) {
  return `${(value / (24 * 365)).toLocaleString('en-US', { maximumFractionDigits: 1, minimumFractionDigits: 1 })} years`
}
