import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Link2, Save, Star, Upload, X } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { api } from '@/api/client'
import { HttpError } from '@/api/http'
import type { BookCoverDto, BookMutationRequest } from '@/api/types'
import { FormField, buttonClass, inputClass, secondaryButtonClass } from '@/components/app/FormField'
import { BookCoverArtwork, CoverLightbox, useResolvedCoverImage } from './BookCoverSection'
import { bookFormSchema, defaultBookFormValues, toBookMutationRequest, type BookFormValues } from './bookFormSchema'

type BookFormPageProps = {
  mode: 'create' | 'edit'
  admin?: boolean
}

type DraftCoverState =
  | { kind: 'file'; file: File; previewUrl: string }
  | { kind: 'url'; imageUrl: string; previewUrl: string }
  | null

type SaveResult = {
  id: string
  coverError?: string | null
}

type CoverInfoItem = {
  text: string
  truncate?: boolean
}

type ExistingCoverChange =
  | { kind: 'keep' }
  | { kind: 'remove' }

const allowedCoverMimeTypes = new Set(['image/jpeg', 'image/png', 'image/webp'])
const allowedCoverExtensions = new Set(['.jpg', '.jpeg', '.png', '.webp'])

export function BookFormPage({ mode, admin = false }: BookFormPageProps) {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const initializedBookIdRef = useRef<string | null>(null)
  const [authorSuggestionsOpen, setAuthorSuggestionsOpen] = useState(false)
  const [coverDialogOpen, setCoverDialogOpen] = useState(false)
  const [coverDialogView, setCoverDialogView] = useState<'choice' | 'url'>('choice')
  const [coverUrlInput, setCoverUrlInput] = useState('')
  const [coverPreviewOpen, setCoverPreviewOpen] = useState(false)
  const [draftCover, setDraftCover] = useState<DraftCoverState>(null)
  const [existingCoverChange, setExistingCoverChange] = useState<ExistingCoverChange>({ kind: 'keep' })
  const bookQueryKey = [admin ? 'adminBook' : 'book', id] as const
  const bookQuery = useQuery({
    queryKey: bookQueryKey,
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

  const authorName = form.watch('authorName') ?? ''
  const primaryTitle = form.watch('primaryTitle') ?? ''
  const selectedGenreIds = form.watch('genreIds')
  const selectedTags = splitComma(form.watch('tagsText'))
  const selectedType = typesQuery.data?.data.find((type) => type.id === form.watch('contentTypeId'))
  const selectedStatus = statusesQuery.data?.data.find((status) => status.id === form.watch('statusId'))
  const resolvedCoverUrl = useResolvedCoverImage(bookQuery.data?.cover)
  const authorSuggestionsQuery = useQuery({
    queryKey: ['authorSuggestions', authorName.trim()],
    queryFn: () => api.searchAuthors(authorName.trim(), 8),
    enabled: authorName.trim().length >= 2,
    staleTime: 30_000,
  })

  useEffect(() => {
    initializedBookIdRef.current = null
    setExistingCoverChange({ kind: 'keep' })
  }, [id, mode])

  useEffect(() => {
    return () => {
      if (draftCover?.kind === 'file') {
        URL.revokeObjectURL(draftCover.previewUrl)
      }
    }
  }, [draftCover])

  useEffect(() => {
    if (mode === 'edit' && bookQuery.data && typesQuery.data && statusesQuery.data && genresQuery.data && initializedBookIdRef.current !== bookQuery.data.id) {
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
      initializedBookIdRef.current = bookQuery.data.id
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

  async function saveCover(bookId: string, cover: DraftCoverState) {
    if (!cover) {
      return null
    }

    if (cover.kind === 'url') {
      return api.setBookCoverFromUrl(bookId, cover.imageUrl)
    }

    return api.uploadBookCover(bookId, cover.file)
  }

  async function saveBookWithCoverSourceLink(
    bookId: string,
    request: BookMutationRequest,
    savedCover: BookCoverDto | null,
  ) {
    const requestWithCoverSource = appendCoverSourceLink(request, savedCover)
    if (requestWithCoverSource === request) {
      return
    }

    await (admin ? api.updateAdminBook(bookId, requestWithCoverSource) : api.updateBook(bookId, requestWithCoverSource))
  }

  function replaceDraftCover(next: DraftCoverState) {
    setDraftCover((current) => {
      if (current?.kind === 'file') {
        URL.revokeObjectURL(current.previewUrl)
      }
      return next
    })
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

  function openCoverDialog() {
    setCoverDialogView('choice')
    setCoverUrlInput(draftCover?.kind === 'url' ? draftCover.imageUrl : '')
    setCoverDialogOpen(true)
  }

  function closeCoverDialog() {
    setCoverDialogOpen(false)
    setCoverDialogView('choice')
    setCoverUrlInput('')
  }

  function storeDraftFile(file: File) {
    if (!isAllowedCoverFile(file)) {
      toast.error('Choose a JPG, PNG, or WebP image.')
      return
    }

    setExistingCoverChange({ kind: 'keep' })
    replaceDraftCover({ kind: 'file', file, previewUrl: URL.createObjectURL(file) })
    closeCoverDialog()
    toast.success('Cover ready. It will be saved with the book.')
  }

  function storeDraftUrl(imageUrl: string) {
    setExistingCoverChange({ kind: 'keep' })
    replaceDraftCover({ kind: 'url', imageUrl, previewUrl: imageUrl })
    closeCoverDialog()
    toast.success('Cover URL ready. It will be saved with the book.')
  }
  const mutation = useMutation({
    mutationFn: async (values: BookFormValues): Promise<SaveResult> => {
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

        const created = await api.createBook(request)
        let coverError: string | null = null
        try {
          if (draftCover) {
            const savedCover = await saveCover(created.id, draftCover)
            await saveBookWithCoverSourceLink(created.id, request, savedCover)
          }
        } catch (error) {
          coverError = error instanceof HttpError ? error.apiError.detail : error instanceof Error ? error.message : 'Failed to save cover.'
        }

        return { id: created.id, coverError }
      }

      await (admin ? api.updateAdminBook(id!, request) : api.updateBook(id!, request))
      let coverError: string | null = null
      try {
        if (draftCover) {
          const savedCover = await saveCover(id!, draftCover)
          await saveBookWithCoverSourceLink(id!, request, savedCover)
        } else if (existingCoverChange.kind === 'remove') {
          await api.deleteBookCover(id!)
        }
      } catch (error) {
        coverError = error instanceof HttpError ? error.apiError.detail : error instanceof Error ? error.message : 'Failed to save cover.'
      }

      return { id: id!, coverError }
    },
    onSuccess: async (response) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['books'] }),
        queryClient.invalidateQueries({ queryKey: ['adminBooks'] }),
        queryClient.invalidateQueries({ queryKey: ['book', response.id] }),
        queryClient.invalidateQueries({ queryKey: ['adminBook', response.id] }),
      ])
      if (response.coverError) {
        toast.warning(`Book saved, but cover failed: ${response.coverError}`)
      } else {
        toast.success(mode === 'create' ? 'Book added.' : 'Book updated.')
      }
      navigate(admin ? '/admin' : `/books/${response.id}`, { replace: true })
    },
    onError: (error) => {
      toast.error(error instanceof HttpError ? error.apiError.detail : error.message)
    },
  })

  const errors = form.formState.errors
  const isLoadingDictionaries = typesQuery.isLoading || statusesQuery.isLoading || genresQuery.isLoading
  const isLoadingBook = mode === 'edit' && bookQuery.isLoading
  const currentCover = bookQuery.data?.cover
  const visibleCoverUrl = mode === 'create'
    ? draftCover?.previewUrl ?? null
    : draftCover?.previewUrl ?? (existingCoverChange.kind === 'remove'
      ? null
      : resolvedCoverUrl)
  const effectiveCurrentCover = mode === 'edit' && (existingCoverChange.kind === 'remove' || draftCover)
    ? null
    : currentCover
  const coverInfo: CoverInfoItem[] = mode === 'create'
    ? [
      draftCover ? { text: 'Status: Ready to save' } : { text: 'Status: Missing' },
      draftCover?.kind === 'file' ? { text: 'Source: Upload' } : draftCover?.kind === 'url' ? { text: 'Source: URL' } : null,
      draftCover?.kind === 'url' ? { text: draftCover.imageUrl, truncate: true } : null,
    ].filter(isCoverInfoItem)
    : draftCover
      ? [
        { text: 'Status: Will replace on save' },
        draftCover.kind === 'file' ? { text: 'Source: Upload' } : { text: 'Source: URL' },
        draftCover.kind === 'url' ? { text: draftCover.imageUrl, truncate: true } : null,
      ].filter(isCoverInfoItem)
    : effectiveCurrentCover
      ? [
        { text: `Status: ${effectiveCurrentCover.status}` },
        effectiveCurrentCover.source ? { text: `Source: ${effectiveCurrentCover.source}` } : null,
        effectiveCurrentCover.failureReason ? { text: effectiveCurrentCover.failureReason } : null,
      ].filter(isCoverInfoItem)
      : existingCoverChange.kind === 'remove'
        ? [{ text: 'Status: Will be removed on save' }]
        : [{ text: 'Status: Missing' }]

  if (isLoadingDictionaries || isLoadingBook) {
    return <div className="rounded-lg border border-slate-200 bg-white p-6 text-slate-500">Loading form...</div>
  }

  return (
    <>
      <form className="mx-auto grid w-full max-w-7xl gap-5" noValidate onSubmit={form.handleSubmit((values) => mutation.mutate(values))}>
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
          <div className="flex flex-col gap-5">
            <div className="flex flex-col gap-5 lg:flex-row lg:items-start">
              <div className="flex-none lg:w-[320px]">
                <BookCoverArtwork
                  className="w-full lg:w-[320px]"
                  cover={mode === 'edit' ? effectiveCurrentCover : null}
                  emptyLabel="Click to add a cover."
                  hint={mode === 'create' ? 'The cover will be uploaded right after the book is created.' : 'Choose file upload or image URL.'}
                  imageUrl={visibleCoverUrl}
                  interactive
                  removeLabel="Remove cover"
                  title={primaryTitle || 'Book cover'}
                  onClick={() => {
                    if (visibleCoverUrl) {
                      setCoverPreviewOpen(true)
                      return
                    }

                    openCoverDialog()
                  }}
                  onRemove={() => {
                    if (mode === 'create') {
                      replaceDraftCover(null)
                      return
                    }

                    if (draftCover) {
                      replaceDraftCover(null)
                      setExistingCoverChange({ kind: 'keep' })
                      toast.success('Draft cover removed.')
                      return
                    }

                    setExistingCoverChange({ kind: 'remove' })
                    toast.success('Cover will be removed after saving the book.')
                  }}
                />
                <div className="mt-2 grid content-start gap-2 rounded-2xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
                  {coverInfo.map((item) => (
                    <p
                      className={`${item.text === effectiveCurrentCover?.failureReason ? 'text-red-600' : ''} ${item.truncate ? 'truncate' : ''}`}
                      key={item.text}
                      title={item.truncate ? item.text : undefined}
                    >
                      {item.text}
                    </p>
                  ))}
                  <p className="text-xs text-slate-500">
                    Cover source URLs are appended to book links automatically.
                  </p>
                </div>
              </div>

              <div className="min-w-0 flex-1">
                <div className="grid gap-5">
                  <div className="grid items-start gap-x-4 gap-y-4 md:grid-cols-2">
                    <FormField error={errors.primaryTitle?.message} label="Primary title">
                      <input aria-label="Primary title" className={inputClass} {...form.register('primaryTitle')} />
                    </FormField>
                    <FormField error={errors.authorName?.message} label="Author">
                      <div className="relative">
                        <input
                          aria-label="Author"
                          className={`${inputClass} w-full`}
                          name="authorName"
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

                  <div className="grid max-w-4xl gap-5">
                    <div className="grid gap-x-5 gap-y-4 md:grid-cols-3">
                    <FormField error={errors.currentChapterNumber?.message} label="Current chapter">
                      <input
                        aria-label="Current chapter"
                        aria-invalid={errors.currentChapterNumber ? 'true' : undefined}
                        className={inputClass}
                        inputMode="decimal"
                        {...form.register('currentChapterNumber')}
                      />
                    </FormField>
                      <FormField error={errors.currentChapterLabel?.message} label="Label">
                        <input className={inputClass} {...form.register('currentChapterLabel')} />
                      </FormField>
                    <FormField error={errors.totalChapters?.message} label="Total chapters">
                      <input
                        aria-label="Total chapters"
                        aria-invalid={errors.totalChapters ? 'true' : undefined}
                        className={inputClass}
                        inputMode="decimal"
                        {...form.register('totalChapters')}
                      />
                    </FormField>
                    </div>
                    <div className="grid gap-x-5 gap-y-4 md:grid-cols-2">
                      <FormField error={errors.priority?.message} label="Priority 1-5">
                        <input
                          aria-label="Priority 1-5"
                          aria-invalid={errors.priority ? 'true' : undefined}
                          className={inputClass}
                          inputMode="numeric"
                          {...form.register('priority')}
                        />
                      </FormField>
                      <FormField error={errors.rating?.message} label="Rating">
                        <RatingStars
                          value={form.watch('rating') ?? ''}
                          onChange={(value) => form.setValue('rating', value, { shouldDirty: true, shouldValidate: true })}
                        />
                      </FormField>
                    </div>
                    <div className="grid max-w-4xl gap-4">
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
                  </div>
                </div>
              </div>
            </div>
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

      <CoverSourceDialog
        mode={mode}
        open={coverDialogOpen}
        setUrlPending={mutation.isPending}
        uploadPending={mutation.isPending}
        urlInput={coverUrlInput}
        view={coverDialogView}
        onBackToChoice={() => setCoverDialogView('choice')}
        onClose={closeCoverDialog}
        onOpenUrl={() => setCoverDialogView('url')}
        onUpload={storeDraftFile}
        onUrlChange={setCoverUrlInput}
        onUrlSubmit={() => {
          const trimmed = coverUrlInput.trim()
          if (!trimmed) {
            return
          }

          storeDraftUrl(trimmed)
        }}
      />
      <CoverLightbox
        emptyLabel="No cover has been saved for this book yet."
        imageUrl={visibleCoverUrl}
        open={coverPreviewOpen}
        title={primaryTitle || 'Book cover'}
        onClose={() => setCoverPreviewOpen(false)}
      />
    </>
  )
}

