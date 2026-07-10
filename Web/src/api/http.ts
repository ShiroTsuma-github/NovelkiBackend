import type { ApiError } from './types'
import type { TokenResponse } from './types'

export const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? '/api/v1'

export const sessionStorageKey = 'novelki.session'

type StoredSession = TokenResponse

const unauthorizedListeners = new Set<() => void>()
let refreshRequest: Promise<string | null> | null = null

export class HttpError extends Error {
  readonly apiError: ApiError

  constructor(apiError: ApiError) {
    super(apiError.detail || apiError.title)
    this.name = 'HttpError'
    this.apiError = apiError
  }
}

type RequestOptions = Omit<RequestInit, 'body'> & {
  body?: unknown
  token?: string | null
}

export async function apiRequest<T>(
  path: string,
  options: RequestOptions = {},
): Promise<T> {
  return requestWithBody<T>(path, options, options.body === undefined ? undefined : JSON.stringify(options.body), true)
}

export async function apiFormRequest<T>(
  path: string,
  formData: FormData,
  options: Omit<RequestInit, 'body'> & { token?: string | null } = {},
): Promise<T> {
  return requestWithBody<T>(path, options, formData, true)
}

async function requestWithBody<T>(
  path: string,
  options: Omit<RequestInit, 'body'> & { body?: unknown; token?: string | null },
  body: BodyInit | undefined,
  allowRefresh: boolean,
): Promise<T> {
  const headers = new Headers(options.headers)
  const token = options.token ?? getStoredSession()?.accessToken ?? null

  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  if (options.body !== undefined && !(body instanceof FormData)) {
    headers.set('Content-Type', 'application/json')
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers,
    body,
  })

  if (response.status === 401 && allowRefresh && shouldTryRefresh(path)) {
    const refreshedAccessToken = await refreshSession()
    if (refreshedAccessToken) {
      return requestWithBody<T>(path, { ...options, token: refreshedAccessToken }, body, false)
    }
  }

  if (response.status === 204) {
    return undefined as T
  }

  const text = await response.text()
  const data = text ? JSON.parse(text) : undefined

  if (!response.ok) {
    throw new HttpError(normalizeApiError(data, response.status, path))
  }

  return data as T
}

function normalizeApiError(data: unknown, status: number, path: string): ApiError {
  if (isApiError(data)) {
    return data
  }

  return {
    type: 'HttpError',
    title: 'Request failed',
    status,
    detail: `Request failed with status ${status}.`,
    instance: path,
  }
}

function isApiError(value: unknown): value is ApiError {
  return (
    typeof value === 'object' &&
    value !== null &&
    'title' in value &&
    'status' in value &&
    'detail' in value
  )
}

export function toQueryString(params: Record<string, string | number | undefined>) {
  const query = new URLSearchParams()
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== '') {
      query.set(key, String(value))
    }
  })
  const serialized = query.toString()
  return serialized ? `?${serialized}` : ''
}

export function getStoredSession(): StoredSession | null {
  const raw = localStorage.getItem(sessionStorageKey)
  if (!raw) {
    return null
  }

  try {
    return JSON.parse(raw) as StoredSession
  } catch {
    localStorage.removeItem(sessionStorageKey)
    return null
  }
}

export function setStoredSession(session: StoredSession) {
  localStorage.setItem(sessionStorageKey, JSON.stringify(session))
}

export function clearStoredSession() {
  localStorage.removeItem(sessionStorageKey)
}

export function subscribeUnauthorized(listener: () => void) {
  unauthorizedListeners.add(listener)
  return () => {
    unauthorizedListeners.delete(listener)
  }
}

async function refreshSession() {
  if (refreshRequest) {
    return refreshRequest
  }

  refreshRequest = performRefresh()
  try {
    return await refreshRequest
  } finally {
    refreshRequest = null
  }
}

async function performRefresh() {
  const session = getStoredSession()
  if (!session?.refreshToken) {
    clearStoredSession()
    notifyUnauthorized()
    return null
  }

  try {
    const response = await fetch(`${API_BASE_URL}/account/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: session.refreshToken }),
    })
    if (!response.ok) {
      clearStoredSession()
      notifyUnauthorized()
      return null
    }

    const refreshedSession = await response.json() as StoredSession
    setStoredSession(refreshedSession)
    return refreshedSession.accessToken
  } catch {
    clearStoredSession()
    notifyUnauthorized()
    return null
  }
}

function notifyUnauthorized() {
  unauthorizedListeners.forEach((listener) => listener())
}

function shouldTryRefresh(path: string) {
  return !path.startsWith('/account/login') &&
    !path.startsWith('/account/register') &&
    !path.startsWith('/account/refresh') &&
    !path.startsWith('/account/logout')
}
