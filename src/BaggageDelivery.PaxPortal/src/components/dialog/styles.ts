/**
 * Shared tokens for the DFRNT dialog design language.
 *
 * Ported from despatchweb's `components/dialogs/shared/mantine/styles.ts` so a
 * dialog here and a dialog in Despatch wear the same chrome. Three deliberate
 * differences, all forced by this app rather than by taste:
 *
 * 1. **No `secondary` (purple) variant.** The house rules reserve the `grape`
 *    ramp for AI/Auto-Mate surfaces and this app has none.
 * 2. **No tenant switch.** Despatch picks Cyan or gold per tenant; the Feb 2026
 *    guidelines give every tenant the one cyan, so `primary` is simply cyan.
 * 3. **No literal colours.** Despatch writes `#ffffff` / `gray-1` / `gray-3`,
 *    which are light-mode values. Everything here reads from the `--dd-*` ladder
 *    so the dialog follows `prefers-color-scheme` like the rest of the app.
 */
import type { CSSProperties } from 'react'
import { tokens } from '../../styles/mantineTheme'

/** Header fills. `error`/`warning` are for destructive and cautionary dialogs. */
export type HeaderVariant = 'primary' | 'success' | 'warning' | 'error'

/**
 * The solid header fill and the colour that sits on it.
 *
 * Fixed shades rather than the `-filled` vars, and so fixed across both colour
 * schemes. The bar is a brand surface, the same way the Ink hero is: it keeps its
 * own contrast and does not flip. `-filled` would step to shade 4 in dark mode and
 * drop white-on-green to about 2.2:1.
 */
export const headerColors: Record<HeaderVariant, { bg: string; fg: string }> = {
  // Ink on cyan, ~8:1 — the pairing the theme already states as --dd-on-brand-fill.
  primary: { bg: 'var(--mantine-color-brand-5)', fg: 'var(--mantine-color-ink-9)' },
  success: { bg: 'var(--mantine-color-green-6)', fg: 'var(--mantine-color-white)' },
  // Orange is a light fill, so it takes the dark on-colour as cyan does.
  warning: { bg: 'var(--mantine-color-orange-5)', fg: 'var(--mantine-color-ink-9)' },
  error: { bg: 'var(--mantine-color-red-6)', fg: 'var(--mantine-color-white)' },
}

/** The header's on-colour — title, subtitle, chip glyph and close button. */
export function headerOnColor(variant: HeaderVariant = 'primary'): string {
  return headerColors[variant].fg
}

/**
 * A translucent wash of the on-colour, for the icon chip. Mixed from the
 * on-colour rather than a fixed white alpha so it reads on a light fill (cyan,
 * orange) as well as a dark one.
 */
export function headerOverlayColor(opacity: number, variant: HeaderVariant = 'primary'): string {
  return `color-mix(in srgb, ${headerColors[variant].fg} ${opacity * 100}%, transparent)`
}

/**
 * The square glyph chip in the header. Spread onto a `<ThemeIcon>`, which brings
 * the box and the centring; the icon sets its own size at the call site.
 */
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

/** The content region behind the sections — one tone below them, as in Despatch. */
export const dialogContentBg = 'var(--dd-surface)'

/**
 * A section inside dialog content: flat, keylined, 12px corner. Spread onto a
 * `<Paper {...sectionPaperProps}>`.
 *
 * The 12px is the *container*. Anything printed inside it — a docket line, the
 * chosen window, a file reference — keeps `tokens.radius.tile`, so the app's
 * round-you-touch-it / square-it's-printed rule survives the port.
 */
export const sectionPaperProps = {
  radius: 'md',
  p: 'md',
} as const

/** The quiet label above a section. */
export const sectionLabelProps = {
  size: 'sm',
  c: 'dimmed',
  fw: 500,
  mb: 'xs',
} as const

/**
 * The shell's Mantine `styles`.
 *
 * `Modal.Content` is the modal's only scroll container — Mantine caps it near
 * 90dvh and scrolls it — so `overflowY` must stay `auto`. Clipping it strands
 * everything past the cap (usually the confirm button) with no scrollbar and no
 * scroll-into-view on focus. The corners still clip to the shell radius; any
 * non-`visible` overflow does that.
 *
 * `borderRadius` and `border` have to be restated. Mantine renders `Modal.Content`
 * as a Paper, so the theme's `Paper` defaults land on it: `--paper-radius` (md,
 * 12px) wins over `--modal-radius` (xl, 28px) in the cascade, and the card hairline
 * the theme gives every Paper draws an outline the dialog does not want. Pointing
 * the radius back at `--modal-radius` keeps `radius` on the shell meaningful.
 */
export const dialogShellStyles = {
  content: {
    overflowY: 'auto',
    backgroundColor: dialogContentBg,
    borderRadius: 'var(--modal-radius)',
    border: 'none',
  },
  body: { padding: 0 },
} satisfies Record<'content' | 'body', CSSProperties>

/**
 * Pins the header and footer against that scroll container so the title and the
 * actions stay put while a long docket scrolls under them. Both bars paint an
 * opaque fill, so nothing shows through.
 */
export function dialogStickyChromeStyle(edge: 'top' | 'bottom'): CSSProperties {
  return { position: 'sticky', [edge]: 0, zIndex: 2 }
}

/** Widths matching Despatch's `dialogSize`. */
export const dialogSize = { sm: 560, md: 760, lg: 1000 } as const

/** Re-exported so section content can reach the printed radius without a second import. */
export const printedRadius = tokens.radius.tile
