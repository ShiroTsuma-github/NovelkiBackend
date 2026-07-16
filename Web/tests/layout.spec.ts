import { expect, type Locator, type Page, test } from '@playwright/test'

const books = [
  {
    id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    created: '2026-01-01T10:00:00Z',
    lastModified: '2026-02-01T10:00:00Z',
    primaryTitle: 'Lord of Mysteries',
    description: 'A long fantasy mystery.',
    alternativeTitles: ['LOTM'],
    authorId: 'author-1',
    author: 'Cuttlefish',
    contentType: 'Novel',
    status: 'Reading',
    currentChapterNumber: 348,
    currentChapterLabel: '348',
    totalChapters: 1432,
    rating: 9,
    priority: 1,
    notes: 'Private note',
    progressHistory: [],
    genres: ['Fantasy'],
    tags: ['favorite', 'mystery'],
    links: [],
  },
  {
    id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    created: '2026-01-02T10:00:00Z',
    lastModified: '2026-02-02T10:00:00Z',
    primaryTitle: 'A Very Long Book Title That Should Stay Inside Its Card Container Without Pushing Actions Away',
    alternativeTitles: [],
    author: 'Toika',
    contentType: 'Novel',
    status: 'Completed',
    currentChapterNumber: 120,
    totalChapters: 120,
    rating: null,
    priority: 2,
    progressHistory: [],
    genres: ['Slice of Life'],
    tags: ['completed'],
    links: [],
  },
]

const dictionaries = {
  type: [
    { id: '10000000-0000-0000-0000-000000000001', name: 'Novel' },
    { id: '10000000-0000-0000-0000-000000000002', name: 'Manga' },
  ],
  status: [
    { id: '20000000-0000-0000-0000-000000000001', name: 'Reading' },
    { id: '20000000-0000-0000-0000-000000000002', name: 'Completed' },
  ],
  genre: [
    { id: '30000000-0000-0000-0000-000000000001', name: 'Fantasy' },
    { id: '30000000-0000-0000-0000-000000000002', name: 'Slice of Life' },
  ],
}

test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    window.localStorage.setItem('novelki.session', JSON.stringify({
      accessToken: 'header.eyJyb2xlIjoiVXNlciJ9.signature',
      refreshToken: 'refresh-token',
      tokenType: 'Bearer',
      expiresAt: '2099-01-01T00:00:00Z',
      refreshTokenExpiresAt: '2099-02-01T00:00:00Z',
      userId: '11111111-1111-1111-1111-111111111111',
    }))
  })
  await mockApi(page)
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
  expect(actionsBackground).toBe('rgb(30, 41, 59)')
  expect(alternativeBadgeBackground).toBe('rgb(71, 85, 105)')
  expect(alternativeBadgeBackground).not.toBe(titleBackground)
})

test('cards layout constrains long titles and shows active toggle styles', async ({ page }) => {
  await page.goto('/books')
  await page.getByRole('button', { name: /cards/i }).click()

  const cardsButton = page.getByRole('button', { name: /cards/i })
  const tableButton = page.getByRole('button', { name: /table/i })
  await expect(cardsButton).toHaveCSS('color', 'rgb(255, 255, 255)')
  await expect(tableButton).not.toHaveCSS('color', 'rgb(255, 255, 255)')

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
    const updatedPanelBox = await requiredBox(invalidRowsPanel)
    const notesBox = await requiredBox(page.getByLabel('Notes'))

    expect(notesBox.y).toBeGreaterThanOrEqual(updatedPanelBox.y)
    expect(notesBox.y + notesBox.height).toBeLessThanOrEqual(updatedPanelBox.y + updatedPanelBox.height)

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

    const bottomRowPanelBox = await requiredBox(invalidRowsPanel)
    const bottomRowNotesBox = await requiredBox(page.getByLabel('Notes'))
    expect(bottomRowNotesBox.y).toBeGreaterThanOrEqual(bottomRowPanelBox.y)
    expect(bottomRowNotesBox.y + bottomRowNotesBox.height).toBeLessThanOrEqual(bottomRowPanelBox.y + bottomRowPanelBox.height)
  }
})

