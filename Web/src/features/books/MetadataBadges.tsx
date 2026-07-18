import { useId, type ReactNode } from 'react'

type MetadataSummaryProps = {
  primary: string
  alternatives: string[]
  totalCount?: number
  countNoun: string
  primaryClassName?: string
}

export function MetadataSummary({
  primary,
  alternatives,
  totalCount = alternatives.length,
  countNoun,
  primaryClassName = 'block min-w-0 truncate',
}: MetadataSummaryProps) {
  const tooltip = buildAlternativeTooltip(alternatives, totalCount, countNoun)

  return (
    <div className="flex min-w-0 items-center gap-2">
      <span className={primaryClassName}>{primary}</span>
      {totalCount > 0 ? (
        <span
          aria-label={`${totalCount} ${pluralize(countNoun, totalCount)}`}
          className="book-table-badge shrink-0 rounded px-2 py-1 text-xs font-medium"
          title={tooltip}
        >
          +{totalCount}
        </span>
      ) : null}
    </div>
  )
}

export function DescribedMetadataPills({
  values,
  descriptions = {},
  maxVisible = 3,
  empty = '-',
  variant = 'table',
}: {
  values: string[]
  descriptions?: Record<string, string | null | undefined>
  maxVisible?: number
  empty?: ReactNode
  variant?: 'table' | 'detail'
}) {
  if (!values.length) {
    return empty
  }

  const visibleValues = values.slice(0, maxVisible)
  const hiddenValues = values.slice(maxVisible)

  return (
    <div className={`flex flex-wrap ${variant === 'detail' ? 'gap-2' : 'gap-1'}`}>
      {visibleValues.map((value) => (
        <DescribedMetadataPill
          description={descriptions[value]}
          key={value}
          value={value}
          variant={variant}
        />
      ))}
      {hiddenValues.length > 0 ? (
        <span
          aria-label={`${hiddenValues.length} more: ${hiddenValues.join(', ')}`}
          className={variant === 'detail'
            ? 'rounded bg-slate-100 px-2 py-1 text-xs font-medium text-slate-700'
            : 'book-table-badge rounded px-2 py-1 text-xs font-medium'}
          title={hiddenValues.join('\n')}
        >
          +{hiddenValues.length} more
        </span>
      ) : null}
    </div>
  )
}

function DescribedMetadataPill({
  value,
  description,
  variant,
}: {
  value: string
  description?: string | null
  variant: 'table' | 'detail'
}) {
  const tooltipId = useId()

  if (variant === 'detail') {
    return (
      <span className="group relative">
        <span
          aria-describedby={description ? tooltipId : undefined}
          className="rounded bg-slate-100 px-2 py-1 text-xs font-medium text-slate-700"
          tabIndex={description ? 0 : undefined}
        >
          {value}
        </span>
        {description ? (
          <span
            className="ui-chart-tooltip pointer-events-none absolute bottom-full left-1/2 z-10 mb-2 hidden w-56 -translate-x-1/2 text-xs font-normal leading-5 group-hover:block group-focus-within:block"
            id={tooltipId}
            role="tooltip"
          >
            {description}
          </span>
        ) : null}
      </span>
    )
  }

  return (
    <span
      aria-label={description ? `${value}: ${description}` : undefined}
      className="book-table-badge rounded px-2 py-1 text-xs"
      title={description ?? undefined}
    >
      {value}
    </span>
  )
}

function buildAlternativeTooltip(alternatives: string[], totalCount: number, countNoun: string) {
  if (!alternatives.length) {
    return `${totalCount} ${pluralize(countNoun, totalCount)}`
  }

  const hiddenCount = Math.max(0, totalCount - alternatives.length)
  return [
    `${capitalize(pluralize(countNoun, totalCount))}:`,
    ...alternatives,
    hiddenCount ? `+${hiddenCount} more` : null,
  ].filter((line): line is string => Boolean(line)).join('\n')
}

function pluralize(value: string, count: number) {
  return count === 1 ? value : `${value}s`
}

function capitalize(value: string) {
  return value.charAt(0).toLocaleUpperCase() + value.slice(1)
}
