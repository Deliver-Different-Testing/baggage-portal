import { memo, useMemo, useState } from 'react'
import { Autocomplete, Loader } from '@mantine/core'
import { MapPinIcon } from './Icon'
import { useAddressSearch } from '../hooks/useAddressSearch'
import type { AddressAutocompleteProps } from './AddressAutocompleteProps'

// Memoized: this sits next to the form's other text inputs, so without memo every
// keystroke elsewhere in ConfirmForm would re-render it. Relies on a stable
// `onAddressSelect` from the parent (see handleAddressSelect in PaxMobile).
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

  // A fresh array on every keystroke re-runs Mantine's option filtering and
  // re-renders the whole dropdown even when the suggestions haven't changed.
  const options = useMemo(() => suggestions.map((s) => s.title), [suggestions])

  // Mantine's Autocomplete works on string options; keep our own suggestion list to
  // recover the PAF id for the selected title and fetch full address details.
  const handleOptionSubmit = async (title: string) => {
    const match = suggestions.find((s) => s.title === title)
    if (!match) return

    setIsLookingUp(true)
    try {
      const detail = await getDetails(match.id)
      const fullDisplay = [
        detail.street,
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
      // Present suggestions as-is (server already ranked them); don't re-filter locally.
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
