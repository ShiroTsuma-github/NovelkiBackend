import { BookOpen, LogOut, Plus, Search, Shield } from 'lucide-react'
import { Outlet, NavLink, useNavigate } from 'react-router-dom'
import { useAuth } from '@/features/auth/AuthProvider'
import { cn } from '@/lib/utils'

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  cn(
    'inline-flex items-center gap-2 rounded-md px-3 py-2 text-sm font-medium transition',
    isActive
      ? 'bg-cyan-500 text-slate-950'
      : 'text-slate-300 hover:bg-slate-800 hover:text-slate-50',
  )

export function AppShell() {
  const { isAdmin, logout } = useAuth()
  const navigate = useNavigate()

  function handleLogout() {
    logout()
    navigate('/login', { replace: true })
  }

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-7xl items-center justify-between px-4 py-3 sm:px-6 lg:px-8">
          <div className="flex items-center gap-3">
            <div className="flex h-9 w-9 items-center justify-center rounded-md bg-cyan-500 text-slate-950">
              <BookOpen className="h-5 w-5" />
            </div>
            <div>
              <div className="text-base font-semibold text-slate-950">Novelki</div>
              <div className="text-xs text-slate-500">Reading library</div>
            </div>
          </div>
          <nav className="hidden items-center gap-2 md:flex">
            <NavLink className={navLinkClass} to="/books">
              <Search className="h-4 w-4" />
              Books
            </NavLink>
            <NavLink className={navLinkClass} to="/books/new">
              <Plus className="h-4 w-4" />
              Add
            </NavLink>
            {isAdmin ? (
              <NavLink className={navLinkClass} to="/admin">
                <Shield className="h-4 w-4" />
                Admin
              </NavLink>
            ) : null}
          </nav>
          <button
            className="inline-flex items-center gap-2 rounded-md border border-slate-700 bg-slate-900 px-3 py-2 text-sm font-medium text-slate-200 hover:bg-slate-800"
            type="button"
            onClick={handleLogout}
          >
            <LogOut className="h-4 w-4" />
            Log out
          </button>
        </div>
      </header>
      <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
        <Outlet />
      </main>
    </div>
  )
}
