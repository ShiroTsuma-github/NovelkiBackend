import type { ApiError } from './types'
import type { TokenResponse } from './types'

export const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? '/api/v1'

export const sessionStorageKey = 'novelki.session'

type StoredSession = TokenResponse

const unauthorizedListeners = new Set<() => void>()
let refreshRequest: Promise<string | null> | null = null
let currentSession: StoredSession | null = null

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

export async function apiBlobRequest(
  path: string,
  options: Omit<RequestInit, 'body'> & { token?: string | null } = {},
): Promise<Blob> {
  const headers = new Headers(options.headers)
  const token = options.token ?? getStoredSession()?.accessToken ?? null

  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers,
    credentials: 'include',
  })

  if (response.status === 401 && shouldTryRefresh(path)) {
    const refreshedAccessToken = await refreshSession()
    if (refreshedAccessToken) {
      return apiBlobRequest(path, { ...options, token: refreshedAccessToken })
    }
  }

  if (!response.ok) {
    const text = await response.text()
    const data = parseResponseData(text)
    throw new HttpError(normalizeApiError(data, response.status, path))
  }

  return response.blob()
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
    credentials: 'include',
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
  const data = parseResponseData(text)

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
    detail: getDefaultErrorDetail(status),
    instance: path,
  }
}

function parseResponseData(text: string): unknown {
  if (!text) {
    return undefined
  }

  try {
    return JSON.parse(text)
  } catch {
    return undefined
  }
}

function getDefaultErrorDetail(status: number) {
  if (status === 413) {
    return 'The selected file is too large.'
  }

  if (status === 429) {
    return 'Too many requests. Please try again later.'
  }

  return `Request failed with status ${status}.`
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

export function toQueryString(params: Record<string, string | number | boolean | undefined>) {
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
  return currentSession
}

export function getStoredSessionUserId() {
  return getStoredSession()?.userId ?? null
}

export function setStoredSession(session: StoredSession) {
  currentSession = session
}

export function clearStoredSession() {
  currentSession = null
  try {
    localStorage.removeItem(sessionStorageKey)
  } catch {
    // Authentication state remains cleared even when browser storage is unavailable.
  }
}

export function subscribeUnauthorized(listener: () => void) {
  unauthorizedListeners.add(listener)
  return () => {
    unauthorizedListeners.delete(listener)
  }
}

export async function refreshSession() {
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
  try {
    const response = await fetch(`${API_BASE_URL}/account/refresh`, {
      method: 'POST',
      credentials: 'include',
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
