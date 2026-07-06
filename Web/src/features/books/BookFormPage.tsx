import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Save, X } from 'lucide-react'
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
  const selectedGenreIds = form.watch('genreIds')
  const selectedTags = splitComma(form.watch('tagsText'))
  const selectedType = typesQuery.data?.data.find((type) => type.id === form.watch('contentTypeId'))
  const selectedStatus = statusesQuery.data?.data.find((status) => status.id === form.watch('statusId'))
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

  function setSelectedGenreIds(value: string[]) {
    form.setValue('genreIds', value, { shouldDirty: true, shouldValidate: true })
  }

  function setSelectedTags(value: string[]) {
    form.setValue('tagsText', value.join(', '), { shouldDirty: true, shouldValidate: true })
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
          throw new Error('A book with this title already exists.')
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
      toast.success(mode === 'create' ? 'Book added.' : 'Book updated.')
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
    return <div className="rounded-lg border border-slate-200 bg-white p-6 text-slate-500">Loading form...</div>
  }

  return (
    <form className="grid gap-5" onSubmit={form.handleSubmit((values) => mutation.mutate(values))}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-slate-950">{admin ? 'Admin edit' : mode === 'create' ? 'Add book' : 'Edit book'}</h1>
          <p className="text-sm text-slate-500">Changes are saved directly to the backend.</p>
        </div>
        <div className="flex gap-2">
          <Link className={secondaryButtonClass} to={admin ? '/admin' : mode === 'edit' && id ? `/books/${id}` : '/books'}>
            <ArrowLeft className="h-4 w-4" />
            Back
          </Link>
          <button className={buttonClass} disabled={mutation.isPending} type="submit">
            <Save className="h-4 w-4" />
            Save
          </button>
        </div>
      </div>

      <section className="grid gap-4 rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-base font-semibold text-slate-950">Basics</h2>
        <div className="grid gap-4 md:grid-cols-2">
          <FormField error={errors.primaryTitle?.message} label="Primary title">
            <input className={inputClass} {...form.register('primaryTitle')} />
          </FormField>
          <FormField error={errors.authorName?.message} label="Author">
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
                    <div className="px-3 py-2 text-sm text-slate-400">Searching authors...</div>
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
                    <div className="px-3 py-2 text-sm text-slate-400">No author found. A new one will be created.</div>
                  ) : null}
                </div>
              ) : null}
              {authorId ? (
                <div className="mt-1 text-xs text-cyan-400">Existing author selected.</div>
              ) : authorName.trim() ? (
                <div className="mt-1 text-xs text-slate-400">If you do not choose a suggestion, the backend will create a new author.</div>
              ) : null}
            </div>
          </FormField>
          <FormField error={errors.contentTypeId?.message} label="Type">
            <select className={inputClass} {...form.register('contentTypeId')}>
              {typesQuery.data?.data.map((type) => <option key={type.id} value={type.id}>{type.name}</option>)}
            </select>
            <DescriptionHelp value={selectedType?.description} />
          </FormField>
          <FormField error={errors.statusId?.message} label="Status">
            <select className={inputClass} {...form.register('statusId')}>
              {statusesQuery.data?.data.map((status) => <option key={status.id} value={status.id}>{status.name}</option>)}
            </select>
            <DescriptionHelp value={selectedStatus?.description} />
          </FormField>
        </div>
      </section>

      <section className="grid gap-4 rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-base font-semibold text-slate-950">Classification</h2>
        <div className="grid gap-4 md:grid-cols-2">
          <FormField label="Genres">
            <GenreChipSelect
              options={genresQuery.data?.data ?? []}
              selectedIds={selectedGenreIds}
              onChange={setSelectedGenreIds}
            />
          </FormField>
          <FormField error={errors.tagsText?.message} label="Tags">
            <TagChipSelect selected={selectedTags} onChange={setSelectedTags} />
          </FormField>
        </div>
      </section>

      <section className="grid gap-4 rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-base font-semibold text-slate-950">Progress and rating</h2>
        <div className="grid gap-4 md:grid-cols-5">
          <FormField error={errors.currentChapterNumber?.message} label="Current chapter">
            <input className={inputClass} type="number" step="0.1" {...form.register('currentChapterNumber')} />
          </FormField>
          <FormField error={errors.currentChapterLabel?.message} label="Label">
            <input className={inputClass} {...form.register('currentChapterLabel')} />
          </FormField>
          <FormField error={errors.totalChapters?.message} label="Total chapters">
            <input className={inputClass} type="number" step="0.1" {...form.register('totalChapters')} />
          </FormField>
          <FormField error={errors.rating?.message} label="Rating 1-10">
            <input className={inputClass} type="number" {...form.register('rating')} />
          </FormField>
          <FormField error={errors.priority?.message} label="Priority 1-5">
            <input className={inputClass} type="number" {...form.register('priority')} />
          </FormField>
        </div>
      </section>

      <section className="grid gap-4 rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-base font-semibold text-slate-950">Additional</h2>
        <div className="grid gap-4 md:grid-cols-2">
          <FormField error={errors.alternativeTitlesText?.message} label="Alternative titles, one per line">
            <textarea className={`${inputClass} min-h-32`} {...form.register('alternativeTitlesText')} />
          </FormField>
          <FormField error={errors.linksText?.message} label="Links, one per line">
            <textarea className={`${inputClass} min-h-32`} {...form.register('linksText')} />
          </FormField>
          <FormField error={errors.notes?.message} label="Notes">
            <textarea className={`${inputClass} min-h-32`} {...form.register('notes')} />
          </FormField>
          <FormField error={errors.description?.message} label="Description">
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

