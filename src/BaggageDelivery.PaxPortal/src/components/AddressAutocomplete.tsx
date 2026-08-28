import { memo, useMemo, useState } from 'react'
import { Autocomplete, Loader } from '@mantine/core'
import { MapPinIcon } from './Icon'
import { useAddressSearch } from '../hooks/useAddressSearch'
import type { AddressAutocompleteProps } from './AddressAutocompleteProps'

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

  const options = useMemo(() => suggestions.map((s) => s.title), [suggestions])

  const handleOptionSubmit = async (title: string) => {
    const match = suggestions.find((s) => s.title === title)
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
