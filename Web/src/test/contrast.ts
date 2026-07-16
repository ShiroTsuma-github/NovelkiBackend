import { expect } from 'vitest'

const foregroundColors: Record<string, string> = {
  'text-white': '#ffffff',
  'text-slate-50': '#f8fafc',
  'text-slate-100': '#eef2f7',
  'text-slate-200': '#cbd2dc',
  'text-slate-300': '#cbd2dc',
  'text-slate-400': '#9aa5b4',
  'text-slate-500': '#9aa5b4',
  'text-slate-600': '#cbd2dc',
  'text-slate-700': '#cbd2dc',
  'text-slate-950': '#eef2f7',
}

const backgroundColors: Record<string, string> = {
  'bg-white': '#0d121a',
  'bg-slate-50': '#05070b',
  'bg-slate-100': '#121925',
  'bg-slate-900': '#121925',
  'bg-slate-950': '#0d121a',
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

  return '#eef2f7'
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