function GenreChipSelect({
  options,
  selectedIds,
  onChange,
}: {
  options: { id: string; name: string; description?: string | null }[]
  selectedIds: string[]
  onChange: (value: string[]) => void
}) {
  const [input, setInput] = useState('')
  const selected = options.filter((option) => selectedIds.includes(option.id))
  const suggestions = options
    .filter((option) => !selectedIds.includes(option.id))
    .filter((option) => option.name.toLocaleLowerCase().includes(input.trim().toLocaleLowerCase()))
    .slice(0, 8)

  function addGenre(id: string) {
    onChange([...selectedIds, id])
    setInput('')
  }

  return (
    <ChipBox
      input={input}
      placeholder="Start typing a genre"
      selected={selected.map((option) => ({ key: option.id, label: option.name }))}
      suggestions={suggestions.map((option) => ({ key: option.id, label: option.name, description: option.description }))}
      onInputChange={setInput}
      onPick={addGenre}
      onRemove={(id) => onChange(selectedIds.filter((selectedId) => selectedId !== id))}
      onCreate={undefined}
    />
  )
}

function TagChipSelect({
  selected,
  onChange,
}: {
  selected: string[]
  onChange: (value: string[]) => void
}) {
  const [input, setInput] = useState('')
  const tagSuggestionsQuery = useQuery({
    queryKey: ['tagSuggestions', input.trim()],
    queryFn: () => api.searchTags(input.trim(), 8),
    enabled: input.trim().length >= 1,
    staleTime: 30_000,
  })
  const normalizedSelected = selected.map((tag) => tag.toLocaleLowerCase())
  const suggestions = (tagSuggestionsQuery.data ?? [])
    .filter((tag) => !normalizedSelected.includes(tag.name.toLocaleLowerCase()))
    .map((tag) => ({ key: tag.name, label: tag.name }))

  function addTag(value: string) {
    const tag = value.trim()
    if (!tag || normalizedSelected.includes(tag.toLocaleLowerCase())) {
      setInput('')
      return
    }

    onChange([...selected, tag])
    setInput('')
  }

  return (
    <ChipBox
      input={input}
      placeholder="Start typing a tag"
      selected={selected.map((tag) => ({ key: tag, label: tag }))}
      suggestions={suggestions}
      onInputChange={setInput}
      onPick={addTag}
      onRemove={(tag) => onChange(selected.filter((selectedTag) => selectedTag !== tag))}
      onCreate={addTag}
    />
  )
}

