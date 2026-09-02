import type { AddressDto } from '../api/client'

export function isUnitedStates(country?: string | null): boolean {
  return (country ?? '').trim().toUpperCase() === 'US'
}

export function addressLabels(country?: string | null) {
  const us = isUnitedStates(country)
  return {
    line1: 'Hotel or company',
    line2: 'Apartment, unit or suite',
    line3: 'Street number',
    line4: 'Street name',
    line5: us ? 'City' : 'Suburb',
    line6: us ? 'State' : 'City',
    line7: us ? 'ZIP code' : 'Postcode',
    country: 'Country',
  }
}

export function addressLines(a: AddressDto): string[] {
  const street = [a.line3, a.line4].map((p) => (p ?? '').trim()).filter(Boolean).join(' ')
  const locality = [
    [a.line5, a.line6].map((p) => (p ?? '').trim()).filter(Boolean).join(', '),
    a.line7,
  ]
    .map((part) => (part ?? '').trim())
    .filter(Boolean)
    .join(' ')
  return [a.line1, street, locality, a.country]
    .map((line) => (line ?? '').trim())
    .filter(Boolean)
}
