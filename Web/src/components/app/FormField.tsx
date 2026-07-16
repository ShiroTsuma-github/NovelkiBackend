import type { ReactNode } from 'react'
import { buttonVariants, controlClass } from './DesignSystem'

type FormFieldProps = {
  label: string
  error?: string
  children: ReactNode
}

export function FormField({ label, error, children }: FormFieldProps) {
  return (
    <div className="ui-form-field">
      <span className="ui-field-label">{label}</span>
      <div className="relative dark-field">
        {children}
        {error ? (
          <span
            aria-live="polite"
            className="ui-field-error"
            role="alert"
          >
            {error}
          </span>
        ) : null}
      </div>
    </div>
  )
}

export const inputClass = controlClass

export const buttonClass = buttonVariants.primary

export const secondaryButtonClass = buttonVariants.secondary
