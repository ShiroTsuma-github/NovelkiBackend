import { apiBlobRequest, apiFormRequest, apiRequest, toQueryString } from './http'
import type {
  AuthorDto,
  AdminLibraryPurgeResult,
  AdminBookDto,
  AdminBookListItemDto,
  BookDto,
  BookListItemDto,
  BookCoverDto,
  BookImportFinalizeResult,
  BookImportRowUpdateRequest,
  BookImportSessionDto,
  BookMutationRequest,
  DictionaryMutationRequest,
  DictionaryDto,
  BookSummaryDto,
  LoginRequest,
  PaginatedResult,
  RegisterRequest,
  RegisterResponse,
  TagDto,
  TokenResponse,
  UpdateProgressRequest,
} from './types'

export const api = {
  login: (request: LoginRequest) =>
    apiRequest<TokenResponse>('/account/login', {
      method: 'POST',
      body: request,
      token: null,
    }),
  refresh: (refreshToken: string) =>
    apiRequest<TokenResponse>('/account/refresh', {
      method: 'POST',
      body: { refreshToken },
      token: null,
    }),
  logout: (refreshToken: string | null) =>
    apiRequest<void>('/account/logout', {
      method: 'POST',
      body: { refreshToken },
      token: null,
    }),
  register: (request: RegisterRequest) =>
    apiRequest<RegisterResponse>('/account/register', {
      method: 'POST',
      body: request,
      token: null,
    }),
  getBooks: (params: { skip?: number; take?: number; query?: string; sortBy?: string; sortDirection?: string; advanceCycle?: boolean }) =>
    apiRequest<PaginatedResult<BookListItemDto>>(`/book${toQueryString(params)}`),
  getBooksSummary: (params: { query?: string }) =>
    apiRequest<BookSummaryDto>(`/book/summary${toQueryString(params)}`),
  getBook: (id: string) => apiRequest<BookDto>(`/book/${id}`),
  getAdminBooks: (params: { skip?: number; take?: number; query?: string; sortBy?: string; sortDirection?: string }) =>
    apiRequest<PaginatedResult<AdminBookListItemDto>>(`/admin/books${toQueryString(params)}`),
  getAdminBook: (id: string) => apiRequest<AdminBookDto>(`/admin/books/${id}`),
  createBook: (request: BookMutationRequest) =>
    apiRequest<{ id: string }>('/book', { method: 'POST', body: request }),
  updateBook: (id: string, request: BookMutationRequest) =>
    apiRequest<void>(`/book/${id}`, { method: 'PUT', body: request }),
  createBookImportSession: (file: File) => {
    const formData = new FormData()
    formData.set('file', file)
    return apiFormRequest<BookImportSessionDto>('/book/import/sessions', formData, { method: 'POST' })
  },
  downloadBookImportTemplate: () =>
    apiBlobRequest('/book/import/template'),
  downloadBooksExport: (params: { query?: string; sortBy?: string; sortDirection?: string }) =>
    apiBlobRequest(`/book/export${toQueryString(params)}`),
  getBookImportSession: (sessionId: string) =>
    apiRequest<BookImportSessionDto>(`/book/import/sessions/${sessionId}`),
  updateBookImportRow: (sessionId: string, rowId: string, request: BookImportRowUpdateRequest) =>
    apiRequest<BookImportSessionDto>(`/book/import/sessions/${sessionId}/rows/${rowId}`, { method: 'PUT', body: request }),
  deleteBookImportRow: (sessionId: string, rowId: string) =>
    apiRequest<BookImportSessionDto>(`/book/import/sessions/${sessionId}/rows/${rowId}`, { method: 'DELETE' }),
  deleteInvalidBookImportRows: (sessionId: string) =>
    apiRequest<BookImportSessionDto>(`/book/import/sessions/${sessionId}/rows/invalid`, { method: 'DELETE' }),
  finalizeBookImport: (sessionId: string) =>
    apiRequest<BookImportFinalizeResult>(`/book/import/sessions/${sessionId}/finalize`, { method: 'POST' }),
  cancelBookImport: (sessionId: string) =>
    apiRequest<void>(`/book/import/sessions/${sessionId}`, { method: 'DELETE' }),
  updateAdminBook: (id: string, request: BookMutationRequest) =>
    apiRequest<void>(`/admin/books/${id}`, { method: 'PUT', body: request }),
  updateProgress: (id: string, request: UpdateProgressRequest) =>
    apiRequest<void>(`/book/${id}/progress`, { method: 'PATCH', body: request }),
  setBookCoverFromUrl: (id: string, imageUrl: string) =>
    apiRequest<BookCoverDto>(`/book/${id}/cover/url`, { method: 'PUT', body: { imageUrl } }),
  uploadBookCover: (id: string, file: File) => {
    const formData = new FormData()
    formData.set('file', file)
    return apiFormRequest<BookCoverDto>(`/book/${id}/cover`, formData, { method: 'PUT' })
  },
  refreshBookCover: (id: string) =>
    apiRequest<BookCoverDto>(`/book/${id}/cover/refresh`, { method: 'POST' }),
  deleteBookCover: (id: string) =>
    apiRequest<void>(`/book/${id}/cover`, { method: 'DELETE' }),
  deleteBook: (id: string) =>
    apiRequest<void>(`/book/${id}`, { method: 'DELETE' }),
  searchAuthors: (search: string, take = 10) =>
    apiRequest<AuthorDto[]>(
      `/author${toQueryString({ search, take })}`,
    ),
  searchTags: (search: string, take = 10) =>
    apiRequest<TagDto[]>(`/tag${toQueryString({ search, take })}`),
  getTypes: () =>
    apiRequest<PaginatedResult<DictionaryDto>>(
      `/type${toQueryString({ skip: 0, take: 100 })}`,
    ),
  getStatuses: () =>
    apiRequest<PaginatedResult<DictionaryDto>>(
      `/status${toQueryString({ skip: 0, take: 100 })}`,
    ),
  getGenres: () =>
    apiRequest<PaginatedResult<DictionaryDto>>(
      `/genre${toQueryString({ skip: 0, take: 100 })}`,
    ),
  createAdminStatus: (request: DictionaryMutationRequest) =>
    apiRequest<DictionaryDto>('/admin/statuses', { method: 'POST', body: request }),
  createAdminType: (request: DictionaryMutationRequest) =>
    apiRequest<DictionaryDto>('/admin/types', { method: 'POST', body: request }),
  createAdminGenre: (request: DictionaryMutationRequest) =>
    apiRequest<DictionaryDto>('/admin/genres', { method: 'POST', body: request }),
  deleteAdminBooksByOwner: (ownerId: string) =>
    apiRequest<AdminLibraryPurgeResult>(`/admin/books/owner/${ownerId}`, { method: 'DELETE' }),
}
