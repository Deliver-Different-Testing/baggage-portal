import type { AddressDto } from '../api/client'

export function addressLines(a: AddressDto): string[] {
  const locality = [
    [a.suburb, a.city].map((p) => (p ?? '').trim()).filter(Boolean).join(', '),
    a.postCode,
  ]
    .map((part) => (part ?? '').trim())
    .filter(Boolean)
    .join(' ')
  return [a.line1, locality, a.country].map((line) => (line ?? '').trim()).filter(Boolean)
}
