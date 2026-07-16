import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Save } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { api } from '@/api/client'
import type { BookDto } from '@/api/types'
import { HttpError } from '@/api/http'
import {
  buttonClass,
  inputClass,
  secondaryButtonClass,
} from '@/components/app/FormField'
import { DialogPanel, useBodyScrollLock } from '@/components/app/DesignSystem'

const chapterNumberPattern = /^\d+(?:\.\d+)?$/
const chapterLabelMaxLength = 100
const commentMaxLength = 1000

export function ProgressDialog({ book }: { book: BookDto }) {
  const [open, setOpen] = useState(false)
  const [currentChapterNumber, setCurrentChapterNumber] = useState(
    book.currentChapterNumber?.toString() ?? '',
  )
  const [currentChapterLabel, setCurrentChapterLabel] = useState(
    book.currentChapterLabel ?? '',
  )
  const [comment, setComment] = useState('')
  useBodyScrollLock(open)
  const queryClient = useQueryClient()
  const totalChapters = typeof book.totalChapters === 'number' ? Number(book.totalChapters) : null
  const chapterNumberText = currentChapterNumber.trim()
  const chapterNumberInvalid = chapterNumberText.length > 0 && !chapterNumberPattern.test(chapterNumberText)
  const numericCurrent = chapterNumberText && !chapterNumberInvalid ? Number(chapterNumberText) : null
  const exceedsTotal = numericCurrent != null && totalChapters != null && numericCurrent > totalChapters
  const chapterNumberError = chapterNumberInvalid
    ? 'Chapter number must be a non-negative number without exponent notation.'
    : exceedsTotal
      ? 'Current chapter cannot be greater than total chapters.'
      : null
  const mutation = useMutation({
    mutationFn: () =>
      api.updateProgress(book.id, {
        currentChapterNumber: numericCurrent,
        currentChapterLabel: currentChapterLabel || null,
        comment: comment || null,
      }),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['book', book.id] }),
        queryClient.invalidateQueries({ queryKey: ['books'] }),
      ])
      toast.success('Progress saved.')
      setOpen(false)
    },
    onError: (error) => {
      toast.error(error instanceof HttpError ? error.apiError.detail : 'Failed to save progress.')
    },
  })

  if (!open) {
    return (
      <button className={secondaryButtonClass} type="button" onClick={() => setOpen(true)}>
        <Save className="h-4 w-4" />
        Progress
      </button>
    )
  }

  return (
    <div aria-modal="true" className="fixed inset-0 z-50 grid place-items-center bg-slate-950/70 p-4" role="dialog" onClick={() => setOpen(false)}>
      <DialogPanel className="max-w-md p-5" onClick={(event) => event.stopPropagation()}>
        <div className="ui-eyebrow">Reading record</div>
        <h2 className="text-lg font-semibold text-slate-950">Update progress</h2>
        <div className="mt-4 grid gap-3">
          {totalChapters != null ? <p className="text-sm text-slate-500">Total chapters: {totalChapters}</p> : null}
          <div>
            <input
              aria-invalid={chapterNumberError ? 'true' : undefined}
              className={inputClass}
              inputMode="decimal"
              placeholder="Chapter number"
              value={currentChapterNumber}
              onChange={(event) => setCurrentChapterNumber(event.target.value)}
            />
            {chapterNumberError ? <p className="mt-1 text-sm text-red-600">{chapterNumberError}</p> : null}
          </div>
          <input className={inputClass} maxLength={chapterLabelMaxLength} placeholder="Chapter label" value={currentChapterLabel} onChange={(event) => setCurrentChapterLabel(event.target.value)} />
          <textarea className={`${inputClass} min-h-24`} maxLength={commentMaxLength} placeholder="Comment" value={comment} onChange={(event) => setComment(event.target.value)} />
        </div>
        <div className="mt-5 flex justify-end gap-2">
          <button className={secondaryButtonClass} type="button" onClick={() => setOpen(false)}>Cancel</button>
          <button className={buttonClass} disabled={mutation.isPending || Boolean(chapterNumberError)} type="button" onClick={() => mutation.mutate()}>
            Save
          </button>
        </div>
      </DialogPanel>
    </div>
  )
}
