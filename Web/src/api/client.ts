import { apiBlobRequest, apiFormRequest, apiRequest, toQueryString } from './http'
import type {
  AuthorDto,
  AdminLibraryPurgeResult,
  AdminUserDto,
  AdminAccountDeleteResult,
  AdminBookDto,
  AdminBookListItemDto,
  BookDto,
  BookAnalyticsDto,
  BookListItemDto,
  BookCoverDto,
  BookImportFinalizeResult,
  BookImportRowUpdateRequest,
  BookImportSessionDto,
  BookMutationRequest,
  BookHtmlParseResult,
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
  UpdateAuthorRequest,
  UpdateTagRequest,
  CreateAuthorRequest,
  CreateTagRequest,
  CopyPublicBookResult,
  PublicBookSnapshotDto,
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
  getBookAnalytics: (params: { query?: string; from?: string; to?: string; bucket?: string }) =>
    apiRequest<BookAnalyticsDto>(`/book/analytics${toQueryString(params)}`),
  getBook: (id: string) => apiRequest<BookDto>(`/book/${id}`),
  getAdminBooks: (params: { skip?: number; take?: number; query?: string; sortBy?: string; sortDirection?: string }) =>
    apiRequest<PaginatedResult<AdminBookListItemDto>>(`/admin/books${toQueryString(params)}`),
  getAdminBook: (id: string) => apiRequest<AdminBookDto>(`/admin/books/${id}`),
  createBook: (request: BookMutationRequest) =>
    apiRequest<{ id: string }>('/book', { method: 'POST', body: request }),
  parseBookHtml: (html: string) =>
    apiRequest<BookHtmlParseResult>('/book/parse-html', { method: 'POST', body: { html } }),
  updateBook: (id: string, request: BookMutationRequest) =>
    apiRequest<void>(`/book/${id}`, { method: 'PUT', body: request }),
  createBookImportSession: (file: File) => {
    const formData = new FormData()
    formData.set('file', file)
    return apiFormRequest<BookImportSessionDto>('/book/import/sessions', formData, { method: 'POST' })
  },
  createFullBookImportSession: (file: File) => {
    const formData = new FormData()
    formData.set('file', file)
    return apiFormRequest<BookImportSessionDto>('/book/import/full/sessions', formData, { method: 'POST' })
  },
  downloadBookImportTemplate: () =>
    apiBlobRequest('/book/import/template'),
  downloadBooksExport: (params: { query?: string; sortBy?: string; sortDirection?: string }) =>
    apiBlobRequest(`/book/export${toQueryString(params)}`),
  downloadBooksFullExport: (params: { query?: string; sortBy?: string; sortDirection?: string }) =>
    apiBlobRequest(`/book/export/full${toQueryString(params)}`),
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
  searchPublicBooks: (params: { search?: string; skip?: number; take?: number; mineOnly?: boolean }) =>
    apiRequest<PaginatedResult<PublicBookSnapshotDto>>(`/public-book${toQueryString(params)}`),
  publishBook: (bookId: string) =>
    apiRequest<PublicBookSnapshotDto>(`/public-book/source/${bookId}`, { method: 'POST' }),
  refreshPublishedBook: (snapshotId: string) =>
    apiRequest<PublicBookSnapshotDto>(`/public-book/${snapshotId}/refresh`, { method: 'PUT' }),
  unlistPublishedBook: (snapshotId: string) =>
    apiRequest<void>(`/public-book/${snapshotId}`, { method: 'DELETE' }),
  copyPublicBook: (snapshotId: string) =>
    apiRequest<CopyPublicBookResult>(`/public-book/${snapshotId}/copy`, { method: 'POST' }),
  searchAuthors: (search: string, take = 10, mineOnly = false) =>
    apiRequest<AuthorDto[]>(
      `/author${toQueryString({ search, take, mineOnly: mineOnly || undefined })}`,
    ),
  createAuthor: (request: CreateAuthorRequest) =>
    apiRequest<AuthorDto>('/author', { method: 'POST', body: request }),
  updateAuthor: (id: string, request: UpdateAuthorRequest) =>
    apiRequest<AuthorDto>(`/author/${id}`, { method: 'PUT', body: request }),
  updateAuthorVisibility: (id: string, isPublic: boolean) =>
    apiRequest<AuthorDto>(`/author/${id}/visibility`, { method: 'PUT', body: { isPublic } }),
  deleteAuthor: (id: string) =>
    apiRequest<void>(`/author/${id}`, { method: 'DELETE' }),
  searchTags: (search: string, take = 10) =>
    apiRequest<TagDto[]>(`/tag${toQueryString({ search, take })}`),
  createTag: (request: CreateTagRequest) =>
    apiRequest<TagDto>('/tag', { method: 'POST', body: request }),
  updateTag: (id: string, request: UpdateTagRequest) =>
    apiRequest<TagDto>(`/tag/${id}`, { method: 'PUT', body: request }),
  deleteTag: (id: string) =>
    apiRequest<void>(`/tag/${id}`, { method: 'DELETE' }),
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
  updateAdminStatus: (id: string, request: DictionaryMutationRequest) =>
    apiRequest<DictionaryDto>(`/status/${id}`, { method: 'PUT', body: request }),
  deleteAdminStatus: (id: string) => apiRequest<void>(`/status/${id}`, { method: 'DELETE' }),
  updateAdminType: (id: string, request: DictionaryMutationRequest) =>
    apiRequest<DictionaryDto>(`/type/${id}`, { method: 'PUT', body: request }),
  deleteAdminType: (id: string) => apiRequest<void>(`/type/${id}`, { method: 'DELETE' }),
  updateAdminGenre: (id: string, request: DictionaryMutationRequest) =>
    apiRequest<DictionaryDto>(`/genre/${id}`, { method: 'PUT', body: request }),
  deleteAdminGenre: (id: string) => apiRequest<void>(`/genre/${id}`, { method: 'DELETE' }),
  searchAdminGlobalTags: (search = '', take = 100) =>
    apiRequest<TagDto[]>(`/admin/tags${toQueryString({ search, take })}`),
  createAdminGlobalTag: (request: DictionaryMutationRequest) =>
    apiRequest<TagDto>('/admin/tags', { method: 'POST', body: request }),
  updateAdminGlobalTag: (id: string, request: DictionaryMutationRequest) =>
    apiRequest<TagDto>(`/admin/tags/${id}`, { method: 'PUT', body: request }),
  deleteAdminGlobalTag: (id: string) => apiRequest<void>(`/admin/tags/${id}`, { method: 'DELETE' }),
  deleteAdminBooksByOwner: (ownerId: string) =>
    apiRequest<AdminLibraryPurgeResult>(`/admin/books/owner/${ownerId}`, { method: 'DELETE' }),
  getAdminUsers: (params: { skip?: number; take?: number; search?: string }) =>
    apiRequest<PaginatedResult<AdminUserDto>>(`/admin/users${toQueryString(params)}`),
  deleteAdminUser: (userId: string) =>
    apiRequest<AdminAccountDeleteResult>(`/admin/users/${userId}`, { method: 'DELETE' }),
}
