import { useInfiniteQuery, useQuery, useQueryClient } from '@tanstack/react-query'
import { BookOpen, EyeOff, Globe2, Plus, RefreshCw, Search, Tags, Trash2, Users, X } from 'lucide-react'
import { useEffect, useId, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import { toast } from 'sonner'
import { api } from '@/api/client'
import { HttpError } from '@/api/http'
import type { AuthorDto, BookListItemDto, PublicBookSnapshotDto, TagDto } from '@/api/types'
import { Badge, buttonVariants, controlClass, DialogPanel, PageHeader, Surface, useBodyScrollLock } from '@/components/app/DesignSystem'
import { buildBookQuery, emptyFilters } from '@/features/books/queryBuilder'
import { cn } from '@/lib/utils'

type ManageSection = 'tags' | 'authors' | 'books'
type ManagedItem = { kind: 'tag'; value: TagDto | null } | { kind: 'author'; value: AuthorDto | null }

export function ManagePage() {
  const [section, setSection] = useState<ManageSection>('tags')
  const [search, setSearch] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [selected, setSelected] = useState<ManagedItem | null>(null)
  const [bookActionId, setBookActionId] = useState<string | null>(null)
  const queryClient = useQueryClient()

  useEffect(() => {
    const timeout = window.setTimeout(() => setDebouncedSearch(search.trim()), 180)
    return () => window.clearTimeout(timeout)
  }, [search])

  const results = useQuery<TagDto[] | AuthorDto[]>({
    queryKey: ['manage-metadata', section, debouncedSearch],
    queryFn: () => section === 'tags'
      ? api.searchTags(debouncedSearch, 50)
      : api.searchAuthors(debouncedSearch, 50),
    enabled: section !== 'books',
  })

  const libraryBooks = useInfiniteQuery({
    queryKey: ['manage-books', debouncedSearch],
    initialPageParam: 0,
    queryFn: ({ pageParam }) => api.getBooks({
      skip: pageParam,
      take: 50,
      query: buildBookQuery({ ...emptyFilters, text: debouncedSearch }) || undefined,
    }),
    getNextPageParam: (lastPage) => {
      const nextSkip = lastPage.skip + lastPage.data.length
      return nextSkip < lastPage.total ? nextSkip : undefined
    },
    enabled: section === 'books',
  })

  const publishedBooks = useInfiniteQuery({
    queryKey: ['public-books', 'mine'],
    initialPageParam: 0,
    queryFn: ({ pageParam }) => api.searchPublicBooks({ skip: pageParam, take: 50, mineOnly: true }),
    getNextPageParam: (lastPage) => {
      const nextSkip = lastPage.skip + lastPage.data.length
      return nextSkip < lastPage.total ? nextSkip : undefined
    },
    enabled: section === 'books',
  })

  useEffect(() => {
    if (section === 'books' && publishedBooks.hasNextPage && !publishedBooks.isFetchingNextPage) {
      void publishedBooks.fetchNextPage()
    }
  }, [publishedBooks.data?.pages.length, publishedBooks.fetchNextPage, publishedBooks.hasNextPage,
    publishedBooks.isFetchingNextPage, section])

  function changeSection(next: ManageSection) {
    setSection(next)
    setSearch('')
    setDebouncedSearch('')
  }

  const libraryBookPages = libraryBooks.data?.pages ?? []
  const publishedBookPages = publishedBooks.data?.pages ?? []
  const libraryBookItems = libraryBookPages.flatMap((page) => page.data)
  const publishedBookItems = publishedBookPages.flatMap((page) => page.data)
  const itemCount = section === 'books' ? libraryBookPages[0]?.total ?? 0 : results.data?.length ?? 0

  async function runBookAction(bookId: string, action: () => Promise<unknown>, success: string) {
    setBookActionId(bookId)
    try {
      await action()
      toast.success(success)
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['public-books'] }),
        queryClient.invalidateQueries({ queryKey: ['manage-metadata'] }),
      ])
    } catch (error) {
      toast.error(getErrorMessage(error))
    } finally {
      setBookActionId(null)
    }
  }

  return (
    <div className="manage-page">
      <PageHeader
        description="Keep saved metadata tidy and control which private books have a public snapshot."
        eyebrow="Library controls"
        title="Manage"
      />

      <Surface className="manage-workspace">
        <div className="manage-rail" role="tablist" aria-label="Manage section">
          <button
            aria-selected={section === 'tags'}
            className={cn('manage-tab', section === 'tags' && 'manage-tab--active')}
            role="tab"
            type="button"
            onClick={() => changeSection('tags')}
          >
            <Tags className="h-4 w-4" />
            <span>Tags</span>
          </button>
          <button
            aria-selected={section === 'authors'}
            className={cn('manage-tab', section === 'authors' && 'manage-tab--active')}
            role="tab"
            type="button"
            onClick={() => changeSection('authors')}
          >
            <Users className="h-4 w-4" />
            <span>Authors</span>
          </button>
          <button
            aria-selected={section === 'books'}
            className={cn('manage-tab', section === 'books' && 'manage-tab--active')}
            role="tab"
            type="button"
            onClick={() => changeSection('books')}
          >
            <BookOpen className="h-4 w-4" />
            <span>Books</span>
          </button>
        </div>

        <section className="manage-content" role="tabpanel">
          <div className="manage-toolbar">
            <div>
              <div className="ui-eyebrow">{section === 'tags' ? 'Personal taxonomy' : section === 'authors' ? 'Shared identities' : 'Community listings'}</div>
              <h2 className="manage-title">{section === 'tags' ? 'Saved tags' : section === 'authors' ? 'Known authors' : 'Your books'}</h2>
            </div>
            <div className="flex items-center gap-2">
              <Badge tone="neutral">{itemCount} shown</Badge>
              {section !== 'books' ? <button
                className={buttonVariants.primary}
                type="button"
                onClick={() => setSelected({ kind: section === 'tags' ? 'tag' : 'author', value: null } as ManagedItem)}
              >
                <Plus className="h-4 w-4" />
                Add {section === 'tags' ? 'tag' : 'author'}
              </button> : null}
            </div>
          </div>

          <label className="manage-search">
            <Search aria-hidden="true" className="h-4 w-4" />
            <span className="sr-only">Search {section}</span>
            <input
              autoComplete="off"
              placeholder={section === 'tags' ? 'Search tags…' : section === 'authors' ? 'Search authors or aliases…' : 'Search your books…'}
              type="search"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
            {(section === 'books' ? libraryBooks.isFetching : results.isFetching) ? <span className="manage-search__status">Searching…</span> : null}
          </label>

          <p className="manage-hint">{section === 'books'
            ? 'Listing creates a public snapshot. Refresh it after editing your private copy; existing imports never change.'
            : 'Double-click a row to edit it, or use the Edit button.'}</p>

          {section === 'books' ? (
            <ManagedBooks
              actionId={bookActionId}
              books={libraryBookItems}
              error={libraryBooks.isError || publishedBooks.isError}
              hasMore={libraryBooks.hasNextPage}
              loading={libraryBooks.isPending || publishedBooks.isPending}
              loadingMore={libraryBooks.isFetchingNextPage}
              published={publishedBookItems}
              search={search}
              onLoadMore={() => void libraryBooks.fetchNextPage()}
              onPublish={(book) => runBookAction(book.id, () => api.publishBook(book.id), `“${book.primaryTitle}” is now listed.`)}
              onRefresh={(snapshot) => runBookAction(snapshot.sourceBookId, () => api.refreshPublishedBook(snapshot.id), `“${snapshot.primaryTitle}” snapshot refreshed.`)}
              onUnlist={(snapshot) => runBookAction(snapshot.sourceBookId, () => api.unlistPublishedBook(snapshot.id), `“${snapshot.primaryTitle}” was unlisted.`)}
            />
          ) : results.isError ? (
            <div className="manage-state manage-state--error">Could not load {section}. Try again.</div>
          ) : results.isPending ? (
            <div className="manage-state">Loading {section}…</div>
          ) : itemCount === 0 ? (
            <div className="manage-state">
              <span>No {section} found.</span>
              <small>{search.trim() ? 'Try a broader search.' : `Use Add ${section === 'tags' ? 'tag' : 'author'} to create one.`}</small>
            </div>
          ) : section === 'tags' ? (
            <TagList tags={results.data as TagDto[]} onEdit={(tag) => setSelected({ kind: 'tag', value: tag })} />
          ) : (
            <AuthorList authors={results.data as AuthorDto[]} onEdit={(author) => setSelected({ kind: 'author', value: author })} />
          )}
        </section>
      </Surface>

      <ManageDialog item={selected} onClose={() => setSelected(null)} />
    </div>
  )
}

