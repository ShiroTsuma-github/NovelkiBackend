import { useQueryClient } from '@tanstack/react-query'
import { BarChart3, BookOpen, Compass, LogOut, Plus, Search, Settings2, Shield, TriangleAlert } from 'lucide-react'
import { useState } from 'react'
import { Outlet, NavLink, useNavigate } from 'react-router-dom'
import { useAuth } from '@/features/auth/AuthProvider'
import { buttonVariants } from '@/components/app/DesignSystem'
import { cn } from '@/lib/utils'
import { isNsfwEnabled, setNsfwEnabled } from '@/lib/nsfwPreference'

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  cn(
    'app-nav-link',
    isActive
      ? 'app-nav-link--active'
      : 'app-nav-link--idle',
  )

export function AppShell() {
  const { isAdmin, logout } = useAuth()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [nsfwEnabled, setNsfwEnabledState] = useState(isNsfwEnabled)

  async function handleLogout() {
    await logout()
    navigate('/login', { replace: true })
  }

  function handleNsfwToggle() {
    const nextEnabled = !nsfwEnabled
    setNsfwEnabledState(setNsfwEnabled(nextEnabled))
    void queryClient.invalidateQueries()
  }

  return (
    <div className="app-frame">
      <a className="skip-link" href="#main-content">Skip to content</a>
      <header className="app-header">
        <div className="app-header__inner">
          <div className="app-brand">
            <div className="app-brand__mark">
              <BookOpen className="h-5 w-5" />
            </div>
            <div className="min-w-0">
              <div className="app-brand__name">Novelki</div>
              <div className="app-brand__meta">Personal library system</div>
            </div>
          </div>
          <nav aria-label="Primary navigation" className="app-nav">
            <NavLink className={navLinkClass} end to="/books">
              <Search className="h-4 w-4" />
              Books
            </NavLink>
            <NavLink className={navLinkClass} to="/analytics">
              <BarChart3 className="h-4 w-4" />
              Analytics
            </NavLink>
            <NavLink className={navLinkClass} to="/books/new">
              <Plus className="h-4 w-4" />
              Add
            </NavLink>
            <NavLink className={navLinkClass} to="/discover">
              <Compass className="h-4 w-4" />
              Discover
            </NavLink>
            <NavLink className={navLinkClass} to="/manage">
              <Settings2 className="h-4 w-4" />
              Manage
            </NavLink>
            {isAdmin ? (
              <NavLink className={navLinkClass} to="/admin">
                <Shield className="h-4 w-4" />
                Admin
              </NavLink>
            ) : null}
          </nav>
          <div className="app-session-actions">
            <button
              aria-label={nsfwEnabled ? 'Disable NSFW content' : 'Enable NSFW content'}
              aria-pressed={nsfwEnabled}
              className={cn(buttonVariants.ghost, 'app-nsfw-toggle', nsfwEnabled && 'app-nsfw-toggle--enabled')}
              type="button"
              onClick={handleNsfwToggle}
            >
              <TriangleAlert aria-hidden="true" className="h-4 w-4" />
              <span className="app-nsfw-toggle__label">NSFW</span>
            </button>
            <button
              aria-label="Log out"
              className={cn(buttonVariants.ghost, 'app-logout')}
              type="button"
              onClick={handleLogout}
            >
              <LogOut className="h-4 w-4" />
              <span className="app-logout__label">Log out</span>
            </button>
          </div>
        </div>
      </header>
      <main className="app-main" id="main-content">
        <Outlet />
      </main>
    </div>
  )
}
