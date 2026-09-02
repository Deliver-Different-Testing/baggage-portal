import type { AddressDto } from '../../api/client'
import type { addressLabels } from '../../utils/address'

export type AddressLabels = ReturnType<typeof addressLabels>

export const ADDRESS_FIELD_KEYS = ['line4', 'line5', 'line6', 'line7', 'country'] as const

const ERROR_ORDER = [
  'slot',
  'line4',
  'line5',
  'line6',
  'line7',
  'country',
  'passengerName',
  'passengerPhone',
  'passengerEmail',
  'accessNotes',
] as const

const MIN_PHONE_DIGITS = 7

export function fieldTargetId(key: string): string {
  return key === 'slot' ? 'pax-delivery-window' : `pax-field-${key}`
}

export interface ConfirmFormValues {
  passengerName: string
  passengerPhone: string
  passengerEmail: string
  address: AddressDto
  hasDeliveryTime: boolean
  atlOptionName: string | undefined
  accessNotes: string
}

export function atlNotesAreRequired(atlOptionName: string | undefined): boolean {
  return atlOptionName?.trim().toLowerCase() === 'safe place'
}

export function computeFieldErrors(
  values: ConfirmFormValues,
  labels: AddressLabels,
): Record<string, string> {
  const {
    passengerName,
    passengerPhone,
    passengerEmail,
    address,
    hasDeliveryTime,
    atlOptionName,
    accessNotes,
  } = values

  const errors: Record<string, string> = {}

  if (!passengerName.trim()) errors.passengerName = 'Please enter your full name.'

  if (!passengerPhone.trim()) {
    errors.passengerPhone = 'Please enter your phone number.'
  } else if (passengerPhone.replace(/\D/g, '').length < MIN_PHONE_DIGITS) {
    errors.passengerPhone = 'Please enter a valid phone number.'
  }

  if (!passengerEmail.trim()) {
    errors.passengerEmail = 'Please enter your email address.'
  } else if (!passengerEmail.includes('@')) {
    errors.passengerEmail = 'Please enter a valid email address.'
  }

  if (!address.line4.trim()) errors.line4 = `Please enter your ${labels.line4.toLowerCase()}.`
  if (!address.line5.trim()) errors.line5 = `Please enter your ${labels.line5.toLowerCase()}.`
  if (!address.line6.trim()) errors.line6 = `Please enter your ${labels.line6.toLowerCase()}.`
  if (!(address.line7 ?? '').trim()) {
    errors.line7 = `Please enter your ${labels.line7.toLowerCase()}.`
  }
  if (!address.country.trim()) errors.country = 'Please enter your country.'

  if (!hasDeliveryTime) errors.slot = 'Please pick a delivery window.'

  if (atlNotesAreRequired(atlOptionName) && !accessNotes.trim()) {
    errors.accessNotes = 'Please describe the safe place to leave your baggage.'
  }

  return errors
}

export function orderedErrors(
  fieldErrors: Record<string, string>,
): Array<{ key: string; message: string }> {
  return ERROR_ORDER.filter((key) => fieldErrors[key]).map((key) => ({
    key,
    message: fieldErrors[key],
  }))
}