function ManagedBooks({ books, published, actionId, loading, loadingMore, hasMore, error, search, onLoadMore, onPublish, onRefresh, onUnlist }: {
  books: BookListItemDto[]
  published: PublicBookSnapshotDto[]
  actionId: string | null
  loading: boolean
  loadingMore: boolean
  hasMore: boolean
  error: boolean
  search: string
  onLoadMore: () => void
  onPublish: (book: BookListItemDto) => void
  onRefresh: (snapshot: PublicBookSnapshotDto) => void
  onUnlist: (snapshot: PublicBookSnapshotDto) => void
}) {
  if (error) return <div className="manage-state manage-state--error">Could not load your book listings. Try again.</div>
  if (loading) return <div className="manage-state">Loading your books…</div>
  if (books.length === 0) {
    return (
      <div className="manage-state">
        <span>No books found.</span>
        <small>{search.trim() ? 'Try a broader search.' : 'Add a book to your library before creating a public listing.'}</small>
      </div>
    )
  }

  const snapshotByBookId = new Map(published.map((snapshot) => [snapshot.sourceBookId, snapshot]))
  const sortedBooks = [...books].sort((left, right) => {
    const listingOrder = Number(snapshotByBookId.has(right.id)) - Number(snapshotByBookId.has(left.id))
    return listingOrder || left.primaryTitle.localeCompare(right.primaryTitle, undefined, {
      sensitivity: 'base',
      numeric: true,
    })
  })
  return (
    <div
      aria-busy={loadingMore}
      aria-label="Books"
      className="manage-list"
      tabIndex={0}
      onScroll={(event) => {
        const list = event.currentTarget
        if (hasMore && !loadingMore && list.scrollHeight - list.scrollTop - list.clientHeight < 96) {
          onLoadMore()
        }
      }}
    >
      {sortedBooks.map((book) => {
        const snapshot = snapshotByBookId.get(book.id)
        const busy = actionId === book.id
        const missingRequirements = getMissingListingRequirements(book)
        const canPublish = missingRequirements.length === 0
        return (
          <div className="manage-item" key={book.id}>
            <span className="manage-item__icon"><BookOpen className="h-4 w-4" /></span>
            <span className="manage-item__body">
              <strong>{book.primaryTitle}</strong>
              <small>{book.author || 'Unknown author'} · {book.contentType}{snapshot ? ` · Snapshot ${new Date(snapshot.snapshotAt).toLocaleDateString()}` : ''}</small>
            </span>
            <Badge tone={snapshot ? 'success' : 'neutral'}>{snapshot ? 'Listed' : 'Private'}</Badge>
            {snapshot ? (
              <span className="manage-book-actions">
                <button
                  aria-label={`Refresh listing ${book.primaryTitle}`}
                  className={buttonVariants.secondary}
                  disabled={busy || !canPublish}
                  type="button"
                  title={canPublish ? undefined : `Required before refreshing: ${missingRequirements.join(', ')}`}
                  onClick={() => onRefresh(snapshot)}
                >
                  <RefreshCw className="h-4 w-4" />
                  Refresh
                </button>
                <button
                  aria-label={`Unlist ${book.primaryTitle}`}
                  className={buttonVariants.ghost}
                  disabled={busy}
                  type="button"
                  onClick={() => onUnlist(snapshot)}
                >
                  <EyeOff className="h-4 w-4" />
                  Unlist
                </button>
              </span>
            ) : (
              <button
                aria-label={`List ${book.primaryTitle}`}
                className={buttonVariants.primary}
                disabled={busy || !canPublish}
                type="button"
                title={canPublish ? undefined : `Required before listing: ${missingRequirements.join(', ')}`}
                onClick={() => onPublish(book)}
              >
                <Globe2 className="h-4 w-4" />
                {busy ? 'Listing…' : 'List publicly'}
              </button>
            )}
          </div>
        )
      })}
      {loadingMore ? <div className="manage-list__loading">Loading more books…</div> : null}
    </div>
  )
}

