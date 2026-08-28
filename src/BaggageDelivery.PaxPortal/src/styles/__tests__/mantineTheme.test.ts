import { describe, expect, it } from 'vitest'
import { DEFAULT_THEME, mergeMantineTheme } from '@mantine/core'
import { dfrntTheme, dfrntCssVariablesResolver, tokens } from '../mantineTheme'
import { airlineAccent } from '../airlineAccent'
import { getAirlineBrand } from '../airlineBranding'

describe('dfrntTheme', () => {
  it('uses the DFRNT cyan brand primary', () => {
    expect(dfrntTheme.primaryColor).toBe('brand')
    expect(dfrntTheme.colors?.brand?.[5]).toBe('#3bc7f4')
  })

  it('uses Ink Blue as the black token', () => {
    expect(dfrntTheme.black).toBe('#0d0c2c')
  })

  it('makes buttons pill-shaped (lozenge)', () => {
    expect(dfrntTheme.components?.Button?.defaultProps).toMatchObject({ radius: 9999 })
  })

  it('declares the brand family without nesting one quote style inside another', () => {
    const families = [dfrntTheme.fontFamily, dfrntTheme.headings?.fontFamily]
    for (const family of families) {
      expect(family).toBeTruthy()
      expect(family).toContain('Plus Jakarta Sans Variable')
      expect(family).not.toMatch(/'"|"'/)
    }
  })

  it('sizes form controls for a thumb, not a mouse', () => {
    for (const control of ['TextInput', 'Textarea', 'Select', 'Autocomplete', 'Checkbox', 'Radio'] as const) {
      expect(dfrntTheme.components?.[control]?.defaultProps).toMatchObject({ size: 'md' })
    }
  })

  it('carries one spec for the full-width primary button', () => {
    expect(tokens.button.primary).toEqual({
      minHeight: 56,
      fontSize: 16,
      fontWeight: 700,
      letterSpacing: '0.01em',
    })
  })

  it('gives section headings a step of their own above body copy', () => {
    expect(tokens.type.sectionTitle.fontSize).toBeGreaterThan(16)
    expect(tokens.type.sectionTitle.fontWeight).toBeGreaterThanOrEqual(600)
  })

  it('states the hero H1 line-height so the two heroes cannot drift', () => {
    expect(tokens.type.heroTitle.lineHeight).toBe(1.05)
    expect(tokens.type.heroTitle.fontSize).toBe(tokens.type.hero)
  })

  it('separates the printed radius from the touchable ones', () => {
    expect(tokens.radius.tile).toBe(2)
    expect(tokens.radius.tile).toBeLessThan(tokens.radius.sm)
  })

  it('carries one eyebrow spec for every micro-label', () => {
    expect(tokens.type.eyebrow).toEqual({
      fontSize: 10,
      fontWeight: 700,
      letterSpacing: '0.12em',
      textTransform: 'uppercase',
    })
  })

  it('gives cards a hairline so they separate from the page', () => {
    expect(dfrntTheme.components?.Card?.styles).toMatchObject({
      root: { border: '1px solid var(--dd-outline-variant)' },
    })
  })
})

