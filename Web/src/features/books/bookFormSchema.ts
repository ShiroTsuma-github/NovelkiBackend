import { z } from 'zod'
import type { BookDto, BookMutationRequest } from '@/api/types'

const optionalNumberString = z
  .string()
  .refine((value) => !value.trim() || !Number.isNaN(Number(value)), 'Wartość musi być liczbą.')
  .refine((value) => !value.trim() || Number(value) >= 0, 'Wartość nie może być ujemna.')

const optionalIntegerString = optionalNumberString.refine(
  (value) => !value.trim() || Number.isInteger(Number(value)),
  'Wartość musi być liczbą całkowitą.',
)

export const bookFormSchema = z
  .object({
    primaryTitle: z.string().min(1, 'Tytuł jest wymagany.').max(500),
    authorId: z.string().optional(),
    authorName: z.string().max(300).optional(),
    contentTypeId: z.string().min(1, 'Typ jest wymagany.'),
    statusId: z.string().min(1, 'Status jest wymagany.'),
    genreIds: z.array(z.string()),
    alternativeTitlesText: z.string(),
    tagsText: z.string(),
    totalChapters: optionalNumberString,
    currentChapterNumber: optionalNumberString,
    currentChapterLabel: z.string().max(100).optional(),
    rating: optionalIntegerString.refine((value) => !value.trim() || (Number(value) >= 1 && Number(value) <= 10), 'Ocena musi być od 1 do 10.'),
    priority: optionalIntegerString.refine((value) => !value.trim() || (Number(value) >= 1 && Number(value) <= 5), 'Priorytet musi być od 1 do 5.'),
    description: z.string().max(4000).optional(),
    comment: z.string().max(1000).optional(),
    notes: z.string().max(4000).optional(),
    linksText: z.string(),
  })
  .refine(
    (value) =>
      !value.currentChapterNumber.trim() ||
      !value.totalChapters.trim() ||
      Number(value.currentChapterNumber) <= Number(value.totalChapters),
    {
      message: 'Aktualny rozdział nie może być większy niż liczba rozdziałów.',
      path: ['currentChapterNumber'],
    },
  )

export type BookFormValues = z.infer<typeof bookFormSchema>

export function toBookMutationRequest(values: BookFormValues): BookMutationRequest {
  return {
    primaryTitle: values.primaryTitle,
    contentTypeId: values.contentTypeId,
    statusId: values.statusId,
    authorName: values.authorName?.trim() || null,
    authorId: values.authorId || null,
    alternativeTitles: splitLines(values.alternativeTitlesText).map((title) => ({
      title,
      language: null,
      source: 'manual',
    })),
    genreIds: values.genreIds,
    tags: splitComma(values.tagsText),
    totalChapters: toOptionalNumber(values.totalChapters),
    currentChapterNumber: toOptionalNumber(values.currentChapterNumber),
    currentChapterLabel: values.currentChapterLabel?.trim() || null,
    rating: toOptionalNumber(values.rating),
    priority: toOptionalNumber(values.priority),
    description: values.description?.trim() || null,
    comment: values.comment?.trim() || null,
    notes: values.notes?.trim() || null,
    rawImportedLine: null,
    links: splitLines(values.linksText).map((url) => ({
      url,
      label: null,
      sourceType: 'Other',
      isPrimary: false,
      lastReadHere: false,
    })),
  }
}

export function defaultBookFormValues(book?: BookDto): BookFormValues {
  return {
    primaryTitle: book?.primaryTitle ?? '',
    authorId: book?.authorId ?? '',
    authorName: book?.author ?? '',
    contentTypeId: '',
    statusId: '',
    genreIds: [],
    alternativeTitlesText: book?.alternativeTitles.join('\n') ?? '',
    tagsText: book?.tags.join(', ') ?? '',
    totalChapters: book?.totalChapters?.toString() ?? '',
    currentChapterNumber: book?.currentChapterNumber?.toString() ?? '',
    currentChapterLabel: book?.currentChapterLabel ?? '',
    rating: book?.rating?.toString() ?? '',
    priority: book?.priority?.toString() ?? '',
    description: '',
    comment: book?.comment ?? '',
    notes: book?.notes ?? '',
    linksText: book?.links.map((link) => link.url).join('\n') ?? '',
  }
}

function splitLines(value: string) {
  return value
    .split('\n')
    .map((item) => item.trim())
    .filter(Boolean)
}

function splitComma(value: string) {
  return value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)
}

function toOptionalNumber(value: string) {
  return value.trim() ? Number(value) : null
}
