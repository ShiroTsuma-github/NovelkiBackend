import { useQuery, useQueryClient } from '@tanstack/react-query'
import { BookType, CircleDot, LibraryBig, Plus, Search, Tags, Trash2, X } from 'lucide-react'
import { useEffect, useId, useRef, useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { api } from '@/api/client'
import { HttpError } from '@/api/http'
import type { DictionaryMutationRequest, TagDto } from '@/api/types'
import { Badge, buttonVariants, controlClass, DialogPanel, Surface, useBodyScrollLock } from '@/components/app/DesignSystem'
import { cn } from '@/lib/utils'

type MetadataKind = 'statuses' | 'types' | 'genres' | 'tags'
type MetadataItem = { id: string; name: string; description?: string | null }

const sections: Array<{ kind: MetadataKind; label: string; singular: string; icon: typeof CircleDot }> = [
  { kind: 'statuses', label: 'Statuses', singular: 'status', icon: CircleDot },
  { kind: 'types', label: 'Types', singular: 'type', icon: BookType },
  { kind: 'genres', label: 'Genres', singular: 'genre', icon: LibraryBig },
  { kind: 'tags', label: 'Global tags', singular: 'global tag', icon: Tags },
]

export function AdminMetadataManager() {
  const [kind, setKind] = useState<MetadataKind>('statuses')
  const [search, setSearch] = useState('')
  const [editor, setEditor] = useState<MetadataItem | 'create' | null>(null)
  const query = useQuery({
    queryKey: ['admin-metadata', kind],
    queryFn: () => loadItems(kind),
  })
  const section = sections.find(item => item.kind === kind)!
  const normalizedSearch = search.trim().toLocaleLowerCase()
  const items = (query.data ?? []).filter(item =>
    !normalizedSearch || item.name.toLocaleLowerCase().includes(normalizedSearch) ||
    item.description?.toLocaleLowerCase().includes(normalizedSearch))

  return (
    <Surface className="manage-workspace">
      <div className="manage-rail" role="tablist" aria-label="Administrative metadata type">
        {sections.map(({ kind: sectionKind, label, icon: Icon }) => (
          <button
            key={sectionKind}
            aria-selected={kind === sectionKind}
            className={cn('manage-tab', kind === sectionKind && 'manage-tab--active')}
            role="tab"
            type="button"
            onClick={() => { setKind(sectionKind); setSearch('') }}
          >
            <Icon className="h-4 w-4" />
            <span>{label}</span>
          </button>
        ))}
      </div>

      <section className="manage-content" role="tabpanel">
        <div className="manage-toolbar">
          <div>
            <div className="ui-eyebrow">Global library metadata</div>
            <h2 className="manage-title">{section.label}</h2>
          </div>
          <div className="flex items-center gap-2">
            <Badge>{items.length} shown</Badge>
            <button className={buttonVariants.primary} type="button" onClick={() => setEditor('create')}>
              <Plus className="h-4 w-4" /> Add {section.singular}
            </button>
          </div>
        </div>

        <label className="manage-search">
          <Search aria-hidden="true" className="h-4 w-4" />
          <span className="sr-only">Search {section.label}</span>
          <input placeholder={`Search ${section.label.toLocaleLowerCase()}…`} value={search} onChange={event => setSearch(event.target.value)} />
        </label>
        <p className="manage-hint">Double-click a row to edit it, or use the Edit button.</p>

        {query.isPending ? <div className="manage-state">Loading {section.label.toLocaleLowerCase()}…</div> :
          query.isError ? <div className="manage-state manage-state--error">Could not load metadata.</div> :
            items.length === 0 ? <div className="manage-state">No matching items.</div> : (
              <div className="manage-list" aria-label={section.label}>
                {items.map(item => (
                  <div key={item.id} className="manage-item" role="group" tabIndex={0} onDoubleClick={() => setEditor(item)} onKeyDown={event => event.key === 'Enter' && setEditor(item)}>
                    <span className="manage-item__icon"><section.icon className="h-4 w-4" /></span>
                    <span className="manage-item__body"><strong>{item.name}</strong><small>{item.description || 'No description'}</small></span>
                    <button aria-label={`Edit ${section.singular} ${item.name}`} className={buttonVariants.ghost} type="button" onClick={() => setEditor(item)}>Edit</button>
                  </div>
                ))}
              </div>
            )}
      </section>
      <MetadataDialog item={editor} kind={kind} singular={section.singular} onClose={() => setEditor(null)} />
    </Surface>
  )
}

function MetadataDialog({ item, kind, singular, onClose }: { item: MetadataItem | 'create' | null; kind: MetadataKind; singular: string; onClose: () => void }) {
  const queryClient = useQueryClient()
  const titleId = useId()
  const nameRef = useRef<HTMLInputElement>(null)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [saving, setSaving] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)
  useBodyScrollLock(item !== null)

  useEffect(() => {
    if (!item) return
    setName(item === 'create' ? '' : item.name)
    setDescription(item === 'create' ? '' : item.description ?? '')
    setConfirmDelete(false)
    window.setTimeout(() => nameRef.current?.focus(), 0)
  }, [item])

  if (!item) return null
  const isCreate = item === 'create'
  const existingItem = item === 'create' ? null : item

  async function save(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    try {
      const request = { name: name.trim(), description: description.trim() || null }
      if (isCreate) await createItem(kind, request)
      else await updateItem(kind, existingItem!.id, request)
      await queryClient.invalidateQueries({ queryKey: ['admin-metadata', kind] })
      toast.success(`${capitalize(singular)} ${isCreate ? 'created' : 'updated'}.`)
      onClose()
    } catch (error) {
      toast.error(errorMessage(error))
    } finally { setSaving(false) }
  }

  async function remove() {
    if (!confirmDelete) { setConfirmDelete(true); return }
    if (isCreate) return
    setSaving(true)
    try {
      await deleteItem(kind, existingItem!.id)
      await queryClient.invalidateQueries({ queryKey: ['admin-metadata', kind] })
      toast.success(`${capitalize(singular)} deleted.`)
      onClose()
    } catch (error) {
      toast.error(errorMessage(error))
      setConfirmDelete(false)
    } finally { setSaving(false) }
  }

  return (
    <div aria-labelledby={titleId} aria-modal="true" className="manage-dialog" role="dialog" onMouseDown={onClose}>
      <DialogPanel className="manage-dialog__panel" onMouseDown={event => event.stopPropagation()}>
        <div className="manage-dialog__header">
          <div><div className="ui-eyebrow">{isCreate ? 'Create' : 'Edit'} {singular}</div><h2 id={titleId}>{isCreate ? `New ${singular}` : item.name}</h2></div>
          <button aria-label="Close dialog" className={`${buttonVariants.ghost} ui-icon-button`} type="button" onClick={onClose}><X className="h-4 w-4" /></button>
        </div>
        <form className="manage-dialog__form" onSubmit={save}>
          <label><span>Name</span><input ref={nameRef} aria-label="Name" className={controlClass} required value={name} onChange={event => setName(event.target.value)} /></label>
          <label><span>Description</span><textarea aria-label="Description" className={controlClass} maxLength={500} rows={6} value={description} onChange={event => setDescription(event.target.value)} /></label>
          <div className="manage-dialog__actions">
            {!isCreate ? <button className={cn(buttonVariants.destructive, confirmDelete && 'manage-delete--confirm')} disabled={saving} type="button" onClick={remove}><Trash2 className="h-4 w-4" />{confirmDelete ? 'Confirm delete' : `Delete ${singular}`}</button> : null}
            <span className="manage-dialog__action-spacer" />
            <button className={buttonVariants.secondary} type="button" onClick={onClose}>Cancel</button>
            <button className={buttonVariants.primary} disabled={saving || !name.trim()} type="submit">{saving ? 'Saving…' : isCreate ? `Create ${singular}` : 'Save changes'}</button>
          </div>
        </form>
      </DialogPanel>
    </div>
  )
}

