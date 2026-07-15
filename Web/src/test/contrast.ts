import { expect } from 'vitest'

const foregroundColors: Record<string, string> = {
  'text-white': '#ffffff',
  'text-slate-50': '#f8fafc',
  'text-slate-100': '#f1f5f9',
  'text-slate-200': '#e2e8f0',
  'text-slate-300': '#cbd5e1',
  'text-slate-400': '#64748b',
  'text-slate-500': '#94a3b8',
  'text-slate-600': '#cbd5e1',
  'text-slate-700': '#cbd5e1',
  'text-slate-950': '#f8fafc',
}

const backgroundColors: Record<string, string> = {
  'bg-white': '#0f172a',
  'bg-slate-50': '#020617',
  'bg-slate-100': '#1e293b',
  'bg-slate-900': '#0f172a',
  'bg-slate-950': '#0f172a',
}

export function expectReadableTextContrast(element: Element, minimumRatio = 4.5) {
  const foreground = resolveForeground(element)
  const background = resolveBackground(element)
  const ratio = contrastRatio(foreground, background)

  expect(
    ratio,
    `Expected "${element.textContent?.trim()}" contrast ${ratio.toFixed(2)} to be at least ${minimumRatio}. foreground=${foreground}, background=${background}`,
  ).toBeGreaterThanOrEqual(minimumRatio)
}

function resolveForeground(element: Element): string {
  for (let current: Element | null = element; current; current = current.parentElement) {
    const color = findClassColor(current, foregroundColors)
    if (color) {
      return color
    }
  }

  return '#e5e7eb'
}

function resolveBackground(element: Element): string {
  for (let current: Element | null = element; current; current = current.parentElement) {
    const color = findClassColor(current, backgroundColors)
    if (color) {
      return color
    }
  }

  return '#020617'
}

function findClassColor(element: Element, colors: Record<string, string>) {
  for (const className of element.classList) {
    const color = colors[className]
    if (color) {
      return color
    }
  }

  return null
}

function contrastRatio(foreground: string, background: string) {
  const foregroundLuminance = relativeLuminance(hexToRgb(foreground))
  const backgroundLuminance = relativeLuminance(hexToRgb(background))
  const lighter = Math.max(foregroundLuminance, backgroundLuminance)
  const darker = Math.min(foregroundLuminance, backgroundLuminance)
  return (lighter + 0.05) / (darker + 0.05)
}

function relativeLuminance([red, green, blue]: [number, number, number]) {
  const [r, g, b] = [red, green, blue].map((value) => {
    const normalized = value / 255
    return normalized <= 0.03928
      ? normalized / 12.92
      : ((normalized + 0.055) / 1.055) ** 2.4
  })

  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

function hexToRgb(value: string): [number, number, number] {
  return [
    Number.parseInt(value.slice(1, 3), 16),
    Number.parseInt(value.slice(3, 5), 16),
    Number.parseInt(value.slice(5, 7), 16),
  ]
}
