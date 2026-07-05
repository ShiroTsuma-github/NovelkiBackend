import type { ApiError } from './types'

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? 'https://localhost:7121/api/v1'

export const tokenStorageKey = 'novelki.accessToken'

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
  const headers = new Headers(options.headers)
  const token = options.token ?? localStorage.getItem(tokenStorageKey)

  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  if (options.body !== undefined) {
    headers.set('Content-Type', 'application/json')
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
  })

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