describe('dfrntCssVariablesResolver', () => {
  const vars = dfrntCssVariablesResolver(mergeMantineTheme(DEFAULT_THEME, dfrntTheme))

  it('maps the page surface per colour scheme (warm grey light, charcoal dark)', () => {
    expect(vars.light['--dd-surface']).toBe('#f4f2f1')
    expect(vars.dark['--dd-surface']).toBe('#2c2a30')
  })

  it('maps cards a tone above the page in each scheme', () => {
    expect(vars.light['--dd-surface-container']).toBe('#ffffff')
    expect(vars.dark['--dd-surface-container']).toBe('#37353c')
  })

  it('lifts the dark error colour off Mantine near-invisible red[8] default', () => {
    expect(vars.dark['--mantine-color-error']).toBe('#e97b88')
  })

  it('keeps light-variant fills translucent (Mantine 9 made them solid)', () => {
    expect(vars.light['--mantine-color-red-light']).toMatch(/^rgba\(/)
    expect(vars.dark['--mantine-color-orange-light']).toMatch(/^rgba\(/)
  })

  it('keeps disabled input text legible in both schemes', () => {
    expect(vars.light['--mantine-color-disabled-color']).toBe('#57534e')
    expect(vars.dark['--mantine-color-disabled-color']?.toLowerCase()).toBe('#cac4d0')
    expect(vars.dark['--mantine-color-disabled']?.toLowerCase()).toBe('#28262c')
  })

  it('exposes the hairline colour the card border and docket tiles read from', () => {
    expect(vars.light['--dd-outline-variant']).toBe('#e7e5e4')
    expect(vars.dark['--dd-outline-variant']).toBe('#56535c')
  })

  it('puts Ink on the solid cyan fill in both schemes', () => {
    expect(vars.light['--dd-on-brand-fill']).toBe('#0d0c2c')
    expect(vars.dark['--dd-on-brand-fill']).toBe('#0d0c2c')
    expect(contrastRatio('#0d0c2c', '#3bc7f4')).toBeGreaterThanOrEqual(4.5)
  })

  it('flips the on-tint colour per scheme rather than trusting -light-color', () => {
    expect(vars.light['--dd-on-brand-tint']).toBe('#0f6f96')
    expect(vars.dark['--dd-on-brand-tint']).toBe('#b1e9fb')
  })

  it('states the checkbox and radio glyph colour instead of inferring it', () => {
    expect(dfrntTheme.components?.Checkbox?.defaultProps).toMatchObject({
      iconColor: 'var(--dd-on-brand-fill)',
    })
    expect(dfrntTheme.components?.Radio?.defaultProps).toMatchObject({
      iconColor: 'var(--dd-on-brand-fill)',
    })
  })
})

const AIRLINE_CODES = [
  'NZ', 'QF', 'VA', 'JQ', 'ZL', 'AA', 'DL', 'UA', 'WN', 'B6', 'AS', 'F9', 'G4', 'NK', 'SQ',
  'NH', 'JL', 'KE', 'CI', 'BR', 'TG', 'MH', 'PR', 'VN', 'GA', 'CX', 'EK', 'QR', 'EY', 'FJ',
  'LA', 'AC', 'HA', 'BA', 'LH',
]

const INK_HERO = '#0d0c2c'

function relativeLuminance(hex: string): number {
  const v = hex.replace('#', '')
  const [r, g, b] = [0, 2, 4].map((i) => {
    const c = parseInt(v.slice(i, i + 2), 16) / 255
    return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4
  })
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

function contrastRatio(a: string, b: string): number {
  const [la, lb] = [relativeLuminance(a), relativeLuminance(b)]
  return (Math.max(la, lb) + 0.05) / (Math.min(la, lb) + 0.05)
}

describe('airlineAccent', () => {
  it('never recolours the DFRNT brand ramp', () => {
    expect(dfrntTheme.colors?.brand?.[5]).toBe('#3bc7f4')
    for (const code of AIRLINE_CODES) {
      expect(airlineAccent(getAirlineBrand(code)).accent).not.toBe(
        dfrntTheme.colors?.brand?.[5],
      )
    }
  })

  it('keeps every carrier accent readable on the Ink hero', () => {
    for (const code of AIRLINE_CODES) {
      expect(
        contrastRatio(airlineAccent(getAirlineBrand(code)).accent, INK_HERO),
      ).toBeGreaterThanOrEqual(3)
    }
  })

  it('keeps the seed hex wherever the seed is already legible', () => {
    expect(airlineAccent(getAirlineBrand('NK')).accent).toBe('#fff200')
    expect(airlineAccent(getAirlineBrand('QF')).accent).toBe('#e0001b')
  })

  it('steps near-black carriers up until they clear the Ink hero', () => {
    const lh = airlineAccent(getAirlineBrand('LH'))
    expect(lh.accent).not.toBe('#05164d')
    expect(contrastRatio('#05164d', INK_HERO)).toBeLessThan(3)
    expect(contrastRatio(lh.accent, INK_HERO)).toBeGreaterThanOrEqual(3)
  })

  it('knocks the chip fill back so a white glyph still reads on it', () => {
    expect(airlineAccent(getAirlineBrand('NZ')).accentTint).toMatch(/^rgba\(/)
  })

  it('carries a dark contrast text for light airline brands (Spirit NK)', () => {
    expect(getAirlineBrand('NK').contrastText.toLowerCase()).not.toBe('#ffffff')
  })
})
