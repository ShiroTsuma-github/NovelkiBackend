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
}: {
  values: string[]
  descriptions?: Record<string, string | null | undefined>
  maxVisible?: number
}) {
  if (!values.length) {
    return '-'
  }

  const visibleValues = values.slice(0, maxVisible)
  const hiddenValues = values.slice(maxVisible)

  return (
    <div className="flex flex-wrap gap-1">
      {visibleValues.map((value) => (
        <span
          aria-label={descriptions[value] ? `${value}: ${descriptions[value]}` : undefined}
          className="book-table-badge rounded px-2 py-1 text-xs"
          key={value}
          title={descriptions[value] ?? undefined}
        >
          {value}
        </span>
      ))}
      {hiddenValues.length > 0 ? (
        <span
          aria-label={`${hiddenValues.length} more: ${hiddenValues.join(', ')}`}
          className="book-table-badge rounded px-2 py-1 text-xs font-medium"
          title={hiddenValues.join(', ')}
        >
          +{hiddenValues.length} more
        </span>
      ) : null}
    </div>
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
