import { memo, useMemo, useState } from 'react'
import { Autocomplete, Loader } from '@mantine/core'
import { MapPinIcon } from './Icon'
import { useAddressSearch } from '../hooks/useAddressSearch'
import type { AddressSearchResult } from '../types/address'
import type { AddressAutocompleteProps } from './AddressAutocompleteProps'

function optionLabel(suggestion: AddressSearchResult): string {
  const extras = [suggestion.suburb, suggestion.city].filter(
    (part) => part && !suggestion.title.includes(part),
  )
  return [suggestion.title, ...extras].join(', ')
}

export const AddressAutocomplete = memo(function AddressAutocomplete({
  bookingId,
  label = 'Search address',
  placeholder = 'Start typing an address…',
  onAddressSelect,
}: AddressAutocompleteProps) {
  const { setInputValue, suggestions, isLoading, getDetails } = useAddressSearch({
    bookingId,
  })
  const [isLookingUp, setIsLookingUp] = useState(false)
  const [displayValue, setDisplayValue] = useState('')

  const loading = isLoading || isLookingUp

  const options = useMemo(() => {
    const byId = new Map(suggestions.map((s) => [s.id, s] as const))
    return [...byId.values()].map((s) => ({ value: s.id, label: optionLabel(s) }))
  }, [suggestions])

  const handleOptionSubmit = async (addressId: string) => {
    const match = suggestions.find((s) => s.id === addressId)
    if (!match) return

    setIsLookingUp(true)
    try {
      const detail = await getDetails(match.id)
      const fullDisplay = [
        [detail.streetNumber, detail.street].filter(Boolean).join(' '),
        detail.suburb,
        detail.city,
        detail.stateCode || detail.state,
        detail.postalCode,
      ]
        .filter(Boolean)
        .join(', ')
      setDisplayValue(fullDisplay)
      onAddressSelect(detail)
    } finally {
      setIsLookingUp(false)
    }
  }

  return (
    <Autocomplete
      label={label}
      placeholder={placeholder}
      value={displayValue}
      data={options}
      filter={({ options }) => options}
      onChange={(value) => {
        setDisplayValue(value)
        setInputValue(value)
      }}
      onOptionSubmit={handleOptionSubmit}
      leftSection={<MapPinIcon size={18} />}
      rightSection={loading ? <Loader size={18} /> : undefined}
      comboboxProps={{ position: 'bottom-start' }}
    />
  )
})