function TagList({ tags, onEdit }: { tags: TagDto[]; onEdit: (tag: TagDto) => void }) {
  const sortedTags = [...tags].sort((left, right) => left.name.localeCompare(right.name, undefined, {
    sensitivity: 'base',
    numeric: true,
  }))
  return (
    <div className="manage-list" aria-label="Tags" tabIndex={0}>
      {sortedTags.map((tag) => (
        <ManageRow editable={!tag.isGlobal} key={tag.id} label={`Edit tag ${tag.name}`} onEdit={() => onEdit(tag)}>
          <span className="manage-item__icon"><Tags className="h-4 w-4" /></span>
          <span className="manage-item__body">
            <strong>{tag.name}</strong>
            <small>{tag.description || 'No description yet'}</small>
          </span>
          {tag.isGlobal ? <Badge tone="accent">Global</Badge> : null}
        </ManageRow>
      ))}
    </div>
  )
}

function AuthorList({ authors, onEdit }: { authors: AuthorDto[]; onEdit: (author: AuthorDto) => void }) {
  const sortedAuthors = [...authors].sort((left, right) => left.primaryName.localeCompare(right.primaryName, undefined, {
    sensitivity: 'base',
    numeric: true,
  }))
  return (
    <div className="manage-list" aria-label="Authors" tabIndex={0}>
      {sortedAuthors.map((author) => (
        <ManageRow editable={author.isOwned} key={author.id} label={`Edit author ${author.primaryName}`} onEdit={() => onEdit(author)} readOnlyLabel="Global identity">
          <span className="manage-item__icon"><Users className="h-4 w-4" /></span>
          <span className="manage-item__body">
            <strong>{author.primaryName}</strong>
            <small>{author.otherNames.length > 0 ? author.otherNames.join(' · ') : 'No alternative names'}</small>
          </span>
          <Badge tone={author.isOwned ? 'neutral' : 'accent'}>{author.isOwned ? 'Yours' : 'Global'}</Badge>
          {author.isOwned && author.isPublic ? <Badge tone="accent">Public</Badge> : null}
          <Badge tone={author.otherNames.length > 0 ? 'accent' : 'neutral'}>{author.otherNames.length} aliases</Badge>
        </ManageRow>
      ))}
    </div>
  )
}