async function loadItems(kind: MetadataKind): Promise<MetadataItem[]> {
  if (kind === 'statuses') return (await api.getStatuses()).data
  if (kind === 'types') return (await api.getTypes()).data
  if (kind === 'genres') return (await api.getGenres()).data
  return (await api.searchAdminGlobalTags()).map(fromTag)
}

function createItem(kind: MetadataKind, request: DictionaryMutationRequest) {
  if (kind === 'statuses') return api.createAdminStatus(request)
  if (kind === 'types') return api.createAdminType(request)
  if (kind === 'genres') return api.createAdminGenre(request)
  return api.createAdminGlobalTag(request)
}

function updateItem(kind: MetadataKind, id: string, request: DictionaryMutationRequest) {
  if (kind === 'statuses') return api.updateAdminStatus(id, request)
  if (kind === 'types') return api.updateAdminType(id, request)
  if (kind === 'genres') return api.updateAdminGenre(id, request)
  return api.updateAdminGlobalTag(id, request)
}

function deleteItem(kind: MetadataKind, id: string) {
  if (kind === 'statuses') return api.deleteAdminStatus(id)
  if (kind === 'types') return api.deleteAdminType(id)
  if (kind === 'genres') return api.deleteAdminGenre(id)
  return api.deleteAdminGlobalTag(id)
}

function fromTag(tag: TagDto): MetadataItem { return { id: tag.id, name: tag.name, description: tag.description } }
function capitalize(value: string) { return value.charAt(0).toUpperCase() + value.slice(1) }
function errorMessage(error: unknown) { return error instanceof HttpError ? error.apiError.detail : error instanceof Error ? error.message : 'Operation failed.' }
