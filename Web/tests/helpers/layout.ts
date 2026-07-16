import { expect, type Locator, type Page } from '@playwright/test'

export async function requiredBox(locator: Locator) {
  const box = await locator.boundingBox()
  expect(box).not.toBeNull()
  return box!
}

export async function backgroundColor(locator: Locator) {
  return locator.evaluate((element) => getComputedStyle(element).backgroundColor)
}

export async function expectNoHorizontalOverflow(page: Page) {
  const hasOverflow = await page.evaluate(() => {
    const root = document.documentElement
    const body = document.body
    return root.scrollWidth > root.clientWidth + 1 || body.scrollWidth > body.clientWidth + 1
  })
  expect(hasOverflow).toBe(false)
}

export async function expectNoVerticalDocumentOverflow(page: Page) {
  const hasOverflow = await page.evaluate(() => {
    const root = document.documentElement
    const body = document.body
    return root.scrollHeight > root.clientHeight + 1 || body.scrollHeight > body.clientHeight + 1
  })
  expect(hasOverflow).toBe(false)
}

export async function expectInViewport(locator: Locator, tolerancePx = 8) {
  const box = await requiredBox(locator)
  const viewport = locator.page().viewportSize()
  expect(viewport).not.toBeNull()

  expect(box.x).toBeGreaterThanOrEqual(0)
  expect(box.y).toBeGreaterThanOrEqual(0)
  expect(box.x + box.width).toBeLessThanOrEqual(viewport!.width + tolerancePx)
  expect(box.y).toBeLessThanOrEqual(viewport!.height + tolerancePx)
}

export async function expectFitsViewportWidth(locator: Locator, tolerancePx = 8) {
  const box = await requiredBox(locator)
  const viewport = locator.page().viewportSize()
  expect(viewport).not.toBeNull()

  expect(box.x).toBeGreaterThanOrEqual(0)
  expect(box.x + box.width).toBeLessThanOrEqual(viewport!.width + tolerancePx)
}

export async function expectCenteredInViewport(locator: Locator) {
  const box = await requiredBox(locator)
  const viewport = locator.page().viewportSize()
  expect(viewport).not.toBeNull()

  const viewportCenterX = viewport!.width / 2
  const viewportCenterY = viewport!.height / 2
  const boxCenterX = box.x + box.width / 2
  const boxCenterY = box.y + box.height / 2

  expect(Math.abs(boxCenterX - viewportCenterX)).toBeLessThan(32)
  expect(Math.abs(boxCenterY - viewportCenterY)).toBeLessThan(32)
}

export async function expectMinHeight(locator: Locator, minHeightPx: number) {
  const box = await requiredBox(locator)
  expect(box.height).toBeGreaterThanOrEqual(minHeightPx)
}

export async function expectElementBottomVisible(container: Locator, element: Locator) {
  const containerBox = await requiredBox(container)
  const elementBox = await requiredBox(element)

  expect(elementBox.y).toBeGreaterThanOrEqual(containerBox.y)
  expect(elementBox.y + elementBox.height).toBeLessThanOrEqual(containerBox.y + containerBox.height)
}
