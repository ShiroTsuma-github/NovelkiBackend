import { describe, expect, it } from 'vitest'
import { buildBookQuery, emptyFilters } from './queryBuilder'

describe('buildBookQuery', () => {
  it('builds numeric filters with colon operator syntax', () => {
    const query = buildBookQuery({
      ...emptyFilters,
      ratingMin: '8',
      progressMin: '50',
      chaptersMax: '200',
      priority: '2',
    })

    expect(query).toBe('rating:>=8 progress:>=50 chapters:<=200 priority:2')
  })

  it('combines text and field filters with numeric colon filters', () => {
    const query = buildBookQuery({
      ...emptyFilters,
      text: 'returnee',
      status: 'Completed',
      totalChapters: '300',
      ratingMin: '9',
    })

    expect(query).toBe('returnee status:Completed rating:>=9 totalChapters:300')
  })
})
