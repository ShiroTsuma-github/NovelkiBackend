import { Check, Pencil } from 'lucide-react'
import { useMemo, useState } from 'react'
import type { BookAnalyticsDto } from '@/api/types'
import { buttonVariants, Surface } from '@/components/app/DesignSystem'
import { inputClass } from '@/components/app/FormField'
import { formatChapterCount } from '@/features/books/BooksPage'
import { formatCount } from './chartUtils'

type EstimatedReadingTimeChartProps = {
  data: BookAnalyticsDto | undefined
  settings: Record<string, number>
  isSaving?: boolean
  onSave: (settings: Record<string, number>) => Promise<unknown>
}

export function EstimatedReadingTimeChart({ data, settings, isSaving = false, onSave }: EstimatedReadingTimeChartProps) {
  const items = data?.progress.typeVolumes ?? []
  const minutesPerType = useMemo(() => getMinutesByType(items, settings), [items, settings])
  const [draftMinutesPerType, setDraftMinutesPerType] = useState<Record<string, number>>({})
  const [isEditing, setIsEditing] = useState(false)
  const estimates = useMemo(
    () => getEstimatedReadingRows(items, isEditing ? draftMinutesPerType : minutesPerType),
    [draftMinutesPerType, isEditing, items, minutesPerType],
  )
  const totalHours = estimates.reduce((sum, item) => sum + item.hours, 0)

  if (!items.length) {
    return <div className="grid min-h-56 place-items-center text-sm text-slate-500">No chapter data to estimate reading time.</div>
  }

  return (
    <div className="grid gap-4">
      <Surface as="div" className="p-4" tone="elevated">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <div className="text-sm font-semibold text-slate-950">Estimated total</div>
            <div className="mt-1 text-3xl font-semibold text-slate-950">{formatHours(totalHours)}</div>
          </div>
          <button
            aria-pressed={isEditing}
            className={buttonVariants.secondary}
            type="button"
            disabled={isSaving}
            onClick={async () => {
              if (isEditing) {
                await onSave(draftMinutesPerType)
                setIsEditing(false)
                return
              }

              setDraftMinutesPerType(minutesPerType)
              setIsEditing(true)
            }}
          >
            {isEditing ? <Check className="h-4 w-4" /> : <Pencil className="h-4 w-4" />}
            {isEditing ? 'Done' : 'Edit estimates'}
          </button>
        </div>
        <div className="mt-2 text-sm text-slate-500">
          {formatDays(totalHours)} · {formatMonths(totalHours)} · {formatYears(totalHours)} based on known current chapters.
        </div>
      </Surface>
      <div className="grid gap-2">
        {estimates.map((item) => (
          <Surface as="div" className="grid gap-2 p-3" key={item.type}>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <span className="font-semibold text-slate-950">{item.type}</span>
              <span className="text-sm text-slate-500">{formatChapterCount(item.currentChapters)} chapters · {formatHours(item.hours)}</span>
            </div>
            {isEditing ? (
              <div className="flex flex-wrap items-center gap-3">
                <input
                  aria-label={`${item.type} minutes per chapter`}
                  className={`${inputClass} w-28`}
                  min="0"
                  step="1"
                  type="number"
                  value={item.minutesPerChapter}
                  onChange={(event) => {
                    const nextValue = normalizeMinutes(Number(event.target.value))
                    setDraftMinutesPerType((current) => ({ ...current, [item.type]: nextValue }))
                  }}
                />
                <span className="text-sm text-slate-600">minutes per chapter</span>
              </div>
            ) : (
              <div className="text-sm text-slate-600">{formatCount(item.minutesPerChapter)} minutes per chapter</div>
            )}
          </Surface>
        ))}
      </div>
    </div>
  )
}

export function estimatedReadingTimeRows(data?: BookAnalyticsDto, settings: Record<string, number> = {}) {
  return getEstimatedReadingRows(data?.progress.typeVolumes ?? [], settings).map((item) => [
    item.type,
    formatChapterCount(item.currentChapters),
    formatCount(item.minutesPerChapter),
    formatHours(item.hours),
  ])
}

function getMinutesByType(
  items: NonNullable<BookAnalyticsDto['progress']>['typeVolumes'],
  settings: Record<string, number>,
) {
  return Object.fromEntries(items.map((item) => [item.type, normalizeMinutes(settings[item.type] ?? 5)]))
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