function ChipBox({
  input,
  placeholder,
  selected,
  suggestions,
  onInputChange,
  onPick,
  onRemove,
  onCreate,
}: {
  input: string
  placeholder: string
  selected: { key: string; label: string }[]
  suggestions: { key: string; label: string; description?: string | null }[]
  onInputChange: (value: string) => void
  onPick: (key: string) => void
  onRemove: (key: string) => void
  onCreate?: (value: string) => void
}) {
  const trimmed = input.trim()
  const canCreate = Boolean(onCreate && trimmed && !suggestions.some((item) => item.label.toLocaleLowerCase() === trimmed.toLocaleLowerCase()))

  function handleKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter' && trimmed) {
      event.preventDefault()
      if (suggestions[0]) {
        onPick(suggestions[0].key)
      } else {
        onCreate?.(trimmed)
      }
    }

    if (event.key === 'Backspace' && !input && selected.length) {
      event.preventDefault()
      onRemove(selected[selected.length - 1].key)
    }
  }

  return (
    <div className="relative">
      <div className="flex min-h-12 flex-wrap items-center gap-2 rounded-md border border-slate-300 bg-white px-3 py-2 focus-within:border-slate-500">
        {selected.map((item) => (
          <span className="inline-flex items-center gap-1 rounded-full border border-slate-500 bg-slate-900 px-2.5 py-1 text-xs text-white shadow-sm" key={item.key}>
            {item.label}
            <button
              aria-label={`Remove ${item.label}`}
              className="rounded-full border border-white/20 p-0.5 hover:bg-white/15"
              type="button"
              onMouseDown={(event) => {
                event.preventDefault()
                event.stopPropagation()
              }}
              onClick={(event) => {
                event.preventDefault()
                event.stopPropagation()
                onRemove(item.key)
              }}
            >
              <X className="h-3 w-3" />
            </button>
          </span>
        ))}
        <input
          className="min-w-40 flex-1 border-0 bg-transparent text-sm outline-none"
          placeholder={selected.length ? '' : placeholder}
          value={input}
          onChange={(event) => onInputChange(event.target.value)}
          onKeyDown={handleKeyDown}
        />
      </div>
      {trimmed ? (
        <div className="absolute z-20 mt-1 max-h-64 w-full overflow-auto rounded-md border border-slate-700 bg-slate-950 shadow-xl">
          {suggestions.map((item) => (
            <button
              className="w-full px-3 py-2 text-left text-sm text-slate-100 hover:bg-slate-800"
              key={item.key}
              type="button"
              onMouseDown={(event) => event.preventDefault()}
              onClick={() => onPick(item.key)}
            >
              <span className="font-medium">{item.label}</span>
              {item.description ? <span className="mt-0.5 block text-xs text-slate-400">{item.description}</span> : null}
            </button>
          ))}
          {canCreate ? (
            <button
              className="w-full px-3 py-2 text-left text-sm text-cyan-100 hover:bg-slate-800"
              type="button"
              onMouseDown={(event) => event.preventDefault()}
              onClick={() => onCreate?.(trimmed)}
            >
              Add "{trimmed}"
            </button>
          ) : null}
          {!suggestions.length && !canCreate ? (
            <div className="px-3 py-2 text-sm text-slate-400">No results.</div>
          ) : null}
        </div>
      ) : null}
    </div>
  )
}

function splitComma(value: string) {
  return value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)
}

function DescriptionHelp({ value }: { value?: string | null }) {
  if (!value) {
    return null
  }

  return <p className="text-xs font-normal text-slate-500">{value}</p>
}