async function mockApi(page: Page) {
  await page.route('**/api/v1/**', async (route) => {
    const url = new URL(route.request().url())
    const path = url.pathname.replace('/api/v1/', '')

    if (path === 'book/import/sessions') {
      await route.fulfill({ json: invalidImportSession })
      return
    }

    if (path === 'book') {
      await route.fulfill({ json: { skip: 0, take: 20, total: books.length, data: books } })
      return
    }

    if (path.startsWith('book/')) {
      await route.fulfill({ json: books[0] })
      return
    }

    if (path in dictionaries) {
      await route.fulfill({
        json: {
          skip: 0,
          take: 100,
          total: dictionaries[path as keyof typeof dictionaries].length,
          data: dictionaries[path as keyof typeof dictionaries],
        },
      })
      return
    }

    await route.fulfill({ status: 204 })
  })
}

const invalidImportSession = {
  sessionId: 'layout-session',
  fileName: 'invalid-books.csv',
  totalRows: 80,
  validRows: 0,
  invalidRows: 80,
  canFinalize: false,
  availableContentTypes: ['Novel', 'Manga'],
  availableStatuses: ['Reading', 'Completed'],
  rows: Array.from({ length: 80 }, (_unused, index) => ({
    rowId: `layout-row-${index + 1}`,
    lineNumber: index + 2,
    isValid: false,
    primaryTitle: `Broken imported title ${index + 1}`,
    authorName: null,
    contentType: 'Wrong',
    status: 'Reading',
    tags: null,
    totalChapters: null,
    currentChapterNumber: null,
    currentChapterLabel: null,
    rating: null,
    priority: null,
    description: null,
    notes: null,
    rawImportedLine: null,
    errors: ['Content type is required and must exist.'],
    fieldErrors: {
      contentType: ['Content type is required and must exist.'],
    },
  })),
}

async function expectNoHorizontalOverflow(page: Page) {
  const hasOverflow = await page.evaluate(() => {
    const root = document.documentElement
    const body = document.body
    return root.scrollWidth > root.clientWidth + 1 || body.scrollWidth > body.clientWidth + 1
  })
  expect(hasOverflow).toBe(false)
}

async function expectNoVerticalDocumentOverflow(page: Page) {
  const hasOverflow = await page.evaluate(() => {
    const root = document.documentElement
    const body = document.body
    return root.scrollHeight > root.clientHeight + 1 || body.scrollHeight > body.clientHeight + 1
  })
  expect(hasOverflow).toBe(false)
}

async function backgroundColor(locator: Locator) {
  return locator.evaluate((element) => getComputedStyle(element).backgroundColor)
}

async function expectInViewport(locator: Locator) {
  const box = await requiredBox(locator)
  const viewport = locator.page().viewportSize()
  expect(viewport).not.toBeNull()
  expect(box.x).toBeGreaterThanOrEqual(0)
  expect(box.y).toBeGreaterThanOrEqual(0)
  expect(box.x + box.width).toBeLessThanOrEqual(viewport!.width + 8)
  expect(box.y).toBeLessThanOrEqual(viewport!.height + 1)
}

async function expectFitsViewportWidth(locator: Locator) {
  const box = await requiredBox(locator)
  const viewport = locator.page().viewportSize()
  expect(viewport).not.toBeNull()
  expect(box.x).toBeGreaterThanOrEqual(0)
  expect(box.x + box.width).toBeLessThanOrEqual(viewport!.width + 8)
}

async function expectCenteredInViewport(locator: Locator) {
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

async function requiredBox(locator: Locator) {
  const box = await locator.boundingBox()
  expect(box).not.toBeNull()
  return box!
}
