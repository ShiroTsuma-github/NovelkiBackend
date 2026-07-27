import { describe, expect, it } from 'vitest'
import { getProductionHttpsRedirectUrl } from './httpsRedirect'

describe('getProductionHttpsRedirectUrl', () => {
  it('moves a public production URL to standard HTTPS before the app starts', () => {
    expect(getProductionHttpsRedirectUrl({
      href: 'http://reader.example.com/library?page=2#results',
      hostname: 'reader.example.com',
      protocol: 'http:',
    }, true, 'https://reader.example.com')).toBe('https://reader.example.com/library?page=2#results')
  })

  it('does not redirect HTTPS, development, or a host outside the configured public origin', () => {
    expect(getProductionHttpsRedirectUrl({
      href: 'https://reader.example.com/',
      hostname: 'reader.example.com',
      protocol: 'https:',
    }, true, 'https://reader.example.com')).toBeNull()
    expect(getProductionHttpsRedirectUrl({
      href: 'http://reader.example.com/',
      hostname: 'reader.example.com',
      protocol: 'http:',
    }, false, 'https://reader.example.com')).toBeNull()
    expect(getProductionHttpsRedirectUrl({
      href: 'http://192.168.1.20:8082/',
      hostname: '192.168.1.20',
      protocol: 'http:',
    }, true, 'https://reader.example.com')).toBeNull()
  })
})
