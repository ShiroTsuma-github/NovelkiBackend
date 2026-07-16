import { expect, test } from '@playwright/test'
import { installLayoutApiMocks, layoutBooks, seedAuthenticatedSession } from './fixtures/api.js'
import {
  backgroundColor,
  expectCenteredInViewport,
  expectElementBottomVisible,
  expectFitsViewportWidth,
  expectInViewport,
  expectMinHeight,
  expectNoHorizontalOverflow,
  expectNoVerticalDocumentOverflow,
  requiredBox,
} from './helpers/layout.js'

test.beforeEach(async ({ page }) => {
  await seedAuthenticatedSession(page)
  await installLayoutApiMocks(page)
})

test('app shell keeps stable page landmarks and primary structure', async ({ page }) => {
  await page.goto('/books')

  await expect(page.locator('header')).toBeVisible()
  await expect(page.locator('main')).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Books' })).toBeVisible()
  await expect(page.getByRole('button', { name: /log out/i })).toBeVisible()
  await expect(page.locator('main')).toHaveCount(1)

  await expect(page.getByRole('navigation', { name: /primary/i })).toBeVisible()
  await expect(page.getByRole('link', { name: /books/i })).toHaveAttribute('aria-current', 'page')
  await expect(page.getByRole('link', { name: /skip to content/i })).toHaveAttribute('href', '#main-content')

  await expectNoHorizontalOverflow(page)
})

test('quiet structure POC keeps its defining visual rules', async ({ page }) => {
  await page.goto('/books')

  const title = page.getByRole('heading', { name: 'Books' })
  const addButton = page.getByRole('link', { name: /add book/i })
  const mainSurface = page.locator('.ui-surface').filter({ has: page.getByRole('table') }).first()

  await expect(title).toHaveCSS('font-family', /IBM Plex Sans/)
  await expect(page.locator('body')).toHaveCSS('background-color', 'rgb(5, 7, 11)')
  await expect(addButton).toHaveCSS('border-radius', '3px')
  await expect(addButton).toHaveCSS('background-image', 'none')
  await expect(addButton).toHaveCSS('box-shadow', 'none')
  await expect(mainSurface).toHaveCSS('border-radius', '0px')

  await page.goto('/books/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa')
  await page.getByRole('button', { name: /progress/i }).click()
  await expect(page.getByRole('dialog').locator('.ui-dialog-panel')).toHaveCSS('border-radius', '4px')
})

test('mobile navigation and primary actions keep touch targets', async ({ page }) => {
  test.skip(page.viewportSize()!.width >= 768, 'Mobile-only touch target coverage')
  await page.goto('/books')

  await expectMinHeight(page.getByRole('navigation', { name: /primary/i }).getByRole('link', { name: /books/i }), 44)
  await expectMinHeight(page.getByRole('button', { name: /log out/i }), 44)
  await expectMinHeight(page.getByRole('button', { name: /summary/i }), 44)
  await expectMinHeight(page.getByRole('button', { name: /import csv/i }), 44)
  await expectMinHeight(page.getByRole('link', { name: /add book/i }), 44)
})

test('books table layout has no horizontal overflow and keeps controls in viewport', async ({ page }) => {
  await page.goto('/books')
  await expect(page.getByRole('table')).toBeVisible()

  await expectNoHorizontalOverflow(page)
  await expectInViewport(page.getByRole('button', { name: /columns/i }))
  if (page.viewportSize()!.width >= 768) {
    await expectInViewport(page.getByRole('table'))
  }

  await page.getByRole('button', { name: /columns/i }).click()
  const popup = page.getByText('Visible columns').locator('..').locator('..')
  await expectInViewport(popup)
  await expectNoHorizontalOverflow(page)
})

