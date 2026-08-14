import { describe, expect, it } from 'vitest'
import { DEFAULT_THEME, mergeMantineTheme } from '@mantine/core'
import { dfrntTheme, dfrntCssVariablesResolver } from '../mantineTheme'
import { airlineThemeOverride } from '../airlineMantineTheme'
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
})

describe('dfrntCssVariablesResolver', () => {
  // The resolver composes Mantine's v8CssVariablesResolver, so it needs the same
  // fully merged theme MantineProvider hands it — not a bare stub object.
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
    // red[8] (#6c1823) is unreadable on the charcoal page; we use a light red.
    expect(vars.dark['--mantine-color-error']).toBe('#e97b88')
  })

  it('keeps light-variant fills translucent (Mantine 9 made them solid)', () => {
    // The Alerts in PaxMobile use variant="light"; the DFRNT look wants the 8.x
    // alpha tint, which is why v8CssVariablesResolver is layered underneath.
    expect(vars.light['--mantine-color-red-light']).toMatch(/^rgba\(/)
    expect(vars.dark['--mantine-color-orange-light']).toMatch(/^rgba\(/)
  })

  it('keeps disabled input text legible in both schemes', () => {
    expect(vars.light['--mantine-color-disabled-color']).toBe('#57534e')
    expect(vars.dark['--mantine-color-disabled-color']?.toLowerCase()).toBe('#cac4d0')
    expect(vars.dark['--mantine-color-disabled']?.toLowerCase()).toBe('#28262c')
  })
})

// Every code in airlineBranding.ts — the palette rules below must hold for all of them.
const AIRLINE_CODES = [
  'NZ', 'QF', 'VA', 'JQ', 'ZL', 'AA', 'DL', 'UA', 'WN', 'B6', 'AS', 'F9', 'G4', 'NK', 'SQ',
  'NH', 'JL', 'KE', 'CI', 'BR', 'TG', 'MH', 'PR', 'VN', 'GA', 'CX', 'EK', 'QR', 'EY', 'FJ',
  'LA', 'AC', 'HA', 'BA', 'LH',
]

const DARK_SURFACE = '#2c2a30'

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

function shadeOf(code: string, scheme: 'light' | 'dark'): string {
  const override = airlineThemeOverride(getAirlineBrand(code))
  const shade = override.primaryShade as { light: number; dark: number }
  return override.colors!.brand![shade[scheme]]
}

describe('airlineThemeOverride', () => {
  it('regenerates the brand tuple from the airline primary hex', () => {
    const override = airlineThemeOverride(getAirlineBrand('QF')) // Qantas red
    const brand = override.colors?.brand
    expect(brand).toHaveLength(10)
    // Different from the house cyan tuple.
    expect(brand?.[5]).not.toBe(dfrntTheme.colors?.brand?.[5])
  })

  it('paints light mode in the airline own hex, not a generated stand-in', () => {
    // generateColors places the seed by luminance, so a fixed primaryShade shipped
    // Air NZ's Pacific teal as neon aqua (#56effa) and Qantas red as #fd1e35.
    for (const code of AIRLINE_CODES) {
      expect(shadeOf(code, 'light')).toBe(getAirlineBrand(code).primary.toLowerCase())
    }
  })

  it('keeps the dark-mode fill readable on the charcoal page surface', () => {
    // Near-black brands (Lufthansa #05164d) step up the ramp until they clear the
    // WCAG non-text minimum against --dd-surface.
    for (const code of AIRLINE_CODES) {
      expect(contrastRatio(shadeOf(code, 'dark'), DARK_SURFACE)).toBeGreaterThanOrEqual(3)
    }
  })

  it('keeps the DFRNT fonts and pill buttons when recoloured', () => {
    const override = airlineThemeOverride(getAirlineBrand('QF'))
    expect(override.primaryColor).toBe('brand')
    expect(override.components?.Button?.defaultProps).toMatchObject({ radius: 9999 })
  })

  it('carries a dark contrast text for light airline brands (Spirit NK)', () => {
    // Data-level guard: the yellow Spirit brand must not use white text.
    expect(getAirlineBrand('NK').contrastText.toLowerCase()).not.toBe('#ffffff')
  })
})
