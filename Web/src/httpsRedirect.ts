type BrowserLocation = Pick<Location, 'href' | 'hostname' | 'protocol'>

export function getProductionHttpsRedirectUrl(
  location: BrowserLocation,
  production: boolean,
  publicOrigin?: string,
): string | null {
  if (!production || location.protocol !== 'http:' || !publicOrigin) {
    return null
  }

  try {
    const configuredOrigin = new URL(publicOrigin)
    if (configuredOrigin.protocol !== 'https:' || configuredOrigin.hostname !== location.hostname) {
      return null
    }

    const secureUrl = new URL(location.href)
    secureUrl.protocol = configuredOrigin.protocol
    secureUrl.host = configuredOrigin.host
    return secureUrl.toString()
  } catch {
    return null
  }
}
