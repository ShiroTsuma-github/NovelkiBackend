export type BookProgressLike = {
  status: string
  currentChapterNumber?: number | null
  currentChapterLabel?: string | null
  totalChapters?: number | null
}

export function formatProgress(book: BookProgressLike) {
  const isCompleted = book.status.trim().toLowerCase() === 'completed'
  const current = isCompleted && book.currentChapterNumber != null
    ? book.currentChapterNumber
    : book.currentChapterLabel || book.currentChapterNumber
  if (!current && !book.totalChapters) {
    return '-'
  }
  return `${current ?? '?'}${book.totalChapters ? ` / ${book.totalChapters}` : ''}`
}
