import { describe, expect, it } from 'vitest'
import type { MantineTheme } from '@mantine/core'
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
  const vars = dfrntCssVariablesResolver({} as MantineTheme)

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

  it('keeps disabled input text legible in both schemes', () => {
    expect(vars.light['--mantine-color-disabled-color']).toBe('#57534e')
    expect(vars.dark['--mantine-color-disabled-color']?.toLowerCase()).toBe('#cac4d0')
    expect(vars.dark['--mantine-color-disabled']?.toLowerCase()).toBe('#28262c')
  })
})

describe('airlineThemeOverride', () => {
  it('regenerates the brand tuple from the airline primary hex', () => {
    const override = airlineThemeOverride(getAirlineBrand('QF')) // Qantas red
    const brand = override.colors?.brand
    expect(brand).toHaveLength(10)
    // Different from the house cyan tuple.
    expect(brand?.[5]).not.toBe(dfrntTheme.colors?.brand?.[5])
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
