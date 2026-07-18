const coverDownloadFailureMessage =
  'A cover was found, but the image could not be downloaded. Try searching again or upload a cover manually.'

export function getDisplayCoverStatus(status?: string | null, failureReason?: string | null) {
  if (status === 'Failed' && isProviderResponseFailure(failureReason)) {
    return 'Not found'
  }

  return status ?? 'Missing'
}

export function getDisplayCoverFailure(failureReason?: string | null) {
  if (!failureReason) {
    return null
  }

  if (isProviderResponseFailure(failureReason)) {
    return 'No valid cover response was found from the configured providers.'
  }

  if (isCoverDownloadFailure(failureReason)) {
    return coverDownloadFailureMessage
  }

  return failureReason
}

function isCoverDownloadFailure(failureReason: string) {
  return failureReason.includes('Response status code does not indicate success')
    || /HTTP\s+3\d\d/i.test(failureReason)
}

function isProviderResponseFailure(failureReason?: string | null) {
  if (!failureReason) {
    return false
  }

  return failureReason.includes('invalid start of a value') || failureReason.includes("'<'")
}
