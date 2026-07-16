import { useMutation } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { useRef, useState } from 'react'
import { api } from '@/api/client'
import { HttpError } from '@/api/http'
import { buttonClass, inputClass } from '@/components/app/FormField'
import { AuthFrame } from './LoginPage'

type RegisterFormValues = {
  username: string
  email: string
  password: string
}

type RegisterFormErrors = Partial<Record<keyof RegisterFormValues | 'form', string>>

const initialValues: RegisterFormValues = {
  username: '',
  email: '',
  password: '',
}

const passwordRules = [
  { id: 'length', label: 'At least 8 characters', isMet: (value: string) => value.length >= 8 },
  { id: 'uppercase', label: 'One uppercase letter', isMet: (value: string) => /[A-Z]/.test(value) },
  { id: 'lowercase', label: 'One lowercase letter', isMet: (value: string) => /[a-z]/.test(value) },
  { id: 'number', label: 'One number', isMet: (value: string) => /[0-9]/.test(value) },
  { id: 'symbol', label: 'One non-alphanumeric character', isMet: (value: string) => /[^a-zA-Z0-9]/.test(value) },
] as const

export function RegisterPage() {
  const navigate = useNavigate()
  const [values, setValues] = useState<RegisterFormValues>(initialValues)
  const [errors, setErrors] = useState<RegisterFormErrors>({})
  const [passwordFocused, setPasswordFocused] = useState(false)
  const passwordInputRef = useRef<HTMLInputElement | null>(null)
  const mutation = useMutation({
    mutationFn: api.register,
    onSuccess: () => {
      toast.success('Account created. You can log in now.')
      navigate('/login', { replace: true })
    },
    onError: (error) => {
      if (error instanceof HttpError) {
        const nextErrors = mapApiErrors(error)
        setErrors(nextErrors)
        if (nextErrors.password) {
          passwordInputRef.current?.focus()
        }
        toast.error(nextErrors.form ?? error.apiError.detail)
        return
      }

      setErrors({ form: 'Failed to create account.' })
      toast.error('Failed to create account.')
    },
  })

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const validationErrors = validate(values)
    setErrors(validationErrors)
    if (Object.keys(validationErrors).length) {
      if (validationErrors.password) {
        passwordInputRef.current?.focus()
      }
      return
    }

    mutation.mutate({
      ...values,
      username: values.username.trim(),
      email: values.email.trim(),
    })
  }

  function updateField(field: keyof RegisterFormValues, value: string) {
    setValues((current) => ({ ...current, [field]: value }))
    setErrors((current) => ({ ...current, [field]: undefined, form: undefined }))
  }

  return (
    <AuthFrame title="Register" description="Create a reader account.">
      <form className="grid gap-4" noValidate onSubmit={handleSubmit}>
        <FieldError error={errors.username}>
          <input
            aria-invalid={errors.username ? 'true' : undefined}
            className={inputClass}
            name="username"
            placeholder="Username"
            value={values.username}
            onChange={(event) => updateField('username', event.target.value)}
          />
        </FieldError>
        <FieldError error={errors.email}>
          <input
            aria-invalid={errors.email ? 'true' : undefined}
            className={inputClass}
            name="email"
            placeholder="Email"
            type="email"
            value={values.email}
            onChange={(event) => updateField('email', event.target.value)}
          />
        </FieldError>
        <FieldError>
          <input
            aria-invalid={errors.password ? 'true' : undefined}
            className={inputClass}
            name="password"
            placeholder="Password"
            ref={passwordInputRef}
            type="password"
            value={values.password}
            onChange={(event) => updateField('password', event.target.value)}
            onBlur={() => setPasswordFocused(false)}
            onFocus={() => setPasswordFocused(true)}
          />
        </FieldError>
        {passwordFocused ? (
          <ul className="grid gap-1 text-xs text-slate-500">
            {passwordRules.map((rule) => {
              const met = rule.isMet(values.password)
              return (
                <li aria-label={`${met ? 'Met' : 'Missing'}: ${rule.label}`} className={met ? 'text-emerald-500' : 'text-slate-500'} key={rule.id}>
                  {met ? '[x]' : '[ ]'} {rule.label}
                </li>
              )
            })}
          </ul>
        ) : null}
        {errors.form ? <p className="text-sm text-red-600">{errors.form}</p> : null}
        <button className={buttonClass} disabled={mutation.isPending} type="submit" onMouseDown={(event) => event.preventDefault()}>
          Register
        </button>
      </form>
      <p className="mt-4 text-sm text-slate-500">
        Already have an account?{' '}
        <Link className="ui-inline-link" to="/login">
          Log in
        </Link>
      </p>
    </AuthFrame>
  )
}

function FieldError({ children, error }: { children: React.ReactNode; error?: string }) {
  return (
    <label className="grid gap-1">
      {children}
      {error ? <span className="text-xs text-red-600">{error}</span> : null}
    </label>
  )
}

function validate(values: RegisterFormValues): RegisterFormErrors {
  const nextErrors: RegisterFormErrors = {}
  const username = values.username.trim()
  const email = values.email.trim()

  if (!username) {
    nextErrors.username = 'Username is required.'
  } else if (username.length < 3) {
    nextErrors.username = 'Username must be at least 3 characters long.'
  } else if (username.length > 32) {
    nextErrors.username = "Username can't be longer than 32 characters."
  }

  if (!email) {
    nextErrors.email = 'Email address is required.'
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
    nextErrors.email = 'A valid email address is required.'
  } else if (email.length > 100) {
    nextErrors.email = "Email can't be longer than 100 characters."
  }

  const failedRules = passwordRules.filter((rule) => !rule.isMet(values.password))
  if (!values.password) {
    nextErrors.password = 'Password is required.'
  } else if (values.password.length > 128) {
    nextErrors.password = "Password can't be longer than 128 characters."
  } else if (failedRules.length) {
    nextErrors.password = failedRules.map((rule) => rule.label).join('; ')
  }

  return nextErrors
}

function mapApiErrors(error: HttpError): RegisterFormErrors {
  const nextErrors: RegisterFormErrors = {}
  const apiErrors = error.apiError.errors ?? {}

  Object.entries(apiErrors).forEach(([property, messages]) => {
    const field = normalizeFieldName(property)
    const message = messages[0]
    if (field && message) {
      nextErrors[field] = message
    }
  })

  if (!Object.keys(nextErrors).length) {
    const detail = error.apiError.detail
    if (/username/i.test(detail)) {
      nextErrors.username = detail
    } else if (/email/i.test(detail)) {
      nextErrors.email = detail
    } else {
      nextErrors.form = detail
    }
  }

  return nextErrors
}

function normalizeFieldName(property: string): keyof RegisterFormValues | null {
  const normalized = property.trim().toLowerCase()
  if (normalized === 'username') {
    return 'username'
  }

  if (normalized === 'email') {
    return 'email'
  }

  if (normalized === 'password') {
    return 'password'
  }

  return null
}
