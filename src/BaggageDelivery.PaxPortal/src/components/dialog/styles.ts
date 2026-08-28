import type { CSSProperties } from 'react'
import { tokens } from '../../styles/mantineTheme'

export type HeaderVariant = 'primary' | 'success' | 'warning' | 'error'

export const headerColors: Record<HeaderVariant, { bg: string; fg: string }> = {
  primary: { bg: 'var(--mantine-color-brand-5)', fg: 'var(--mantine-color-ink-9)' },
  success: { bg: 'var(--mantine-color-green-6)', fg: 'var(--mantine-color-white)' },
  warning: { bg: 'var(--mantine-color-orange-5)', fg: 'var(--mantine-color-ink-9)' },
  error: { bg: 'var(--mantine-color-red-6)', fg: 'var(--mantine-color-white)' },
}

export function headerOnColor(variant: HeaderVariant = 'primary'): string {
  return headerColors[variant].fg
}

export function headerOverlayColor(opacity: number, variant: HeaderVariant = 'primary'): string {
  return `color-mix(in srgb, ${headerColors[variant].fg} ${opacity * 100}%, transparent)`
}

export function headerChipProps(variant: HeaderVariant = 'primary', size = 40) {
  return {
    size,
    radius: 'md' as const,
    style: {
      '--ti-bg': headerOverlayColor(0.18, variant),
      '--ti-color': headerOnColor(variant),
    } as CSSProperties & Record<`--${string}`, string>,
  }
}

export const dialogContentBg = 'var(--dd-surface)'

export const sectionPaperProps = {
  radius: 'md',
  p: 'md',
} as const

export const sectionLabelProps = {
  size: 'sm',
  c: 'dimmed',
  fw: 500,
  mb: 'xs',
} as const

export const dialogShellStyles = {
  content: {
    overflowY: 'auto',
    backgroundColor: dialogContentBg,
    borderRadius: 'var(--modal-radius)',
    border: 'none',
  },
  body: { padding: 0 },
} satisfies Record<'content' | 'body', CSSProperties>

export function dialogStickyChromeStyle(edge: 'top' | 'bottom'): CSSProperties {
  return { position: 'sticky', [edge]: 0, zIndex: 2 }
}

export const dialogSize = { sm: 560, md: 760, lg: 1000 } as const

export const printedRadius = tokens.radius.tile
