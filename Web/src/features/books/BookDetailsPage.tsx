import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Edit, ExternalLink, Trash2 } from 'lucide-react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { api } from '@/api/client'
import { HttpError } from '@/api/http'
import {
  buttonClass,
  secondaryButtonClass,
} from '@/components/app/FormField'
import { formatProgress } from './BooksPage'
import { ProgressDialog } from './ProgressDialog'

export function BookDetailsPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const bookQuery = useQuery({
    queryKey: ['book', id],
    queryFn: () => api.getBook(id!),
    enabled: Boolean(id),
  })
  const deleteMutation = useMutation({
    mutationFn: () => api.deleteBook(id!),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['books'] })
      toast.success('Książka usunięta.')
      navigate('/books', { replace: true })
    },
    onError: (error) => {
      toast.error(error instanceof HttpError ? error.apiError.detail : 'Nie udało się usunąć książki.')
    },
  })

  if (bookQuery.isLoading) {
    return <div className="rounded-lg border border-slate-200 bg-white p-6 text-slate-500">Ładowanie...</div>
  }

  if (!bookQuery.data) {
    return <div className="rounded-lg border border-slate-200 bg-white p-6 text-slate-500">Nie znaleziono książki.</div>
  }

  const book = bookQuery.data

  return (
    <div className="grid gap-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Link className={secondaryButtonClass} to="/books">
          <ArrowLeft className="h-4 w-4" />
          Lista
        </Link>
        <div className="flex flex-wrap gap-2">
          <ProgressDialog book={book} />
          <Link className={buttonClass} to={`/books/${book.id}/edit`}>
            <Edit className="h-4 w-4" />
            Edytuj
          </Link>
          <button
            className="inline-flex min-h-10 items-center justify-center gap-2 rounded-md bg-red-600 px-4 py-2 text-sm font-semibold text-white hover:bg-red-700 disabled:bg-red-300"
            disabled={deleteMutation.isPending}
            type="button"
            onClick={() => deleteMutation.mutate()}
          >
            <Trash2 className="h-4 w-4" />
            Usuń
          </button>
        </div>
      </div>

      <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <div className="grid gap-1">
          <h1 className="text-3xl font-semibold text-slate-950">{book.primaryTitle}</h1>
          <p className="text-sm text-slate-500">{book.author ?? 'Autor nieznany'}</p>
        </div>
        <div className="mt-5 grid gap-4 md:grid-cols-4">
          <Metric label="Status" value={book.status} />
          <Metric label="Typ" value={book.contentType} />
          <Metric label="Progres" value={formatProgress(book)} />
          <Metric label="Ocena" value={book.rating?.toString() ?? '-'} />
        </div>
      </section>

      <section className="grid gap-4 md:grid-cols-2">
        <Panel title="Tytuły alternatywne">
          <Pills values={book.alternativeTitles} empty="Brak aliasów." />
        </Panel>
        <Panel title="Tagi i gatunki">
          <div className="grid gap-3">
            <Pills values={book.tags} empty="Brak tagów." />
            <Pills values={book.genres} empty="Brak gatunków." />
          </div>
        </Panel>
        <Panel title="Linki">
          <div className="grid gap-2">
            {book.links.length ? book.links.map((link) => (
              <a className="flex items-center justify-between rounded-md border border-slate-200 px-3 py-2 text-sm hover:bg-slate-50" href={link.url} key={link.id} rel="noreferrer" target="_blank">
                <span>{link.label || link.sourceType}</span>
                <ExternalLink className="h-4 w-4 text-slate-400" />
              </a>
            )) : <p className="text-sm text-slate-500">Brak linków.</p>}
          </div>
        </Panel>
        <Panel title="Notatki">
          <div className="grid gap-3 text-sm text-slate-700">
            <p>{book.comment || 'Brak komentarza.'}</p>
            <p>{book.notes || 'Brak notatek.'}</p>
          </div>
        </Panel>
      </section>
    </div>
  )
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border border-slate-200 bg-slate-50 p-3">
      <div className="text-xs uppercase tracking-wide text-slate-500">{label}</div>
      <div className="mt-1 font-semibold text-slate-950">{value}</div>
    </div>
  )
}

function Panel({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
      <h2 className="mb-3 text-base font-semibold text-slate-950">{title}</h2>
      {children}
    </section>
  )
}

function Pills({ values, empty }: { values: string[]; empty: string }) {
  if (!values.length) {
    return <p className="text-sm text-slate-500">{empty}</p>
  }
  return (
    <div className="flex flex-wrap gap-2">
      {values.map((value) => (
        <span className="rounded bg-slate-100 px-2 py-1 text-xs font-medium text-slate-700" key={value}>{value}</span>
      ))}
    </div>
  )
}
