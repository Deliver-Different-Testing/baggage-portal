import { describe, expect, it } from 'vitest'
import { addressLabels, addressLines, isUnitedStates, sameDeliveryAddress } from './address'
import type { AddressDto } from '../api/client'

const nz: AddressDto = {
  line1: 'Sofitel Auckland',
  line2: 'Room 402',
  line3: '21',
  line4: 'Viaduct Harbour Ave',
  line5: 'Auckland CBD',
  line6: 'Auckland',
  line7: '1010',
  country: 'NZ',
}

describe('isUnitedStates', () => {
  it.each(['US', 'us', ' Us '])('treats %s as the United States', (value) => {
    expect(isUnitedStates(value)).toBe(true)
  })

  it.each(['NZ', '', null, undefined, 'USA'])('treats %s as elsewhere', (value) => {
    expect(isUnitedStates(value)).toBe(false)
  })
})

describe('addressLabels', () => {
  it('asks a New Zealand passenger for a suburb, city and postcode', () => {
    const labels = addressLabels('NZ')
    expect(labels.line5).toBe('Suburb')
    expect(labels.line6).toBe('City')
    expect(labels.line7).toBe('Postcode')
  })

  it('asks a US passenger for a city, state and ZIP code', () => {
    const labels = addressLabels('US')
    expect(labels.line5).toBe('City')
    expect(labels.line6).toBe('State')
    expect(labels.line7).toBe('ZIP code')
  })

  it('keeps the country-independent labels stable', () => {
    expect(addressLabels('US').line3).toBe(addressLabels('NZ').line3)
    expect(addressLabels('US').line4).toBe(addressLabels('NZ').line4)
  })
})

describe('addressLines', () => {
  it('joins the street number to the street name', () => {
    expect(addressLines(nz)[1]).toBe('21 Viaduct Harbour Ave')
  })

  it('lays the address out company, street, locality, country', () => {
    expect(addressLines(nz)).toEqual([
      'Sofitel Auckland',
      '21 Viaduct Harbour Ave',
      'Auckland CBD, Auckland 1010',
      'NZ',
    ])
  })

  it('omits the blanks rather than leaving gaps', () => {
    expect(
      addressLines({ line4: 'Test St', line5: 'Suburb', line6: 'Auckland', country: 'NZ' }),
    ).toEqual(['Test St', 'Suburb, Auckland', 'NZ'])
  })

  it('never renders line 2, which the docket shows on its own', () => {
    expect(addressLines(nz).join('|')).not.toContain('Room 402')
  })
})

describe('sameDeliveryAddress', () => {
  const base: AddressDto = {
    line1: null,
    line2: null,
    line3: '1',
    line4: 'Test Street',
    line5: 'Ponsonby',
    line6: 'Auckland',
    line7: '1011',
    country: 'NZ',
    latitude: -36.85,
    longitude: 174.76,
  }

  it('treats an identical address as unchanged', () => {
    expect(sameDeliveryAddress(base, { ...base })).toBe(true)
  })

  it('ignores coordinates, which a manual edit clears', () => {
    expect(sameDeliveryAddress(base, { ...base, latitude: null, longitude: null })).toBe(true)
  })

  it('ignores surrounding whitespace and blank-versus-null lines', () => {
    expect(sameDeliveryAddress(base, { ...base, line1: '  ', line4: ' Test Street ' })).toBe(true)
  })

  it.each(['line1', 'line2', 'line3', 'line4', 'line5', 'line6', 'line7'] as const)(
    'notices a change to %s',
    (key) => {
      expect(sameDeliveryAddress(base, { ...base, [key]: 'Different' })).toBe(false)
    },
  )

  it('notices a change of country', () => {
    expect(sameDeliveryAddress(base, { ...base, country: 'AU' })).toBe(false)
  })

  it('is case insensitive, matching the server comparison', () => {
    expect(sameDeliveryAddress(base, { ...base, line5: 'PONSONBY' })).toBe(true)
  })
})
