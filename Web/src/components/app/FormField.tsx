import type { ReactNode } from 'react'

type FormFieldProps = {
  label: string
  error?: string
  children: ReactNode
}

export function FormField({ label, error, children }: FormFieldProps) {
  return (
    <div className="grid gap-1.5 text-sm font-medium text-slate-200">
      <span>{label}</span>
      <div className="relative dark-field">
        {children}
        {error ? (
          <span
            aria-live="polite"
            className="pointer-events-none absolute left-0 top-full z-30 mt-2 max-w-full rounded-md border border-rose-300 bg-rose-50 px-2.5 py-1.5 text-xs font-medium text-rose-700 shadow-lg"
            role="alert"
          >
            {error}
          </span>
        ) : null}
      </div>
    </div>
  )
}

export const inputClass =
  'w-full min-h-10 rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-slate-100 outline-none transition placeholder:text-slate-500 focus:border-cyan-400 focus:ring-2 focus:ring-cyan-400/20 disabled:cursor-not-allowed disabled:bg-slate-900 disabled:text-slate-500'

export const buttonClass =
  'inline-flex min-h-10 items-center justify-center gap-2 rounded-md bg-cyan-500 px-4 py-2 text-sm font-semibold text-slate-950 transition hover:bg-cyan-400 disabled:cursor-not-allowed disabled:bg-slate-700 disabled:text-slate-400'

export const secondaryButtonClass =
  'inline-flex min-h-10 items-center justify-center gap-2 rounded-md border border-slate-700 bg-slate-900 px-4 py-2 text-sm font-semibold text-slate-200 transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:text-slate-500'
