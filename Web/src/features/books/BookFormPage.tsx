import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Save } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { api } from '@/api/client'
import { HttpError } from '@/api/http'
import {
  FormField,
  buttonClass,
  inputClass,
  secondaryButtonClass,
} from '@/components/app/FormField'
import {
  bookFormSchema,
  defaultBookFormValues,
  toBookMutationRequest,
  type BookFormValues,
} from './bookFormSchema'

type BookFormPageProps = {
  mode: 'create' | 'edit'
  admin?: boolean
}

export function BookFormPage({ mode, admin = false }: BookFormPageProps) {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const bookQuery = useQuery({
    queryKey: [admin ? 'adminBook' : 'book', id],
    queryFn: () => admin ? api.getAdminBook(id!) : api.getBook(id!),
    enabled: mode === 'edit' && Boolean(id),
  })
  const typesQuery = useQuery({ queryKey: ['types'], queryFn: api.getTypes, staleTime: 300_000 })
  const statusesQuery = useQuery({ queryKey: ['statuses'], queryFn: api.getStatuses, staleTime: 300_000 })
  const genresQuery = useQuery({ queryKey: ['genres'], queryFn: api.getGenres, staleTime: 300_000 })

  const form = useForm<BookFormValues>({
    resolver: zodResolver(bookFormSchema),
    defaultValues: defaultBookFormValues(),
  })
  const [authorSuggestionsOpen, setAuthorSuggestionsOpen] = useState(false)
  const authorName = form.watch('authorName') ?? ''
  const authorId = form.watch('authorId') ?? ''
  const authorSuggestionsQuery = useQuery({
    queryKey: ['authorSuggestions', authorName.trim()],
    queryFn: () => api.searchAuthors(authorName.trim(), 8),
    enabled: authorName.trim().length >= 2,
    staleTime: 30_000,
  })

  useEffect(() => {
    if (mode === 'edit' && bookQuery.data && typesQuery.data && statusesQuery.data && genresQuery.data) {
      const matchingType = typesQuery.data.data.find((item) => item.name === bookQuery.data.contentType)
      const matchingStatus = statusesQuery.data.data.find((item) => item.name === bookQuery.data.status)
      const matchingGenres = genresQuery.data.data
        .filter((item) => bookQuery.data.genres.includes(item.name))
        .map((item) => item.id)
      form.reset({
        ...defaultBookFormValues(bookQuery.data),
        contentTypeId: matchingType?.id ?? '',
        statusId: matchingStatus?.id ?? '',
        genreIds: matchingGenres,
      })
    }
  }, [bookQuery.data, form, genresQuery.data, mode, statusesQuery.data, typesQuery.data])

  useEffect(() => {
    if (mode === 'create' && typesQuery.data && statusesQuery.data) {
      form.setValue('contentTypeId', typesQuery.data.data[0]?.id ?? '')
      form.setValue('statusId', statusesQuery.data.data[0]?.id ?? '')
    }
  }, [form, mode, statusesQuery.data, typesQuery.data])

  async function resolveAuthor(values: BookFormValues): Promise<BookFormValues> {
    const name = values.authorName?.trim()
    if (!name || values.authorId) {
      return values
    }

    const authors = await api.searchAuthors(name, 10)
    const exactAuthor = authors.find((author) => {
      const normalizedName = normalizeAuthorName(name)
      return (
        normalizeAuthorName(author.primaryName) === normalizedName ||
        author.otherNames.some((otherName) => normalizeAuthorName(otherName) === normalizedName)
      )
    })

    return exactAuthor
      ? { ...values, authorId: exactAuthor.id, authorName: exactAuthor.primaryName }
      : values
  }

  function selectAuthor(author: { id: string; primaryName: string }) {
    form.setValue('authorId', author.id, { shouldDirty: true, shouldValidate: true })
    form.setValue('authorName', author.primaryName, { shouldDirty: true, shouldValidate: true })
    setAuthorSuggestionsOpen(false)
  }

  const mutation = useMutation({
    mutationFn: async (values: BookFormValues) => {
      const resolvedValues = await resolveAuthor(values)
      const request = toBookMutationRequest(resolvedValues)
      if (mode === 'create') {
        const duplicateQuery = await api.getBooks({
          take: 10,
          skip: 0,
          query: `title:"${resolvedValues.primaryTitle.replaceAll('"', '')}"`,
        })
        if (duplicateQuery.total > 0) {
          throw new Error('Książka o takim tytule już istnieje.')
        }
        return api.createBook(request)
      }
      await (admin ? api.updateAdminBook(id!, request) : api.updateBook(id!, request))
      return { id: id! }
    },
    onSuccess: async (response) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['books'] }),
        queryClient.invalidateQueries({ queryKey: ['adminBooks'] }),
        queryClient.invalidateQueries({ queryKey: ['book', response.id] }),
        queryClient.invalidateQueries({ queryKey: ['adminBook', response.id] }),
      ])
      toast.success(mode === 'create' ? 'Książka dodana.' : 'Książka zaktualizowana.')
      navigate(admin ? '/admin' : `/books/${response.id}`, { replace: true })
    },
    onError: (error) => {
      toast.error(error instanceof HttpError ? error.apiError.detail : error.message)
    },
  })

  const errors = form.formState.errors
  const isLoadingDictionaries = typesQuery.isLoading || statusesQuery.isLoading || genresQuery.isLoading
  const isLoadingBook = mode === 'edit' && bookQuery.isLoading

  if (isLoadingDictionaries || isLoadingBook) {
    return <div className="rounded-lg border border-slate-200 bg-white p-6 text-slate-500">Ładowanie formularza...</div>
  }

  return (
    <form className="grid gap-5" onSubmit={form.handleSubmit((values) => mutation.mutate(values))}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-slate-950">{admin ? 'Edycja admina' : mode === 'create' ? 'Dodaj książkę' : 'Edytuj książkę'}</h1>
          <p className="text-sm text-slate-500">Dane zostaną zapisane bezpośrednio w backendzie.</p>
        </div>
        <div className="flex gap-2">
          <Link className={secondaryButtonClass} to={admin ? '/admin' : mode === 'edit' && id ? `/books/${id}` : '/books'}>
            <ArrowLeft className="h-4 w-4" />
            Wróć
          </Link>
          <button className={buttonClass} disabled={mutation.isPending} type="submit">
            <Save className="h-4 w-4" />
            Zapisz
          </button>
        </div>
      </div>

      <section className="grid gap-4 rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-base font-semibold text-slate-950">Podstawowe</h2>
        <div className="grid gap-4 md:grid-cols-2">
          <FormField error={errors.primaryTitle?.message} label="Tytuł główny">
            <input className={inputClass} {...form.register('primaryTitle')} />
          </FormField>
          <FormField error={errors.authorName?.message} label="Autor">
            <div className="relative">
              <input
                className={`${inputClass} w-full`}
                value={authorName}
                onBlur={() => window.setTimeout(() => setAuthorSuggestionsOpen(false), 120)}
                onChange={(event) => {
                  form.setValue('authorName', event.target.value, { shouldDirty: true, shouldValidate: true })
                  form.setValue('authorId', '', { shouldDirty: true, shouldValidate: true })
                  setAuthorSuggestionsOpen(true)
                }}
                onFocus={() => setAuthorSuggestionsOpen(true)}
              />
              {authorSuggestionsOpen && authorName.trim().length >= 2 ? (
                <div className="absolute z-20 mt-1 max-h-64 w-full overflow-auto rounded-md border border-slate-700 bg-slate-950 shadow-xl">
                  {authorSuggestionsQuery.isLoading ? (
                    <div className="px-3 py-2 text-sm text-slate-400">Szukam autorów...</div>
                  ) : null}
                  {authorSuggestionsQuery.data?.map((author) => (
                    <button
                      className="grid w-full gap-0.5 px-3 py-2 text-left text-sm text-slate-100 hover:bg-slate-800"
                      key={author.id}
                      type="button"
                      onMouseDown={(event) => event.preventDefault()}
                      onClick={() => selectAuthor(author)}
                    >
                      <span className="font-medium">{author.primaryName}</span>
                      {author.otherNames.length ? (
                        <span className="text-xs text-slate-400">{author.otherNames.slice(0, 3).join(', ')}</span>
                      ) : null}
                    </button>
                  ))}
                  {!authorSuggestionsQuery.isLoading && authorSuggestionsQuery.data?.length === 0 ? (
                    <div className="px-3 py-2 text-sm text-slate-400">Brak autora. Zostanie utworzony nowy.</div>
                  ) : null}
                </div>
              ) : null}
              {authorId ? (
                <div className="mt-1 text-xs text-cyan-400">Wybrano istniejącego autora.</div>
              ) : authorName.trim() ? (
                <div className="mt-1 text-xs text-slate-400">Jeśli nie wybierzesz sugestii, backend utworzy nowego autora.</div>
              ) : null}
            </div>
          </FormField>
          <FormField error={errors.contentTypeId?.message} label="Typ">
            <select className={inputClass} {...form.register('contentTypeId')}>
              {typesQuery.data?.data.map((type) => <option key={type.id} value={type.id}>{type.name}</option>)}
            </select>
          </FormField>
          <FormField error={errors.statusId?.message} label="Status">
            <select className={inputClass} {...form.register('statusId')}>
              {statusesQuery.data?.data.map((status) => <option key={status.id} value={status.id}>{status.name}</option>)}
            </select>
          </FormField>
        </div>
      </section>

      <section className="grid gap-4 rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-base font-semibold text-slate-950">Klasyfikacja</h2>
        <div className="grid gap-4 md:grid-cols-2">
          <FormField label="Gatunki">
            <select className={`${inputClass} min-h-32`} multiple {...form.register('genreIds')}>
              {genresQuery.data?.data.map((genre) => <option key={genre.id} value={genre.id}>{genre.name}</option>)}
            </select>
          </FormField>
          <FormField error={errors.tagsText?.message} label="Tagi, po przecinku">
            <textarea className={`${inputClass} min-h-32`} {...form.register('tagsText')} />
          </FormField>
        </div>
      </section>

      <section className="grid gap-4 rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-base font-semibold text-slate-950">Postęp i ocena</h2>
        <div className="grid gap-4 md:grid-cols-5">
          <FormField error={errors.currentChapterNumber?.message} label="Aktualny rozdział">
            <input className={inputClass} type="number" step="0.1" {...form.register('currentChapterNumber')} />
          </FormField>
          <FormField error={errors.currentChapterLabel?.message} label="Etykieta">
            <input className={inputClass} {...form.register('currentChapterLabel')} />
          </FormField>
          <FormField error={errors.totalChapters?.message} label="Razem rozdziałów">
            <input className={inputClass} type="number" step="0.1" {...form.register('totalChapters')} />
          </FormField>
          <FormField error={errors.rating?.message} label="Ocena 1-10">
            <input className={inputClass} type="number" {...form.register('rating')} />
          </FormField>
          <FormField error={errors.priority?.message} label="Priorytet 1-5">
            <input className={inputClass} type="number" {...form.register('priority')} />
          </FormField>
        </div>
      </section>

      <section className="grid gap-4 rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-base font-semibold text-slate-950">Dodatkowe</h2>
        <div className="grid gap-4 md:grid-cols-2">
          <FormField error={errors.alternativeTitlesText?.message} label="Alternatywne tytuły, każdy w nowej linii">
            <textarea className={`${inputClass} min-h-32`} {...form.register('alternativeTitlesText')} />
          </FormField>
          <FormField error={errors.linksText?.message} label="Linki, każdy w nowej linii">
            <textarea className={`${inputClass} min-h-32`} {...form.register('linksText')} />
          </FormField>
          <FormField error={errors.comment?.message} label="Komentarz">
            <textarea className={`${inputClass} min-h-32`} {...form.register('comment')} />
          </FormField>
          <FormField error={errors.notes?.message} label="Notatki">
            <textarea className={`${inputClass} min-h-32`} {...form.register('notes')} />
          </FormField>
          <FormField error={errors.description?.message} label="Opis">
            <textarea className={`${inputClass} min-h-32 md:col-span-2`} {...form.register('description')} />
          </FormField>
        </div>
      </section>
    </form>
  )
}

function normalizeAuthorName(value: string) {
  return value.trim().toLocaleUpperCase()
}
