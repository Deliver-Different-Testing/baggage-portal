import { describe, expect, it } from 'vitest'
import { getAirlineBrand } from './airlineBranding'

describe('getAirlineBrand', () => {
  it('returns the configured brand for a known code', () => {
    expect(getAirlineBrand('QF').primary).toBe('#e0001b')
    expect(getAirlineBrand('NZ').primary).toBe('#008c95')
    expect(getAirlineBrand('SQ').primary).toBe('#1d3c6e')
  })

  it('covers the major NZ/AU/US carriers and international airlines', () => {
    const codes = [
      'NZ', 'QF', 'VA', 'JQ', 'ZL', 'AA', 'DL', 'UA', 'WN', 'B6', 'AS', 'F9', 'G4', 'NK', 'SQ',
      'NH', 'JL', 'KE', 'CI', 'BR', 'TG', 'MH', 'PR', 'VN', 'GA', 'CX', 'EK', 'QR', 'EY', 'FJ',
      'LA', 'AC', 'HA', 'BA', 'LH',
    ]
    const fallback = getAirlineBrand('ZZ').primary
    for (const code of codes) {
      expect(getAirlineBrand(code).primary).not.toBe(fallback)
    }
  })

  it('uses dark contrast text for light primaries (Spirit)', () => {
    const nk = getAirlineBrand('NK')
    expect(nk.primary).toBe('#fff200')
    expect(nk.contrastText).toBe('rgba(0, 0, 0, 0.87)')
  })

  it('is case-insensitive and trims whitespace', () => {
    expect(getAirlineBrand(' qf ')).toEqual(getAirlineBrand('QF'))
  })

  it('falls back to the default blue for unknown or missing codes', () => {
    const fallback = getAirlineBrand(undefined)
    expect(fallback.primary).toBe('#2196f3')
    expect(getAirlineBrand(null)).toEqual(fallback)
    expect(getAirlineBrand('')).toEqual(fallback)
    expect(getAirlineBrand('ZZ')).toEqual(fallback)
  })
})
