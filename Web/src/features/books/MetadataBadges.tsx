import { useEffect, useId, useRef, useState, type ReactNode } from 'react'
import { createPortal } from 'react-dom'

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
      <span className={primaryClassName} title={primary}>{primary}</span>
      {totalCount > 0 ? (
        <MetadataTooltip
          ariaLabel={`${totalCount} ${pluralize(countNoun, totalCount)}`}
          content={tooltip}
          triggerClassName="book-table-badge shrink-0 rounded px-2 py-1 text-xs font-medium"
        >
          +{totalCount}
        </MetadataTooltip>
      ) : null}
    </div>
  )
}

export function DescribedMetadataPills({
  values,
  descriptions = {},
  maxVisible = 3,
  totalCount,
  empty = '-',
  variant = 'table',
}: {
  values: string[]
  descriptions?: Record<string, string | null | undefined>
  maxVisible?: number
  totalCount?: number
  empty?: ReactNode
  variant?: 'table' | 'detail'
}) {
  if (!values.length) {
    return empty
  }

  const visibleValues = values.slice(0, maxVisible)
  const hiddenValues = values.slice(maxVisible)
  const resolvedTotalCount = Math.max(totalCount ?? values.length, values.length)
  const hiddenCount = Math.max(0, resolvedTotalCount - visibleValues.length)
  const unlistedCount = Math.max(0, hiddenCount - hiddenValues.length)
  const hiddenTooltip = [
    ...hiddenValues,
    unlistedCount > 0 ? `+${unlistedCount} more` : null,
  ].filter((line): line is string => Boolean(line)).join('\n')

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
      {hiddenCount > 0 ? (
        <MetadataTooltip
          ariaLabel={`${hiddenCount} more${hiddenValues.length ? `: ${hiddenValues.join(', ')}` : ''}`}
          content={hiddenTooltip}
          triggerClassName={variant === 'detail'
            ? 'rounded bg-slate-100 px-2 py-1 text-xs font-medium text-slate-700'
            : 'book-table-badge rounded px-2 py-1 text-xs font-medium'}
        >
          +{hiddenCount} more
        </MetadataTooltip>
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
  const triggerClassName = variant === 'detail'
    ? 'rounded bg-slate-100 px-2 py-1 text-xs font-medium text-slate-700'
    : 'book-table-badge rounded px-2 py-1 text-xs'

  if (!description) {
    const plainPill = <span className={triggerClassName}>{value}</span>
    return variant === 'detail'
      ? <span className="group relative">{plainPill}</span>
      : plainPill
  }

  const pill = (
    <MetadataTooltip
      ariaLabel={`${value}: ${description}`}
      content={description}
      triggerClassName={triggerClassName}
    >
      {value}
    </MetadataTooltip>
  )

  return variant === 'detail'
    ? <span className="group relative">{pill}</span>
    : pill
}

function MetadataTooltip({
  ariaLabel,
  children,
  content,
  triggerClassName,
}: {
  ariaLabel: string
  children: ReactNode
  content: string
  triggerClassName: string
}) {
  const tooltipId = useId()
  const triggerRef = useRef<HTMLSpanElement | null>(null)
  const [isOpen, setIsOpen] = useState(false)
  const [position, setPosition] = useState({ left: 0, top: 0, below: false })

  function updatePosition() {
    const trigger = triggerRef.current
    if (!trigger) {
      return
    }

    const bounds = trigger.getBoundingClientRect()
    const availableWidth = Math.max(0, window.innerWidth - 24)
    const halfTooltipWidth = Math.min(144, availableWidth / 2)
    const centeredLeft = bounds.left + bounds.width / 2
    const left = Math.min(
      window.innerWidth - 12 - halfTooltipWidth,
      Math.max(12 + halfTooltipWidth, centeredLeft),
    )

    setPosition({
      left,
      top: bounds.top < 140 ? bounds.bottom : bounds.top,
      below: bounds.top < 140,
    })
  }

  function showTooltip() {
    updatePosition()
    setIsOpen(true)
  }

  useEffect(() => {
    if (!isOpen) {
      return
    }

    window.addEventListener('resize', updatePosition)
    window.addEventListener('scroll', updatePosition, true)
    return () => {
      window.removeEventListener('resize', updatePosition)
      window.removeEventListener('scroll', updatePosition, true)
    }
  }, [isOpen])

  return (
    <>
      <span
        aria-describedby={isOpen ? tooltipId : undefined}
        aria-label={ariaLabel}
        className={triggerClassName}
        ref={triggerRef}
        tabIndex={0}
        onBlur={() => setIsOpen(false)}
        onFocus={showTooltip}
        onMouseEnter={showTooltip}
        onMouseLeave={() => setIsOpen(false)}
      >
        {children}
      </span>
      {isOpen && typeof document !== 'undefined' ? createPortal(
        <span
          className="metadata-tooltip ui-chart-tooltip pointer-events-none fixed z-[80] w-72 max-w-[calc(100vw-1.5rem)] whitespace-pre-line text-xs font-normal leading-5 shadow-lg"
          id={tooltipId}
          role="tooltip"
          style={{
            left: position.left,
            top: position.top,
            transform: position.below
              ? 'translate(-50%, 0.5rem)'
              : 'translate(-50%, calc(-100% - 0.5rem))',
          }}
        >
          {content}
        </span>,
        document.body,
      ) : null}
    </>
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
