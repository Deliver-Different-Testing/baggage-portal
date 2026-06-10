import { useState } from 'react'
import Autocomplete from '@mui/material/Autocomplete'
import CircularProgress from '@mui/material/CircularProgress'
import InputAdornment from '@mui/material/InputAdornment'
import TextField from '@mui/material/TextField'
import LocationOnIcon from '@mui/icons-material/LocationOn'
import { useAddressSearch } from '../hooks/useAddressSearch'
import type { AddressDetail, AddressSearchResult } from '../types/address'

interface AddressAutocompleteProps {
  bookingId: string
  label?: string
  placeholder?: string
  countryCode?: string
  onAddressSelect: (detail: AddressDetail) => void
}

export function AddressAutocomplete({
  bookingId,
  label = 'Search address',
  placeholder = 'Start typing an address...',
  countryCode,
  onAddressSelect,
}: AddressAutocompleteProps) {
  const { setInputValue, suggestions, isLoading, getDetails } = useAddressSearch({
    bookingId,
    countryCode,
  })
  const [isLookingUp, setIsLookingUp] = useState(false)
  const [displayValue, setDisplayValue] = useState('')

  const handleSelect = async (
    _: unknown,
    option: AddressSearchResult | string | null,
  ) => {
    if (!option || typeof option === 'string') {
      setDisplayValue(typeof option === 'string' ? option : '')
      return
    }

    setIsLookingUp(true)
    try {
      const detail = await getDetails(option.id)
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
      freeSolo
      slotProps={{ popper: { placement: 'bottom-start' } }}
      options={suggestions}
      getOptionLabel={(option) =>
        typeof option === 'string' ? option : option.title
      }
      filterOptions={(x) => x}
      inputValue={displayValue}
      onInputChange={(_, newValue, reason) => {
        setDisplayValue(newValue)
        if (reason === 'input') {
          setInputValue(newValue)
        }
      }}
      onChange={handleSelect}
      loading={isLoading || isLookingUp}
      renderInput={(params) => (
        <TextField
          {...params}
          fullWidth
          label={label}
          placeholder={placeholder}
          slotProps={{
            ...params.slotProps,
            input: {
              ...params.slotProps?.input,
              startAdornment: (
                <>
                  <InputAdornment position="start">
                    <LocationOnIcon sx={{ color: 'text.secondary' }} />
                  </InputAdornment>
                  {params.slotProps?.input?.startAdornment}
                </>
              ),
              endAdornment: (
                <>
                  {(isLoading || isLookingUp) && (
                    <CircularProgress color="inherit" size={20} />
                  )}
                  {params.slotProps?.input?.endAdornment}
                </>
              ),
            },
          }}
        />
      )}
      renderOption={(props, option) => (
        <li {...props} key={option.id}>
          {option.title}
        </li>
      )}
    />
  )
}
