import { z } from 'zod'
import type { BookDto, BookMutationRequest } from '@/api/types'

const optionalNumberString = z
  .string()
  .refine((value) => !value.trim() || !Number.isNaN(Number(value)), 'Value must be a number.')
  .refine((value) => !value.trim() || Number(value) >= 0, 'Value cannot be negative.')

const optionalPositiveNumberString = optionalNumberString.refine(
  (value) => !value.trim() || Number(value) > 0,
  'Total chapters must be greater than 0.',
)

const optionalIntegerString = optionalNumberString.refine(
  (value) => !value.trim() || Number.isInteger(Number(value)),
  'Value must be an integer.',
)

const requiredCurrentChapterString = z
  .string()
  .refine((value) => value.trim().length > 0, 'Current chapter is required.')
  .refine(
    (value) => !value.trim() || /^\d+$/.test(value.trim()),
    'Current chapter must be a non-negative integer.',
  )

export const bookFormSchema = z
  .object({
    primaryTitle: z.string().refine((value) => value.trim().length > 0, 'Title is required.').refine((value) => value.trim().length <= 500, 'Title must be 500 characters or fewer.'),
    authorId: z.string().optional(),
    authorName: z.string().max(300).optional(),
    contentTypeId: z.string().min(1, 'Type is required.'),
    statusId: z.string().min(1, 'Status is required.'),
    genreIds: z.array(z.string()),
    alternativeTitlesText: z.string(),
    tagsText: z.string(),
    totalChapters: optionalPositiveNumberString,
    currentChapterNumber: requiredCurrentChapterString,
    currentChapterLabel: z.string().max(100).optional(),
    rating: optionalIntegerString.refine((value) => !value.trim() || (Number(value) >= 1 && Number(value) <= 10), 'Rating must be between 1 and 10.'),
    priority: optionalIntegerString.refine((value) => !value.trim() || (Number(value) >= 1 && Number(value) <= 5), 'Priority must be between 1 and 5.'),
    description: z.string().max(4000).optional(),
    notes: z.string().max(4000).optional(),
    linksText: z.string(),
  })
  .refine(
    (value) =>
      !value.currentChapterNumber.trim() ||
      !value.totalChapters.trim() ||
      Number(value.currentChapterNumber) <= Number(value.totalChapters),
    {
      message: 'Current chapter cannot be greater than total chapters.',
      path: ['currentChapterNumber'],
    },
  )

export type BookFormValues = z.infer<typeof bookFormSchema>

export function toBookMutationRequest(values: BookFormValues): BookMutationRequest {
  return {
    primaryTitle: values.primaryTitle.trim(),
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
    currentChapterNumber: Number(values.currentChapterNumber.trim()),
    currentChapterLabel: values.currentChapterLabel?.trim() || null,
    rating: toOptionalNumber(values.rating),
    priority: toOptionalNumber(values.priority),
    description: values.description?.trim() || null,
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
    totalChapters: book?.totalChapters != null && book.totalChapters > 0 ? book.totalChapters.toString() : '',
    currentChapterNumber: book?.currentChapterNumber?.toString() ?? '',
    currentChapterLabel: book?.currentChapterLabel ?? '',
    rating: book?.rating?.toString() ?? '',
    priority: book?.priority?.toString() ?? '',
    description: book?.description ?? '',
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
