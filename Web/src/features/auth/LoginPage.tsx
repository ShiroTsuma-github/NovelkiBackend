import { useMutation } from '@tanstack/react-query'
import { BookOpen } from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { api } from '@/api/client'
import { HttpError } from '@/api/http'
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
        <Link className="font-semibold text-slate-950 underline" to="/register">
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
    <main className="flex min-h-screen items-center justify-center bg-slate-50 px-4 py-10">
      <section className="w-full max-w-md rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <div className="mb-6 flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-md bg-slate-950 text-white">
            <BookOpen className="h-5 w-5" />
          </div>
          <div>
            <h1 className="text-xl font-semibold text-slate-950">{title}</h1>
            <p className="text-sm text-slate-500">{description}</p>
          </div>
        </div>
        {children}
      </section>
    </main>
  )
}
