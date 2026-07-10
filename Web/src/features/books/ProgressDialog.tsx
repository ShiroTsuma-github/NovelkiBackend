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

export function ProgressDialog({ book }: { book: BookDto }) {
  const [open, setOpen] = useState(false)
  const [currentChapterNumber, setCurrentChapterNumber] = useState(
    book.currentChapterNumber?.toString() ?? '',
  )
  const [currentChapterLabel, setCurrentChapterLabel] = useState(
    book.currentChapterLabel ?? '',
  )
  const [comment, setComment] = useState('')
  const queryClient = useQueryClient()
  const totalChapters = typeof book.totalChapters === 'number' ? Number(book.totalChapters) : null
  const numericCurrent = currentChapterNumber.trim() ? Number(currentChapterNumber) : null
  const exceedsTotal = numericCurrent != null && totalChapters != null && numericCurrent > totalChapters
  const mutation = useMutation({
    mutationFn: () =>
      api.updateProgress(book.id, {
        currentChapterNumber: currentChapterNumber ? Number(currentChapterNumber) : null,
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
    <div className="fixed inset-0 z-50 grid place-items-center bg-slate-950/40 p-4">
      <section className="w-full max-w-md rounded-lg bg-white p-5 shadow-xl">
        <h2 className="text-lg font-semibold text-slate-950">Update progress</h2>
        <div className="mt-4 grid gap-3">
          {totalChapters != null ? <p className="text-sm text-slate-500">Total chapters: {totalChapters}</p> : null}
          <input className={inputClass} max={totalChapters ?? undefined} placeholder="Chapter number" type="number" value={currentChapterNumber} onChange={(event) => setCurrentChapterNumber(event.target.value)} />
          <input className={inputClass} placeholder="Chapter label" value={currentChapterLabel} onChange={(event) => setCurrentChapterLabel(event.target.value)} />
          <textarea className={`${inputClass} min-h-24`} placeholder="Comment" value={comment} onChange={(event) => setComment(event.target.value)} />
          {exceedsTotal ? <p className="text-sm text-red-600">Current chapter cannot be greater than total chapters.</p> : null}
        </div>
        <div className="mt-5 flex justify-end gap-2">
          <button className={secondaryButtonClass} type="button" onClick={() => setOpen(false)}>Cancel</button>
          <button className={buttonClass} disabled={mutation.isPending || exceedsTotal} type="button" onClick={() => mutation.mutate()}>
            Save
          </button>
        </div>
      </section>
    </div>
  )
}
