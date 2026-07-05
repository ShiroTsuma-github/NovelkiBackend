import { useMutation } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { api } from '@/api/client'
import { HttpError } from '@/api/http'
import { buttonClass, inputClass } from '@/components/app/FormField'
import { AuthFrame } from './LoginPage'

export function RegisterPage() {
  const navigate = useNavigate()
  const mutation = useMutation({
    mutationFn: api.register,
    onSuccess: () => {
      toast.success('Konto utworzone. Możesz się zalogować.')
      navigate('/login', { replace: true })
    },
    onError: (error) => {
      toast.error(error instanceof HttpError ? error.apiError.detail : 'Nie udało się utworzyć konta.')
    },
  })

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const formData = new FormData(event.currentTarget)
    mutation.mutate({
      username: String(formData.get('username') ?? ''),
      email: String(formData.get('email') ?? ''),
      password: String(formData.get('password') ?? ''),
    })
  }

  return (
    <AuthFrame title="Rejestracja" description="Utwórz konto czytelnika.">
      <form className="grid gap-4" onSubmit={handleSubmit}>
        <input className={inputClass} name="username" placeholder="Nazwa użytkownika" />
        <input className={inputClass} name="email" placeholder="Email" type="email" />
        <input className={inputClass} name="password" placeholder="Hasło" type="password" />
        <button className={buttonClass} disabled={mutation.isPending} type="submit">
          Zarejestruj
        </button>
      </form>
      <p className="mt-4 text-sm text-slate-500">
        Masz konto?{' '}
        <Link className="font-semibold text-slate-950 underline" to="/login">
          Zaloguj się
        </Link>
      </p>
    </AuthFrame>
  )
}
