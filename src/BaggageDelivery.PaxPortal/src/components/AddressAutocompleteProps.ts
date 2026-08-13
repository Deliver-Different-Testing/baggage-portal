import type { AddressDetail } from '../types/address'

export interface AddressAutocompleteProps {
  bookingId: string
  label?: string
  placeholder?: string
  onAddressSelect: (detail: AddressDetail) => void
}
