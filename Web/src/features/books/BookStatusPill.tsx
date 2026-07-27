export function BookStatusPill({
  className = '',
  status,
  variant = 'detail',
}: {
  className?: string
  status: string
  variant?: 'card' | 'detail'
}) {
  const normalized = status.trim().toLowerCase()
  const tone = normalized === 'reading'
    ? 'reading'
    : normalized === 'completed'
      ? 'completed'
      : normalized === 'plan to read'
        ? 'planned'
        : normalized === 'on hold'
          ? 'paused'
          : normalized === 'dropped'
            ? 'dropped'
            : 'neutral'

  return (
    <span
      aria-label={`Book status: ${status}`}
      className={[
        'book-details-status',
        `book-details-status--${tone}`,
        variant === 'card' ? 'book-details-status--card' : 'self-start',
        className,
      ].filter(Boolean).join(' ')}
    >
      <span aria-hidden="true" className="book-details-status__dot" />
      <span aria-hidden="true" className="book-details-status__label">Status</span>
      <span className="book-details-status__value" title={variant === 'card' ? status : undefined}>{status}</span>
    </span>
  )
}
