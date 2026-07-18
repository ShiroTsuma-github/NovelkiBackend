import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Search, Tags, Trash2, Users, X } from 'lucide-react'
import { useEffect, useId, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import { toast } from 'sonner'
import { api } from '@/api/client'
import { HttpError } from '@/api/http'
import type { AuthorDto, TagDto } from '@/api/types'
import { Badge, buttonVariants, controlClass, DialogPanel, PageHeader, Surface, useBodyScrollLock } from '@/components/app/DesignSystem'
import { cn } from '@/lib/utils'

type ManageSection = 'tags' | 'authors'
type ManagedItem = { kind: 'tag'; value: TagDto } | { kind: 'author'; value: AuthorDto }

export function ManagePage() {
  const [section, setSection] = useState<ManageSection>('tags')
  const [search, setSearch] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [selected, setSelected] = useState<ManagedItem | null>(null)

  useEffect(() => {
    const timeout = window.setTimeout(() => setDebouncedSearch(search.trim()), 180)
    return () => window.clearTimeout(timeout)
  }, [search])

  const results = useQuery<TagDto[] | AuthorDto[]>({
    queryKey: ['manage-metadata', section, debouncedSearch],
    queryFn: () => section === 'tags'
      ? api.searchTags(debouncedSearch, 50)
      : api.searchAuthors(debouncedSearch, 50, true),
  })

  function changeSection(next: ManageSection) {
    setSection(next)
    setSearch('')
    setDebouncedSearch('')
  }

  const itemCount = results.data?.length ?? 0

  return (
    <div className="manage-page">
      <PageHeader
        description="Keep saved tags and author identities tidy, even after their books are gone."
        eyebrow="Library metadata"
        title="Manage"
      />

      <Surface className="manage-workspace">
        <div className="manage-rail" role="tablist" aria-label="Metadata type">
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
        </div>

        <section className="manage-content" role="tabpanel">
          <div className="manage-toolbar">
            <div>
              <div className="ui-eyebrow">{section === 'tags' ? 'Personal taxonomy' : 'Shared identities'}</div>
              <h2 className="manage-title">{section === 'tags' ? 'Saved tags' : 'Known authors'}</h2>
            </div>
            <Badge tone="neutral">{itemCount} shown</Badge>
          </div>

          <label className="manage-search">
            <Search aria-hidden="true" className="h-4 w-4" />
            <span className="sr-only">Search {section}</span>
            <input
              autoComplete="off"
              placeholder={section === 'tags' ? 'Search tags…' : 'Search authors or aliases…'}
              type="search"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
            {results.isFetching ? <span className="manage-search__status">Searching…</span> : null}
          </label>

          <p className="manage-hint">Double-click a row to edit it, or use the Edit button.</p>

          {results.isError ? (
            <div className="manage-state manage-state--error">Could not load {section}. Try again.</div>
          ) : results.isPending ? (
            <div className="manage-state">Loading {section}…</div>
          ) : itemCount === 0 ? (
            <div className="manage-state">
              <span>No {section} found.</span>
              <small>{search.trim() ? 'Try a broader search.' : 'Items appear here when they are added to a book.'}</small>
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

function TagList({ tags, onEdit }: { tags: TagDto[]; onEdit: (tag: TagDto) => void }) {
  return (
    <div className="manage-list" aria-label="Tags">
      {tags.map((tag) => (
        <ManageRow key={tag.id} label={`Edit tag ${tag.name}`} onEdit={() => onEdit(tag)}>
          <span className="manage-item__icon"><Tags className="h-4 w-4" /></span>
          <span className="manage-item__body">
            <strong>{tag.name}</strong>
            <small>{tag.description || 'No description yet'}</small>
          </span>
        </ManageRow>
      ))}
    </div>
  )
}

function AuthorList({ authors, onEdit }: { authors: AuthorDto[]; onEdit: (author: AuthorDto) => void }) {
  return (
    <div className="manage-list" aria-label="Authors">
      {authors.map((author) => (
        <ManageRow key={author.id} label={`Edit author ${author.primaryName}`} onEdit={() => onEdit(author)}>
          <span className="manage-item__icon"><Users className="h-4 w-4" /></span>
          <span className="manage-item__body">
            <strong>{author.primaryName}</strong>
            <small>{author.otherNames.length > 0 ? author.otherNames.join(' · ') : 'No alternative names'}</small>
          </span>
          <Badge tone={author.otherNames.length > 0 ? 'accent' : 'neutral'}>{author.otherNames.length} aliases</Badge>
        </ManageRow>
      ))}
    </div>
  )
}

function ManageRow({ children, label, onEdit }: { children: React.ReactNode; label: string; onEdit: () => void }) {
  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === 'Enter') {
      onEdit()
    }
  }

  return (
    <div className="manage-item" role="group" tabIndex={0} onDoubleClick={onEdit} onKeyDown={handleKeyDown}>
      {children}
      <button aria-label={label} className={buttonVariants.ghost} type="button" onClick={onEdit}>Edit</button>
    </div>
  )
}

function ManageDialog({ item, onClose }: { item: ManagedItem | null; onClose: () => void }) {
  const queryClient = useQueryClient()
  const titleId = useId()
  const firstFieldRef = useRef<HTMLTextAreaElement>(null)
  const [value, setValue] = useState('')
  const [saving, setSaving] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)
  useBodyScrollLock(item !== null)

  useEffect(() => {
    if (!item) {
      return
    }

    setValue(item.kind === 'tag' ? item.value.description ?? '' : item.value.otherNames.join('\n'))
    setConfirmDelete(false)
    window.setTimeout(() => firstFieldRef.current?.focus(), 0)
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

  const name = item.kind === 'tag' ? item.value.name : item.value.primaryName
  const section = item.kind === 'tag' ? 'tags' : 'authors'

  async function handleSave(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    try {
      if (item?.kind === 'tag') {
        await api.updateTag(item.value.id, { description: value.trim() || null })
        toast.success(`Tag “${item.value.name}” updated.`)
      } else if (item?.kind === 'author') {
        const otherNames = value.split('\n').map((alias) => alias.trim()).filter(Boolean)
        await api.updateAuthor(item.value.id, { otherNames })
        toast.success(`Author “${item.value.primaryName}” updated.`)
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
      if (item?.kind === 'tag') {
        await api.deleteTag(item.value.id)
      } else if (item?.kind === 'author') {
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

  return (
    <div aria-labelledby={titleId} aria-modal="true" className="manage-dialog" role="dialog" onMouseDown={onClose}>
      <DialogPanel className="manage-dialog__panel" onMouseDown={(event) => event.stopPropagation()}>
        <div className="manage-dialog__header">
          <div>
            <div className="ui-eyebrow">Edit {item.kind}</div>
            <h2 id={titleId}>{name}</h2>
          </div>
          <button aria-label="Close dialog" className={`${buttonVariants.ghost} ui-icon-button`} disabled={saving} type="button" onClick={onClose}>
            <X className="h-4 w-4" />
          </button>
        </div>

        <form className="manage-dialog__form" onSubmit={handleSave}>
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
              <small>One name per line. The primary name stays unchanged.</small>
            </label>
          )}

          <div className="manage-dialog__actions">
            <button
              className={cn(buttonVariants.destructive, confirmDelete && 'manage-delete--confirm')}
              disabled={saving}
              type="button"
              onClick={handleDelete}
            >
              <Trash2 className="h-4 w-4" />
              {confirmDelete ? 'Confirm delete' : `Delete ${item.kind}`}
            </button>
            <span className="manage-dialog__action-spacer" />
            <button className={buttonVariants.secondary} disabled={saving} type="button" onClick={onClose}>Cancel</button>
            <button className={buttonVariants.primary} disabled={saving} type="submit">{saving ? 'Saving…' : 'Save changes'}</button>
          </div>
          {confirmDelete ? <p className="manage-delete-note">This cannot be undone. Items still used by books cannot be deleted.</p> : null}
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
