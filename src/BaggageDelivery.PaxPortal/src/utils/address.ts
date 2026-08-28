import type { AddressDto } from '../api/client'

/**
 * Docket lines for the address, dropping the parts this country doesn't use.
 * Suburb and city are separate places and get a comma between them; the postcode
 * belongs to the city, so it follows on a space as it would on an envelope.
 *
 * Lives outside the components that render it so both the docket and the address
 * gate can import it without tripping the fast-refresh "components only" rule.
 */
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
