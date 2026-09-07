import { describe, expect, it } from 'vitest'
import { addressLabels } from '../../utils/address'
import {
  atlNotesAreRequired,
  computeFieldErrors,
  orderedErrors,
  type ConfirmFormValues,
} from './confirmValidation'

const nzLabels = addressLabels('NZ')
const usLabels = addressLabels('US')

const valid: ConfirmFormValues = {
  passengerName: 'Test Passenger',
  passengerPhone: '+64211234567',
  passengerEmail: 'test@example.com',
  address: {
    line3: '123',
    line4: 'Test St',
    line5: 'Suburb',
    line6: 'Auckland',
    line7: '1010',
    country: 'NZ',
  },
  addressConfirmed: true,
  hasDeliveryTime: true,
  atlOptionName: undefined,
  accessNotes: '',
}

const errorsFor = (overrides: Partial<ConfirmFormValues>, labels = nzLabels) =>
  computeFieldErrors({ ...valid, ...overrides }, labels)

describe('computeFieldErrors', () => {
  it('finds nothing wrong with a complete booking', () => {
    expect(errorsFor({})).toEqual({})
  })

  it('asks for a name', () => {
    expect(errorsFor({ passengerName: '   ' }).passengerName).toBe('Please enter your full name.')
  })

  it('distinguishes a missing phone from an unusable one', () => {
    expect(errorsFor({ passengerPhone: '' }).passengerPhone).toBe('Please enter your phone number.')
    expect(errorsFor({ passengerPhone: '12345' }).passengerPhone).toBe(
      'Please enter a valid phone number.',
    )
  })

  it('counts digits, not characters, in a phone number', () => {
    expect(errorsFor({ passengerPhone: '(09) 123-4567' }).passengerPhone).toBeUndefined()
    expect(errorsFor({ passengerPhone: '(09) 12-34' }).passengerPhone).toBe(
      'Please enter a valid phone number.',
    )
  })

  it('distinguishes a missing email from a malformed one', () => {
    expect(errorsFor({ passengerEmail: '' }).passengerEmail).toBe(
      'Please enter your email address.',
    )
    expect(errorsFor({ passengerEmail: 'nope' }).passengerEmail).toBe(
      'Please enter a valid email address.',
    )
  })

  it('names the missing address field the way the form labels it', () => {
    const nz = errorsFor({ address: { ...valid.address, line5: '', line7: '' } })
    expect(nz.line5).toBe('Please enter your suburb.')
    expect(nz.line7).toBe('Please enter your postcode.')

    const us = errorsFor(
      { address: { ...valid.address, line5: '', line6: '', line7: '', country: 'US' } },
      usLabels,
    )
    expect(us.line5).toBe('Please enter your city.')
    expect(us.line6).toBe('Please enter your state.')
    expect(us.line7).toBe('Please enter your zip code.')
  })

  it('treats a null postcode as missing', () => {
    expect(errorsFor({ address: { ...valid.address, line7: null } }).line7).toBe(
      'Please enter your postcode.',
    )
  })

  it('asks for a street number', () => {
    expect(errorsFor({ address: { ...valid.address, line3: '  ' } }).line3).toBe(
      'Please enter your street number.',
    )
    expect(errorsFor({ address: { ...valid.address, line3: null } }).line3).toBe(
      'Please enter your street number.',
    )
  })

  it('blocks until the address is ticked as correct', () => {
    expect(errorsFor({ addressConfirmed: false }).addressConfirmed).toBe(
      'Please confirm your delivery address is correct.',
    )
  })

  it('asks for a country', () => {
    expect(errorsFor({ address: { ...valid.address, country: '' } }).country).toBe(
      'Please enter your country.',
    )
  })

  it('blocks until a window with a real run time is picked', () => {
    expect(errorsFor({ hasDeliveryTime: false }).slot).toBe('Please pick a delivery window.')
  })

  it('requires access notes only for a safe place', () => {
    expect(errorsFor({ atlOptionName: 'Safe Place' }).accessNotes).toBe(
      'Please describe the safe place to leave your baggage.',
    )
    expect(errorsFor({ atlOptionName: 'Front door' }).accessNotes).toBeUndefined()
    expect(
      errorsFor({ atlOptionName: 'Safe Place', accessNotes: 'Behind the bin' }).accessNotes,
    ).toBeUndefined()
  })
})

describe('atlNotesAreRequired', () => {
  it('matches a safe place regardless of case or padding', () => {
    expect(atlNotesAreRequired('  safe PLACE ')).toBe(true)
  })

  it('does not match other options, or none at all', () => {
    expect(atlNotesAreRequired('Front door')).toBe(false)
    expect(atlNotesAreRequired(undefined)).toBe(false)
  })
})

describe('orderedErrors', () => {
  it('lists errors in the order the fields appear on the page', () => {
    const errors = errorsFor({
      passengerName: '',
      hasDeliveryTime: false,
      addressConfirmed: false,
      address: { ...valid.address, line3: '', line5: '' },
    })

    expect(orderedErrors(errors).map((e) => e.key)).toEqual([
      'passengerName',
      'line3',
      'line5',
      'addressConfirmed',
      'slot',
    ])
  })

  it('carries the same message the field shows inline', () => {
    const errors = errorsFor({ hasDeliveryTime: false })

    expect(orderedErrors(errors)).toEqual([
      { key: 'slot', message: 'Please pick a delivery window.' },
    ])
  })

  it('returns nothing for a clean form', () => {
    expect(orderedErrors({})).toEqual([])
  })
})
