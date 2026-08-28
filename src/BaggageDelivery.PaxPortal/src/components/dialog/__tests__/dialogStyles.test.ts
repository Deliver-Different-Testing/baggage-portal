import { describe, expect, it } from 'vitest'
import {
  dialogContentBg,
  dialogShellStyles,
  dialogSize,
  dialogStickyChromeStyle,
  headerChipProps,
  headerColors,
  headerOnColor,
  printedRadius,
  sectionPaperProps,
} from '../styles'
import { tokens } from '../../../styles/mantineTheme'

describe('dialog design language (ported from Despatch)', () => {
  it('restates the modal radius over the theme Paper default', () => {
    // Mantine renders Modal.Content as a Paper, so the theme's Paper defaults land
    // on it and --paper-radius (md, 12px) beats --modal-radius (xl, 28px) in the
    // cascade. Without this the shell silently rendered at 12px however `radius`
    // was set. The stray Paper hairline goes for the same reason.
    expect(dialogShellStyles.content.borderRadius).toBe('var(--modal-radius)')
    expect(dialogShellStyles.content.border).toBe('none')
  })

  it('keeps Modal.Content scrollable', () => {
    // It is the modal's only scroll container — Mantine caps it near 90dvh. Clipping
    // it strands the footer's confirm button with no scrollbar and no scroll-into-view.
    expect(dialogShellStyles.content.overflowY).toBe('auto')
    expect(dialogShellStyles.body.padding).toBe(0)
  })

  it('carries no purple header variant', () => {
    // The house rules reserve the grape ramp for AI/Auto-Mate surfaces, and this app
    // has none — so Despatch's `secondary` variant is deliberately not ported.
    expect(Object.keys(headerColors).sort()).toEqual(['error', 'primary', 'success', 'warning'])
  })

  it('states every header colour as a token, never a literal', () => {
    // Despatch hard-codes its fills; this app has to follow prefers-color-scheme.
    for (const { bg, fg } of Object.values(headerColors)) {
      expect(bg).toMatch(/^var\(--/)
      expect(fg).toMatch(/^var\(--/)
    }
  })

  it('pairs the cyan header with the ink on-colour the theme already states', () => {
    expect(headerColors.primary).toEqual({
      bg: 'var(--mantine-color-brand-5)',
      fg: 'var(--mantine-color-ink-9)',
    })
    expect(headerOnColor()).toBe(headerColors.primary.fg)
  })

  it('mixes the chip wash from the on-colour so it reads on a light or dark fill', () => {
    const chip = headerChipProps('warning')
    expect(chip.size).toBe(40)
    expect(chip.style['--ti-color']).toBe(headerColors.warning.fg)
    expect(chip.style['--ti-bg']).toContain('color-mix')
  })

  it('sits sections one tone above the content they sit on', () => {
    expect(dialogContentBg).toBe('var(--dd-surface)')
    expect(sectionPaperProps.radius).toBe('md')
  })

  it('leaves the printed radius to the docket, not the section container', () => {
    // round = you touch it, square = it's printed. The port gives the *container*
    // the design language's 12px; the docket lines inside keep their 2px.
    expect(printedRadius).toBe(tokens.radius.tile)
    expect(printedRadius).toBeLessThan(12)
  })

  it('pins the header and footer against the scroll container', () => {
    expect(dialogStickyChromeStyle('top')).toEqual({ position: 'sticky', top: 0, zIndex: 2 })
    expect(dialogStickyChromeStyle('bottom')).toEqual({ position: 'sticky', bottom: 0, zIndex: 2 })
  })

  it('keeps Despatch widths so a converted dialog does not collapse to the default', () => {
    expect(dialogSize).toEqual({ sm: 560, md: 760, lg: 1000 })
  })
})
