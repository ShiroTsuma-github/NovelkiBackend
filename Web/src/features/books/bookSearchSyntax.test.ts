import { describe, expect, it } from 'vitest'
import {
  analyzeBookSearch,
  applyBookSearchSuggestion,
  getBookSearchScopeQuery,
  getLocalBookSearchSuggestions,
} from './bookSearchSyntax'

describe('book search syntax', () => {
  it('recognizes a token when the caret is at its first character', () => {
    const context = analyzeBookSearch('tag:favorite rating:8', 0)

    expect(context.text).toBe('tag:favorite')
    expect(context.definition?.canonical).toBe('tag')
    expect(context.complete).toBe(true)
  })

  it('targets only the active comma-separated value', () => {
    const query = 'tag:"favorite", slow,blocked status:Reading'
    const context = analyzeBookSearch(query, query.indexOf('slow') + 2)
    const result = applyBookSearchSuggestion(query, context, {
      id: 'value:slow burn',
      kind: 'value',
      label: 'slow burn',
      description: '4 books',
      value: 'slow burn',
    })

    expect(result.value).toBe('tag:"favorite", "slow burn",blocked status:Reading')
    expect(result.caret).toBe(result.value.indexOf('"slow burn"') + '"slow burn"'.length)
  })

  it('offers include and remove actions for an excluded complete filter', () => {
    const query = 'title:Lord -tag:dropped rating:>=8'
    const context = analyzeBookSearch(query, query.indexOf('dropped'))
    const actions = getLocalBookSearchSuggestions(context)

    expect(actions.map((item) => item.kind).slice(0, 2)).toEqual(['include', 'remove'])
    expect(applyBookSearchSuggestion(query, context, actions[0]).value)
      .toBe('title:Lord tag:dropped rating:>=8')
    expect(applyBookSearchSuggestion(query, context, actions[1]).value)
      .toBe('title:Lord rating:>=8')
  })

  it('preserves an exclusion prefix while completing a filter name', () => {
    const query = '-rat'
    const context = analyzeBookSearch(query, query.length)
    const rating = getLocalBookSearchSuggestions(context)
      .find((suggestion) => suggestion.definition?.canonical === 'rating')!

    expect(applyBookSearchSuggestion(query, context, rating)).toEqual({
      value: '-rating:',
      caret: '-rating:'.length,
    })
  })

  it('opens quotes and places the caret inside for text filters', () => {
    const context = analyzeBookSearch('tit', 'tit'.length)
    const title = getLocalBookSearchSuggestions(context)
      .find((suggestion) => suggestion.definition?.canonical === 'title')!

    expect(applyBookSearchSuggestion('tit', context, title)).toEqual({
      value: 'title:""',
      caret: 'title:"'.length,
    })
  })

  it.each(['title', 'description'])(
    'offers an actionable wildcard hint for the empty %s filter',
    (field) => {
      const query = `${field}:""`
      const context = analyzeBookSearch(query, query.length - 1)
      const wildcard = getLocalBookSearchSuggestions(context)
        .find((suggestion) => suggestion.kind === 'wildcard')!

      expect(wildcard.label).toBe('* wildcard')
      expect(wildcard.description).toContain(`${field}:"Lord *"`)
      expect(applyBookSearchSuggestion(query, context, wildcard)).toEqual({
        value: `${field}:"*"`,
        caret: `${field}:"*`.length,
      })
    },
  )

  it('does not expose token actions for an incomplete numeric operator', () => {
    const context = analyzeBookSearch('rating:>=', 'rating:>='.length)
    const suggestions = getLocalBookSearchSuggestions(context)

    expect(context.complete).toBe(false)
    expect(suggestions.some((suggestion) => suggestion.kind === 'exclude')).toBe(false)
    expect(suggestions.some((suggestion) => suggestion.kind === 'remove')).toBe(false)
  })

  it('shows manual text filters without suggesting missing-value filters', () => {
    const suggestions = getLocalBookSearchSuggestions(analyzeBookSearch('', 0))
    const labels = suggestions.map((suggestion) => suggestion.label)

    expect(labels).toContain('title:#')
    expect(labels).toContain('description:#')
    expect(labels).not.toContain('alternativeTitle:none')
    expect(labels).not.toContain('cover:none')
    expect(labels).not.toContain('links:none')
    expect(labels.some((label) => label.endsWith(':none'))).toBe(false)
  })

  it('never proposes none as a filter value', () => {
    const suggestions = getLocalBookSearchSuggestions(
      analyzeBookSearch('rating:', 'rating:'.length),
    )

    expect(suggestions.some((suggestion) => suggestion.kind === 'none')).toBe(false)
    expect(suggestions.some((suggestion) => suggestion.value === 'none')).toBe(false)
  })

  it('adds a trailing space after accepting the last suggested value', () => {
    const query = 'tag:fant'
    const context = analyzeBookSearch(query, query.length)
    const result = applyBookSearchSuggestion(query, context, {
      id: 'value:fantasy',
      kind: 'value',
      label: 'fantasy',
      description: '12 books',
      value: 'fantasy',
    })

    expect(result).toEqual({
      value: 'tag:"fantasy" ',
      caret: 'tag:"fantasy" '.length,
    })
    expect(getLocalBookSearchSuggestions(analyzeBookSearch(result.value, result.caret)))
      .toEqual(expect.arrayContaining([
        expect.objectContaining({ label: 'title:#' }),
        expect.objectContaining({ label: 'author:' }),
      ]))
  })

  it('builds a suggestion scope query without the active token', () => {
    const query = 'type:"Novel" status:"rea" rating:>=7'
    const context = analyzeBookSearch(query, query.indexOf('rea') + 2)

    expect(getBookSearchScopeQuery(query, context)).toBe('type:"Novel" rating:>=7')
  })
})