test('books table hover highlight reaches the sticky actions cell', async ({ page }) => {
  await page.goto('/books')
  await expect(page.getByRole('table')).toBeVisible()

  await page.getByTestId('book-table-row-aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa').hover()

  const titleBackground = await backgroundColor(page.getByTestId('book-table-cell-aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa-title'))
  const actionsBackground = await backgroundColor(page.getByTestId('book-table-actions-cell-aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'))
  const alternativeBadgeBackground = await backgroundColor(page.getByLabel('1 alternative titles'))

  expect(actionsBackground).toBe(titleBackground)
  expect(actionsBackground).toBe('rgb(23, 31, 45)')
  expect(alternativeBadgeBackground).toBe('rgb(36, 42, 66)')
  expect(alternativeBadgeBackground).not.toBe(titleBackground)
})

test('quiet structure reference screens stay visually stable', async ({ page }, testInfo) => {
  await page.emulateMedia({ reducedMotion: 'reduce' })

  await page.goto('/books')
  await expect(page.getByRole('table')).toBeVisible()
  await waitForVisualReady(page)
  await expect(page).toHaveScreenshot(`nb-054-v3-books-table-${testInfo.project.name}.png`, {
    animations: 'disabled',
    fullPage: false,
  })

  await page.getByRole('button', { name: /cards/i }).click()
  await expect(page.locator('article').first()).toBeVisible()
  await waitForVisualReady(page)
  await expect(page).toHaveScreenshot(`nb-054-v3-books-cards-${testInfo.project.name}.png`, {
    animations: 'disabled',
    fullPage: false,
  })

  await page.goto('/books/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa')
  await expect(page.getByRole('heading', { name: layoutBooks[0].primaryTitle })).toBeVisible()
  await waitForVisualReady(page)
  await expect(page).toHaveScreenshot(`nb-054-v3-book-details-${testInfo.project.name}.png`, {
    animations: 'disabled',
    fullPage: false,
  })

  await page.getByRole('button', { name: /progress/i }).click()
  await expect(page.getByRole('heading', { name: /update progress/i })).toBeVisible()
  await waitForVisualReady(page)
  await expect(page).toHaveScreenshot(`nb-054-v3-progress-dialog-${testInfo.project.name}.png`, {
    animations: 'disabled',
    fullPage: false,
  })

  await page.goto('/books/new')
  await expect(page.getByRole('heading', { name: 'Add book' })).toBeVisible()
  await waitForVisualReady(page)
  await expect(page).toHaveScreenshot(`nb-054-v3-book-form-${testInfo.project.name}.png`, {
    animations: 'disabled',
    fullPage: false,
  })
})

async function waitForVisualReady(page: import('@playwright/test').Page) {
  await page.evaluate(async () => {
    await document.fonts.ready
    await new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())))
  })
}

test('cards layout constrains long titles and shows active toggle styles', async ({ page }) => {
  await page.goto('/books')
  await page.getByRole('button', { name: /cards/i }).click()

  const cardsButton = page.getByRole('button', { name: /cards/i })
  const tableButton = page.getByRole('button', { name: /table/i })
  await expect(cardsButton).toHaveAttribute('aria-pressed', 'true')
  await expect(cardsButton).toHaveClass(/ui-segmented-control__item--active/)
  await expect(tableButton).toHaveAttribute('aria-pressed', 'false')

  const longTitle = page.getByRole('heading', {
    name: 'A Very Long Book Title That Should Stay Inside Its Card Container Without Pushing Actions Away',
  })
  const titleBox = await requiredBox(longTitle)
  const cardBox = await requiredBox(longTitle.locator('xpath=ancestor::article[1]'))

  expect(titleBox.x).toBeGreaterThanOrEqual(cardBox.x)
  expect(titleBox.x + titleBox.width).toBeLessThanOrEqual(cardBox.x + cardBox.width + 1)
  expect(titleBox.height).toBeLessThanOrEqual(56)
  await expectNoHorizontalOverflow(page)
})

