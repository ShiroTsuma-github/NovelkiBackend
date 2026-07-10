export type PaginatedResult<T> = {
  skip: number
  take: number
  total: number
  data: T[]
}

export type ApiError = {
  type: string
  title: string
  status: number
  detail: string
  instance: string
  errors?: Record<string, string[]>
}

export type TokenResponse = {
  accessToken: string
  refreshToken: string
  tokenType: string
  expiresAt: string
  refreshTokenExpiresAt: string
  userId: string
}

export type RegisterResponse = {
  id: string
  name: string
  createdAt: string
}

export type LoginRequest = {
  username?: string | null
  email?: string | null
  password: string
}

export type RegisterRequest = {
  username: string
  email: string
  password: string
}

export type BookDto = {
  id: string
  created: string
  lastModified: string
  primaryTitle: string
  description?: string | null
  alternativeTitles: string[]
  authorId?: string | null
  author?: string | null
  contentType: string
  status: string
  currentChapterNumber?: number | null
  currentChapterLabel?: string | null
  totalChapters?: number | null
  rating?: number | null
  priority?: number | null
  comment?: string | null
  notes?: string | null
  cover?: BookCoverDto | null
  genres: string[]
  tags: string[]
  links: BookLinkDto[]
}

export type AdminBookDto = BookDto & {
  ownerId: string
}

export type BookLinkDto = {
  id: string
  url: string
  label?: string | null
  sourceType: string
  isPrimary: boolean
  lastReadHere: boolean
}

export type BookCoverDto = {
  id: string
  status: string
  source?: string | null
  imageUrl?: string | null
  originalImageUrl?: string | null
  mimeType?: string | null
  sizeBytes?: number | null
  width?: number | null
  height?: number | null
  failureReason?: string | null
  lastAttemptAt?: string | null
}

export type BookTitleInput = {
  title: string
  language?: string | null
  source?: string | null
}

export type BookLinkInput = {
  url: string
  label?: string | null
  sourceType: string
  isPrimary: boolean
  lastReadHere: boolean
}

export type BookMutationRequest = {
  primaryTitle: string
  contentTypeId: string
  statusId: string
  authorId?: string | null
  authorName?: string | null
  alternativeTitles: BookTitleInput[]
  genreIds: string[]
  tags: string[]
  totalChapters?: number | null
  currentChapterNumber?: number | null
  currentChapterLabel?: string | null
  rating?: number | null
  priority?: number | null
  description?: string | null
  comment?: string | null
  notes?: string | null
  rawImportedLine?: string | null
  links: BookLinkInput[]
}

export type UpdateProgressRequest = {
  currentChapterNumber?: number | null
  currentChapterLabel?: string | null
  comment?: string | null
}

export type DictionaryDto = {
  id: string
  name: string
  description?: string | null
}

export type DictionaryMutationRequest = {
  name: string
  description?: string | null
}

export type AuthorDto = {
  id: string
  primaryName: string
  otherNames: string[]
}

export type TagDto = {
  id: string
  name: string
  description?: string | null
  color?: string | null
}
