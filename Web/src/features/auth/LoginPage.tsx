import { useMutation } from '@tanstack/react-query'
import { BookOpen } from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { api } from '@/api/client'
import { HttpError } from '@/api/http'
import { Surface } from '@/components/app/DesignSystem'
import { buttonClass, inputClass } from '@/components/app/FormField'
import { useAuth } from './AuthProvider'

export function LoginPage() {
  const { setSession } = useAuth()
  const navigate = useNavigate()
  const mutation = useMutation({
    mutationFn: api.login,
    onSuccess: (response) => {
      setSession(response)
      navigate('/books', { replace: true })
    },
    onError: (error) => {
      toast.error(error instanceof HttpError ? error.apiError.detail : 'Failed to log in.')
    },
  })

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const formData = new FormData(event.currentTarget)
    const identifier = String(formData.get('identifier') ?? '').trim()
    mutation.mutate({
      email: identifier.includes('@') ? identifier : null,
      username: identifier.includes('@') ? null : identifier,
      password: String(formData.get('password') ?? ''),
    })
  }

  return (
    <AuthFrame title="Log in" description="Enter your library.">
      <form className="grid gap-4" onSubmit={handleSubmit}>
        <input className={inputClass} name="identifier" placeholder="Email or username" type="text" />
        <input className={inputClass} name="password" placeholder="Password" type="password" />
        <button className={buttonClass} disabled={mutation.isPending} type="submit">
          Log in
        </button>
      </form>
      <p className="mt-4 text-sm text-slate-500">
        Do not have an account?{' '}
        <Link className="ui-inline-link" to="/register">
          Register
        </Link>
      </p>
    </AuthFrame>
  )
}

type AuthFrameProps = {
  title: string
  description: string
  children: React.ReactNode
}

export function AuthFrame({ title, description, children }: AuthFrameProps) {
  return (
    <main className="auth-frame">
      <Surface className="auth-panel">
        <div className="mb-6 flex items-center gap-3">
          <div className="auth-brand-mark">
            <BookOpen className="h-5 w-5" />
          </div>
          <div>
            <div className="ui-eyebrow">Personal library</div>
            <h1 className="text-xl font-semibold text-slate-950">{title}</h1>
            <p className="ui-panel-description">{description}</p>
          </div>
        </div>
        {children}
      </Surface>
    </main>
  )
}
