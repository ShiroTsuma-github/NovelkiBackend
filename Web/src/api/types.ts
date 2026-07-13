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
  notes?: string | null
  progressHistory: BookProgressHistoryDto[]
  cover?: BookCoverDto | null
  genres: string[]
  tags: string[]
  links: BookLinkDto[]
}

export type BookListItemDto = {
  id: string
  created: string
  lastModified: string
  primaryTitle: string
  description?: string | null
  alternativeTitles: string[]
  alternativeTitlesCount: number
  author?: string | null
  contentType: string
  status: string
  currentChapterNumber?: number | null
  currentChapterLabel?: string | null
  totalChapters?: number | null
  rating?: number | null
  priority?: number | null
  notes?: string | null
  cover?: BookCoverDto | null
  genres: string[]
  genresCount: number
  tags: string[]
  tagsCount: number
  linksCount: number
}

export type BookSummaryDto = {
  totalBooks: number
  ratedBooks: number
  unratedBooks: number
  averageRating?: number | null
  currentChapters: number
  booksWithKnownCurrentChapter: number
  booksWithoutKnownCurrentChapter: number
  statusCounts: BookSummaryStatusCountDto[]
  typeCounts: BookSummaryTypeCountDto[]
  genreCounts: BookSummaryGenreCountDto[]
  ratingCounts: BookSummaryRatingCountDto[]
}

export type BookSummaryStatusCountDto = {
  status: string
  count: number
}

export type BookSummaryTypeCountDto = {
  type: string
  bookCount: number
  currentChapters: number
}

export type BookSummaryGenreCountDto = {
  genre: string
  bookCount: number
}

export type BookSummaryRatingCountDto = {
  rating: number
  bookCount: number
}

export type AdminBookDto = BookDto & {
  ownerId: string
}

export type AdminBookListItemDto = BookListItemDto & {
  ownerId: string
  ownerUsername?: string | null
  ownerEmail?: string | null
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
  thumbnailImageUrl?: string | null
  originalImageUrl?: string | null
  mimeType?: string | null
  sizeBytes?: number | null
  width?: number | null
  height?: number | null
  thumbnailMimeType?: string | null
  thumbnailSizeBytes?: number | null
  thumbnailWidth?: number | null
  thumbnailHeight?: number | null
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
  notes?: string | null
  rawImportedLine?: string | null
  links: BookLinkInput[]
}

export type BookProgressHistoryDto = {
  id: string
  changedAt: string
  chapterNumber?: number | null
  chapterLabel?: string | null
  comment?: string | null
}

export type BookImportRowDto = {
  rowId: string
  lineNumber: number
  isValid: boolean
  primaryTitle?: string | null
  authorName?: string | null
  contentType?: string | null
  status?: string | null
  tags?: string | null
  totalChapters?: string | null
  currentChapterNumber?: string | null
  currentChapterLabel?: string | null
  rating?: string | null
  priority?: string | null
  description?: string | null
  notes?: string | null
  rawImportedLine?: string | null
  errors: string[]
  fieldErrors: Record<string, string[]>
}

export type BookImportSessionDto = {
  sessionId: string
  fileName: string
  totalRows: number
  validRows: number
  invalidRows: number
  canFinalize: boolean
  availableContentTypes: string[]
  availableStatuses: string[]
  rows: BookImportRowDto[]
}

export type BookImportRowUpdateRequest = {
  primaryTitle?: string | null
  authorName?: string | null
  contentType?: string | null
  status?: string | null
  tags?: string | null
  totalChapters?: string | null
  currentChapterNumber?: string | null
  currentChapterLabel?: string | null
  rating?: string | null
  priority?: string | null
  description?: string | null
  notes?: string | null
  rawImportedLine?: string | null
}

export type BookImportFinalizeResult = {
  importedCount: number
  skippedCount: number
  errors: string[]
}

export type AdminLibraryPurgeResult = {
  deletedBooks: number
  deletedAuthors: number
  deletedTags: number
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