function normalizeAuthorName(value: string) {
  return value.trim().toLocaleUpperCase()
}

function isAllowedCoverFile(file: File) {
  const lowerName = file.name.toLocaleLowerCase()
  const extensionAllowed = Array.from(allowedCoverExtensions).some((extension) => lowerName.endsWith(extension))
  return allowedCoverMimeTypes.has(file.type) && extensionAllowed
}

function isCoverInfoItem(value: CoverInfoItem | null): value is CoverInfoItem {
  return value !== null
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
      onCreate={undefined}
      onInputChange={setInput}
      onPick={addGenre}
      onRemove={(selectedId) => onChange(selectedIds.filter((id) => id !== selectedId))}
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
      onCreate={addTag}
      onInputChange={setInput}
      onPick={addTag}
      onRemove={(tag) => onChange(selected.filter((selectedTag) => selectedTag !== tag))}
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

function CoverSourceDialog({
  mode,
  open,
  view,
  urlInput,
  uploadPending,
  setUrlPending,
  onClose,
  onOpenUrl,
  onBackToChoice,
  onUpload,
  onUrlChange,
  onUrlSubmit,
}: {
  mode: 'create' | 'edit'
  open: boolean
  view: 'choice' | 'url'
  urlInput: string
  uploadPending: boolean
  setUrlPending: boolean
  onClose: () => void
  onOpenUrl: () => void
  onBackToChoice: () => void
  onUpload: (file: File) => void
  onUrlChange: (value: string) => void
  onUrlSubmit: () => void
}) {
  if (!open) {
    return null
  }

  const urlButtonLabel = mode === 'create' ? 'Use URL' : 'Save URL'
  const introText = mode === 'create'
    ? 'Pick the cover now. It will be uploaded automatically after the book is created.'
    : 'Choose whether the cover comes from a local file or a direct image link.'

  return (
    <div
      aria-modal="true"
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 p-4 backdrop-blur-sm"
      role="dialog"
      onClick={onClose}
    >
      <div className="grid w-full max-w-xl gap-4 rounded-3xl border border-slate-700 bg-slate-950 p-6 shadow-2xl" onClick={(event) => event.stopPropagation()}>
        <div className="flex items-start justify-between gap-4">
          <div className="grid gap-1">
            <h2 className="text-lg font-semibold text-white">Add cover</h2>
            <p className="text-sm text-slate-400">{introText}</p>
          </div>
          <button className="rounded-full border border-slate-700 p-2 text-slate-300 hover:bg-slate-900" type="button" onClick={onClose}>
            <X className="h-4 w-4" />
          </button>
        </div>

        {view === 'choice' ? (
          <div className="grid gap-3 sm:grid-cols-2">
            <label className="grid gap-2 rounded-2xl border border-slate-700 bg-slate-900 p-4 text-left text-slate-200 transition hover:border-cyan-400 hover:bg-slate-900/80">
              <div className="inline-flex h-10 w-10 items-center justify-center rounded-full bg-cyan-500/15 text-cyan-300">
                <Upload className="h-4 w-4" />
              </div>
              <div className="text-base font-semibold">Upload image</div>
              <div className="text-sm text-slate-400">Use JPG, PNG, or WebP from disk.</div>
              <input
                accept="image/jpeg,image/png,image/webp"
                className="sr-only"
                disabled={mode === 'edit' && uploadPending}
                type="file"
                onChange={(event) => {
                  const file = event.target.files?.[0]
                  if (file) {
                    onUpload(file)
                    event.target.value = ''
                  }
                }}
              />
            </label>
            <button
              className="grid gap-2 rounded-2xl border border-slate-700 bg-slate-900 p-4 text-left text-slate-200 transition hover:border-cyan-400 hover:bg-slate-900/80"
              type="button"
              onClick={onOpenUrl}
            >
              <div className="inline-flex h-10 w-10 items-center justify-center rounded-full bg-cyan-500/15 text-cyan-300">
                <Link2 className="h-4 w-4" />
              </div>
              <div className="text-base font-semibold">Paste image URL</div>
              <div className="text-sm text-slate-400">The original URL is also added to book links.</div>
            </button>
          </div>
        ) : (
          <div className="grid gap-3">
            <input
              className={inputClass}
              placeholder="https://example.com/cover.jpg"
              value={urlInput}
              onChange={(event) => onUrlChange(event.target.value)}
            />
            <div className="flex flex-wrap gap-2">
              <button className={buttonClass} disabled={mode === 'edit' && setUrlPending || !urlInput.trim()} type="button" onClick={onUrlSubmit}>
                {urlButtonLabel}
              </button>
              <button className={secondaryButtonClass} type="button" onClick={onBackToChoice}>
                Back
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

function appendUniqueLine(value: string, nextLine: string) {
  const trimmedLine = nextLine.trim()
  if (!trimmedLine) {
    return value
  }

  const lines = value
    .split('\n')
    .map((item) => item.trim())
    .filter(Boolean)
  if (lines.some((line) => line.localeCompare(trimmedLine, undefined, { sensitivity: 'accent' }) === 0)) {
    return value
  }

  return lines.length ? `${lines.join('\n')}\n${trimmedLine}` : trimmedLine
}

function appendCoverSourceLink(request: BookMutationRequest, cover?: BookCoverDto | null) {
  const sourceUrl = cover?.originalImageUrl?.trim()
  if (!sourceUrl) {
    return request
  }

  const nextLinks = appendUniqueLine(
    request.links.map((link) => link.url).join('\n'),
    sourceUrl,
  )
    .split('\n')
    .map((url) => url.trim())
    .filter(Boolean)

  if (nextLinks.length === request.links.length) {
    return request
  }

  return {
    ...request,
    links: nextLinks.map((url) => ({
      url,
      label: null,
      sourceType: 'Other',
      isPrimary: false,
      lastReadHere: false,
    })),
  }
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

function RatingStars({
  value,
  onChange,
}: {
  value: string
  onChange: (value: string) => void
}) {
  const numericValue = Number(value)
  const normalizedValue = Number.isFinite(numericValue) && numericValue > 0 ? numericValue : 0

  return (
    <div className="flex min-h-10 min-w-0 flex-wrap items-center gap-2">
      <div className="flex w-14 shrink-0 items-center text-base font-bold text-slate-100">
        {normalizedValue ? `${normalizedValue}/10` : '?/10'}
      </div>
      <div className="flex min-w-0 flex-1 flex-wrap items-center gap-1">
        {[1, 2, 3, 4, 5, 6, 7, 8, 9, 10].map((star) => {
          const active = normalizedValue >= star
          return (
            <div className="relative flex h-7 w-7 shrink-0 items-center justify-center" key={star}>
              <div className="pointer-events-none absolute inset-0 flex items-center justify-center">
                <Star className={`h-5 w-5 ${active ? 'fill-amber-400 text-amber-400' : 'fill-slate-200 text-slate-300'}`} />
              </div>
              <button
                aria-label={`Set rating to ${star}/10`}
                className="absolute inset-0 z-10"
                type="button"
                onClick={() => onChange(normalizedValue === star ? '' : String(star))}
              />
            </div>
          )
        })}
      </div>
    </div>
  )
}
