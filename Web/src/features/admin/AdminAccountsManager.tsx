import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Search, Trash2, UserRound, X } from 'lucide-react'
import { useEffect, useId, useState } from 'react'
import { toast } from 'sonner'
import { api } from '@/api/client'
import { getStoredSessionUserId, HttpError } from '@/api/http'
import type { AdminUserDto } from '@/api/types'
import { Badge, buttonVariants, DialogPanel, Surface, useBodyScrollLock } from '@/components/app/DesignSystem'

export function AdminAccountsManager() {
  const [search, setSearch] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [selected, setSelected] = useState<AdminUserDto | null>(null)
  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedSearch(search.trim()), 180)
    return () => window.clearTimeout(timer)
  }, [search])
  const users = useQuery({
    queryKey: ['admin-users', debouncedSearch],
    queryFn: () => api.getAdminUsers({ skip: 0, take: 100, search: debouncedSearch }),
  })

  return (
    <Surface className="manage-content">
      <div className="manage-toolbar">
        <div><div className="ui-eyebrow">Identity administration</div><h2 className="manage-title">User accounts</h2></div>
        <Badge>{users.data?.total ?? 0} accounts</Badge>
      </div>
      <label className="manage-search">
        <Search aria-hidden="true" className="h-4 w-4" />
        <span className="sr-only">Search user accounts</span>
        <input placeholder="Search by username or email…" value={search} onChange={event => setSearch(event.target.value)} />
        {users.isFetching ? <span className="manage-search__status">Searching…</span> : null}
      </label>
      <p className="manage-hint">Deleting an account also deletes its books, private tags, sessions, and unused authors.</p>

      {users.isPending ? <div className="manage-state">Loading accounts…</div> :
        users.isError ? <div className="manage-state manage-state--error">Could not load accounts.</div> :
          users.data!.data.length === 0 ? <div className="manage-state">No accounts found.</div> : (
            <div className="manage-list" aria-label="User accounts">
              {users.data!.data.map(user => {
                const isCurrent = user.id === getStoredSessionUserId()
                return (
                  <div key={user.id} className="manage-item" role="group">
                    <span className="manage-item__icon"><UserRound className="h-4 w-4" /></span>
                    <span className="manage-item__body"><strong>{user.username || 'Unnamed user'}</strong><small>{user.email || user.id}</small></span>
                    <Badge>{user.booksCount} books</Badge>
                    <Badge>{user.tagsCount} tags</Badge>
                    {isCurrent ? <Badge tone="accent">Current admin</Badge> : (
                      <button aria-label={`Delete account ${user.username || user.email || user.id}`} className={buttonVariants.destructive} type="button" onClick={() => setSelected(user)}>Delete</button>
                    )}
                  </div>
                )
              })}
            </div>
          )}
      <DeleteAccountDialog user={selected} onClose={() => setSelected(null)} />
    </Surface>
  )
}

function DeleteAccountDialog({ user, onClose }: { user: AdminUserDto | null; onClose: () => void }) {
  const titleId = useId()
  const queryClient = useQueryClient()
  const [deleting, setDeleting] = useState(false)
  useBodyScrollLock(user !== null)
  if (!user) return null

  async function remove() {
    setDeleting(true)
    try {
      const result = await api.deleteAdminUser(user!.id)
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['admin-users'] }),
        queryClient.invalidateQueries({ queryKey: ['adminBooks'] }),
      ])
      toast.success(`Account deleted with ${result.deletedBooks} books, ${result.deletedTags} tags, and ${result.deletedAuthors} authors.`)
      onClose()
    } catch (error) {
      toast.error(error instanceof HttpError ? error.apiError.detail : error instanceof Error ? error.message : 'Account could not be deleted.')
    } finally { setDeleting(false) }
  }

  return (
    <div aria-labelledby={titleId} aria-modal="true" className="manage-dialog" role="dialog" onMouseDown={onClose}>
      <DialogPanel className="manage-dialog__panel" onMouseDown={event => event.stopPropagation()}>
        <div className="manage-dialog__header">
          <div><div className="ui-eyebrow">Destructive action</div><h2 id={titleId}>Delete {user.username || user.email || 'account'}?</h2></div>
          <button aria-label="Close dialog" className={`${buttonVariants.ghost} ui-icon-button`} disabled={deleting} type="button" onClick={onClose}><X className="h-4 w-4" /></button>
        </div>
        <div className="manage-dialog__form">
          <p className="text-sm text-[var(--qs-muted)]">This permanently removes the account, {user.booksCount} books, {user.tagsCount} private tags, authentication sessions, and authors no longer used elsewhere.</p>
          <div className="manage-dialog__actions">
            <span className="manage-dialog__action-spacer" />
            <button className={buttonVariants.secondary} disabled={deleting} type="button" onClick={onClose}>Cancel</button>
            <button className={buttonVariants.destructive} disabled={deleting} type="button" onClick={remove}><Trash2 className="h-4 w-4" />{deleting ? 'Deleting…' : 'Delete account permanently'}</button>
          </div>
        </div>
      </DialogPanel>
    </div>
  )
}
