import { BarChart3, BookOpen, LogOut, Plus, Search, Settings2, Shield } from 'lucide-react'
import { Outlet, NavLink, useNavigate } from 'react-router-dom'
import { useAuth } from '@/features/auth/AuthProvider'
import { buttonVariants } from '@/components/app/DesignSystem'
import { cn } from '@/lib/utils'

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

  async function handleLogout() {
    await logout()
    navigate('/login', { replace: true })
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
      </header>
      <main className="app-main" id="main-content">
        <Outlet />
      </main>
    </div>
  )
}