test('analytics layout exposes chart structure without page overflow', async ({ page }) => {
  await page.goto('/analytics')
  await expect(page.getByRole('heading', { name: 'Analytics' })).toBeVisible()
  await expect(page.getByText('Status by type')).toBeVisible()

  await expectNoHorizontalOverflow(page)
  await expectFitsViewportWidth(page.locator('main'))
  await expectFitsViewportWidth(page.getByTestId('analytics-left-column'))
  await expectFitsViewportWidth(page.getByTestId('analytics-right-column'))

  const statusByTypeCard = page.getByText('Status by type').locator('xpath=ancestor::section[1]')
  await expectMinHeight(statusByTypeCard, 220)
  await expectInViewport(page.getByRole('button', { name: /date range/i }))
})

test('progress dialog exposes error state through DOM and stays centered', async ({ page }) => {
  await page.goto('/books/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa')
  await page.getByRole('button', { name: /progress/i }).click()

  const dialog = page.getByText('Update progress').locator('xpath=ancestor::section[1]')
  await expectInViewport(dialog)
  await expectCenteredInViewport(dialog)

  const chapterInput = page.getByPlaceholder('Chapter number')
  await chapterInput.fill('1e9')

  await expect(chapterInput).toHaveAttribute('aria-invalid', 'true')
  const errorMessage = page.getByText('Chapter number must be a non-negative number without exponent notation.')
  await expect(errorMessage).toHaveClass(/text-red-600/)
  await expect(errorMessage).not.toHaveCSS('color', 'rgb(203, 213, 225)')
  await expect(page.getByRole('button', { name: 'Save' })).toBeDisabled()
})

test('book form create layout fits desktop and mobile viewports', async ({ page }) => {
  await page.goto('/books/new')
  await expect(page.getByRole('heading', { name: 'Add book' })).toBeVisible()

  await expectNoHorizontalOverflow(page)
  await expectFitsViewportWidth(page.getByText('Basics').locator('xpath=ancestor::section[1]'))
  await expectFitsViewportWidth(page.getByText('Additional').locator('xpath=ancestor::section[1]'))
})

test('csv import invalid rows panel keeps usable height in the browser layout', async ({ page }) => {
  await page.goto('/books')
  await page.getByRole('button', { name: /import csv/i }).click()
  await page.locator('input[type="file"]').setInputFiles({
    name: 'invalid-books.csv',
    mimeType: 'text/csv',
    buffer: Buffer.from('primaryTitle,contentType,status\nBroken,Wrong,Reading\n'),
  })

  const invalidRowsPanel = page.getByTestId('import-invalid-rows-panel')
  await expect(invalidRowsPanel).toBeVisible()

  const viewport = page.viewportSize()
  expect(viewport).not.toBeNull()

  const panelBox = await requiredBox(invalidRowsPanel)
  expect(panelBox.height).toBeGreaterThan(viewport!.height * 0.3)
  expect(panelBox.height).toBeGreaterThan(180)

  const dialogBox = await requiredBox(page.getByTestId('import-dialog-panel'))
  expect(dialogBox.height).toBeLessThanOrEqual(viewport!.height - 8)
  await expectNoVerticalDocumentOverflow(page)

  if (viewport!.width >= 1024) {
    await page.getByRole('button', { name: /edit row/i }).first().click()
    await expectElementBottomVisible(invalidRowsPanel, page.getByLabel('Notes'))

    await page.getByRole('button', { name: /collapse/i }).click()
    await page.locator('.import-rows-scroll').evaluate((element) => {
      element.scrollTop = element.scrollHeight
    })
    await expect(page.getByTestId('import-row-layout-row-80')).toBeVisible()
    await page.getByTestId('import-row-layout-row-80').getByRole('button', { name: /edit row/i }).click()

    await expect.poll(async () => {
      const bottomRowPanelBox = await requiredBox(invalidRowsPanel)
      const bottomRowNotesBox = await requiredBox(page.getByLabel('Notes'))

      return bottomRowNotesBox.y >= bottomRowPanelBox.y
        && bottomRowNotesBox.y + bottomRowNotesBox.height <= bottomRowPanelBox.y + bottomRowPanelBox.height
    }).toBe(true)

    await expectElementBottomVisible(invalidRowsPanel, page.getByLabel('Notes'))
  }
})