function getMissingListingRequirements(book: BookListItemDto) {
  const missing: string[] = []
  if (!book.description?.trim()) missing.push('description')
  if (!book.author?.trim()) missing.push('author')
  if (book.genresCount === 0) missing.push('genre')
  if (book.tagsCount === 0) missing.push('tag')
  if (!book.cover?.imageUrl) {
    missing.push('stored cover')
  }
  return missing
}

function ManageRow({ children, editable = true, label, readOnlyLabel = 'Managed by admin', onEdit }: { children: React.ReactNode; editable?: boolean; label: string; readOnlyLabel?: string; onEdit: () => void }) {
  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (editable && event.key === 'Enter') {
      onEdit()
    }
  }

  return (
    <div className="manage-item" role="group" tabIndex={editable ? 0 : -1} onDoubleClick={editable ? onEdit : undefined} onKeyDown={handleKeyDown}>
      {children}
      {editable ? <button aria-label={label} className={buttonVariants.ghost} type="button" onClick={onEdit}>Edit</button> : <span className="text-xs text-[var(--qs-subtle)]">{readOnlyLabel}</span>}
    </div>
  )
}

function ManageDialog({ item, onClose }: { item: ManagedItem | null; onClose: () => void }) {
  const queryClient = useQueryClient()
  const titleId = useId()
  const nameFieldRef = useRef<HTMLInputElement>(null)
  const firstFieldRef = useRef<HTMLTextAreaElement>(null)
  const [nameValue, setNameValue] = useState('')
  const [value, setValue] = useState('')
  const [saving, setSaving] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)
  useBodyScrollLock(item !== null)

  useEffect(() => {
    if (!item) {
      return
    }

    setNameValue(item.value ? (item.kind === 'tag' ? item.value.name : item.value.primaryName) : '')
    setValue(item.value ? (item.kind === 'tag' ? item.value.description ?? '' : item.value.otherNames.join('\n')) : '')
    setConfirmDelete(false)
    window.setTimeout(() => (item.value ? firstFieldRef.current : nameFieldRef.current)?.focus(), 0)
  }, [item])

  useEffect(() => {
    if (!item) {
      return
    }

    function closeOnEscape(event: globalThis.KeyboardEvent) {
      if (event.key === 'Escape' && !saving) {
        onClose()
      }
    }

    window.addEventListener('keydown', closeOnEscape)
    return () => window.removeEventListener('keydown', closeOnEscape)
  }, [item, onClose, saving])

  if (!item) {
    return null
  }

  const isCreate = item.value === null
  const name = item.value ? (item.kind === 'tag' ? item.value.name : item.value.primaryName) : nameValue
  const section = item.kind === 'tag' ? 'tags' : 'authors'

  async function handleSave(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    try {
      if (item?.kind === 'tag' && item.value) {
        await api.updateTag(item.value.id, { description: value.trim() || null })
        toast.success(`Tag “${item.value.name}” updated.`)
      } else if (item?.kind === 'tag') {
        await api.createTag({ name: nameValue.trim(), description: value.trim() || null })
        toast.success(`Tag “${nameValue.trim()}” created.`)
      } else if (item?.kind === 'author' && item.value) {
        const otherNames = value.split('\n').map((alias) => alias.trim()).filter(Boolean)
        await api.updateAuthor(item.value.id, { otherNames })
        toast.success(`Author “${item.value.primaryName}” updated.`)
      } else if (item?.kind === 'author') {
        const otherNames = value.split('\n').map((alias) => alias.trim()).filter(Boolean)
        await api.createAuthor({ primaryName: nameValue.trim(), otherNames })
        toast.success(`Author “${nameValue.trim()}” created.`)
      }
      await queryClient.invalidateQueries({ queryKey: ['manage-metadata', section] })
      onClose()
    } catch (error) {
      toast.error(getErrorMessage(error))
    } finally {
      setSaving(false)
    }
  }

  async function handleDelete() {
    if (!confirmDelete) {
      setConfirmDelete(true)
      return
    }

    setSaving(true)
    try {
      if (item?.kind === 'tag' && item.value) {
        await api.deleteTag(item.value.id)
      } else if (item?.kind === 'author' && item.value) {
        await api.deleteAuthor(item.value.id)
      }
      toast.success(`${item?.kind === 'tag' ? 'Tag' : 'Author'} “${name}” deleted.`)
      await queryClient.invalidateQueries({ queryKey: ['manage-metadata', section] })
      onClose()
    } catch (error) {
      toast.error(getErrorMessage(error))
      setConfirmDelete(false)
    } finally {
      setSaving(false)
    }
  }

  async function handleVisibilityChange() {
    if (item?.kind !== 'author' || !item.value) {
      return
    }

    const isPublic = !item.value.isPublic
    setSaving(true)
    try {
      await api.updateAuthorVisibility(item.value.id, isPublic)
      toast.success(`Author “${item.value.primaryName}” is now ${isPublic ? 'public' : 'private'}.`)
      await queryClient.invalidateQueries({ queryKey: ['manage-metadata', 'authors'] })
      onClose()
    } catch (error) {
      toast.error(getErrorMessage(error))
    } finally {
      setSaving(false)
    }
  }

  return (
    <div aria-labelledby={titleId} aria-modal="true" className="manage-dialog" role="dialog" onMouseDown={onClose}>
      <DialogPanel className="manage-dialog__panel" onMouseDown={(event) => event.stopPropagation()}>
        <div className="manage-dialog__header">
          <div>
            <div className="ui-eyebrow">{isCreate ? 'Create' : 'Edit'} {item.kind}</div>
            <h2 id={titleId}>{isCreate ? `New ${item.kind}` : name}</h2>
          </div>
          <button aria-label="Close dialog" className={`${buttonVariants.ghost} ui-icon-button`} disabled={saving} type="button" onClick={onClose}>
            <X className="h-4 w-4" />
          </button>
        </div>

        <form className="manage-dialog__form" onSubmit={handleSave}>
          {isCreate ? (
            <label>
              <span>{item.kind === 'tag' ? 'Tag name' : 'Primary name'}</span>
              <input
                ref={nameFieldRef}
                aria-label={item.kind === 'tag' ? 'Tag name' : 'Primary name'}
                className={controlClass}
                maxLength={item.kind === 'tag' ? 100 : 300}
                required
                value={nameValue}
                onChange={(event) => setNameValue(event.target.value)}
              />
            </label>
          ) : null}
          {item.kind === 'tag' ? (
            <label>
              <span>Description</span>
              <textarea
                aria-label="Description"
                ref={firstFieldRef}
                className={controlClass}
                maxLength={500}
                placeholder="What does this tag mean in your library?"
                rows={6}
                value={value}
                onChange={(event) => setValue(event.target.value)}
              />
              <small>{value.length}/500 characters</small>
            </label>
          ) : (
            <label>
              <span>Alternative names</span>
              <textarea
                aria-label="Alternative names"
                ref={firstFieldRef}
                className={controlClass}
                placeholder={'One alternative name per line\nExample: Cuttlefish That Loves Diving'}
                rows={8}
                value={value}
                onChange={(event) => setValue(event.target.value)}
              />
              <small>One name per line. The primary name stays unchanged.{isCreate ? ' New authors start private.' : ''}</small>
            </label>
          )}

          {item.kind === 'author' && item.value ? (
            <div className="rounded-xl border border-[var(--qs-line)] bg-[var(--qs-surface-muted)] p-4">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="max-w-md">
                  <strong className="text-sm text-[var(--qs-text)]">
                    {item.value.isPublic ? 'Public author' : 'Private author'}
                  </strong>
                  <p className="mt-1 text-xs leading-5 text-[var(--qs-muted)]">
                    {item.value.isPublic
                      ? 'Other readers can use this identity. Making it private creates private copies for libraries already using it.'
                      : 'Only you can find this identity. Publishing makes its primary and alternative names available to everyone.'}
                  </p>
                </div>
                <button
                  className={buttonVariants.secondary}
                  disabled={saving}
                  type="button"
                  onClick={handleVisibilityChange}
                >
                  {item.value.isPublic ? 'Make private' : 'Make public'}
                </button>
              </div>
            </div>
          ) : null}

          <div className="manage-dialog__actions">
            {!isCreate ? <button
              className={cn(buttonVariants.destructive, confirmDelete && 'manage-delete--confirm')}
              disabled={saving}
              type="button"
              onClick={handleDelete}
            >
              <Trash2 className="h-4 w-4" />
              {confirmDelete ? 'Confirm delete' : `Delete ${item.kind}`}
            </button> : null}
            <span className="manage-dialog__action-spacer" />
            <button className={buttonVariants.secondary} disabled={saving} type="button" onClick={onClose}>Cancel</button>
            <button className={buttonVariants.primary} disabled={saving || (isCreate && !nameValue.trim())} type="submit">{saving ? 'Saving…' : isCreate ? `Create ${item.kind}` : 'Save changes'}</button>
          </div>
          {confirmDelete ? <p className="manage-delete-note">{item.kind === 'author' && item.value?.isPublic
            ? 'Your own books must stop using this author first. Other readers keep private copies.'
            : 'This cannot be undone. Items still used by books cannot be deleted.'}</p> : null}
        </form>
      </DialogPanel>
    </div>
  )
}

function getErrorMessage(error: unknown) {
  if (error instanceof HttpError) {
    return error.apiError.detail
  }
  return error instanceof Error ? error.message : 'The operation could not be completed.'
}
