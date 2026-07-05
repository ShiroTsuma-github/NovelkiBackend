import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import { tokenStorageKey } from '@/api/http'

type AuthContextValue = {
  token: string | null
  isAuthenticated: boolean
  roles: string[]
  isAdmin: boolean
  setSession: (token: string) => void
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(() =>
    localStorage.getItem(tokenStorageKey),
  )

  const setSession = useCallback((nextToken: string) => {
    localStorage.setItem(tokenStorageKey, nextToken)
    setToken(nextToken)
  }, [])

  const logout = useCallback(() => {
    localStorage.removeItem(tokenStorageKey)
    setToken(null)
  }, [])

  const value = useMemo(
    () => {
      const roles = readRoles(token)
      return {
        token,
        isAuthenticated: Boolean(token),
        roles,
        isAdmin: roles.includes('Admin'),
        setSession,
        logout,
      }
    },
    [logout, setSession, token],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

function readRoles(token: string | null) {
  if (!token) {
    return []
  }

  try {
    const payload = JSON.parse(atob(toBase64(token.split('.')[1] ?? ''))) as Record<string, unknown>
    const roleClaim =
      payload.role ??
      payload.roles ??
      payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
    return Array.isArray(roleClaim)
      ? roleClaim.filter((role): role is string => typeof role === 'string')
      : typeof roleClaim === 'string'
        ? [roleClaim]
        : []
  } catch {
    return []
  }
}

function toBase64(value: string) {
  const normalized = value.replace(/-/g, '+').replace(/_/g, '/')
  return normalized.padEnd(Math.ceil(normalized.length / 4) * 4, '=')
}

export function useAuth() {
  const value = useContext(AuthContext)
  if (!value) {
    throw new Error('useAuth must be used inside AuthProvider.')
  }
  return value
}
