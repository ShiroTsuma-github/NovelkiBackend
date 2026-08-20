import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'
import { toast } from 'sonner'
import { getStoredSessionUserId } from '@/api/http'
import { useAuth } from '@/features/auth/AuthProvider'
import {
  getPendingCoverUploads,
  isPendingCoverUploadNotCreated,
  processPendingCoverUpload,
  subscribeToCoverUploadOutbox,
} from './coverUploadOutbox'

const retryIntervalMs = 10_000

export function CoverUploadOutbox() {
  const { isAuthenticated } = useAuth()
  const queryClient = useQueryClient()
  const drainingRef = useRef(false)
  const rerunRequestedRef = useRef(false)

  useEffect(() => {
    if (!isAuthenticated) {
      return
    }

    let active = true
    async function drain() {
      if (!active || drainingRef.current) {
        rerunRequestedRef.current = true
        return
      }

      drainingRef.current = true
      try {
        const ownerId = getStoredSessionUserId()
        if (!ownerId) {
          return
        }

        for (const upload of await getPendingCoverUploads(ownerId)) {
          if (!active) {
            return
          }

          try {
            const bookId = await processPendingCoverUpload(upload)
            await Promise.all([
              queryClient.invalidateQueries({ queryKey: ['books'] }),
              queryClient.invalidateQueries({ queryKey: ['book', bookId] }),
            ])
            toast.success('Cover uploaded.')
          } catch (error) {
            if (!isPendingCoverUploadNotCreated(error)) {
              // Keep the local file for a later retry instead of discarding the user's cover.
            }
          }
        }
      } finally {
        drainingRef.current = false
        if (rerunRequestedRef.current) {
          rerunRequestedRef.current = false
          void drain()
        }
      }
    }

    void drain()
    const unsubscribe = subscribeToCoverUploadOutbox(() => void drain())
    const interval = window.setInterval(() => void drain(), retryIntervalMs)
    return () => {
      active = false
      unsubscribe()
      window.clearInterval(interval)
    }
  }, [isAuthenticated, queryClient])

  return null
}
