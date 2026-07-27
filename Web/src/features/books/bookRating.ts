export function isLowRating(rating?: number | null) {
  return typeof rating === 'number' && rating <= 5
}
