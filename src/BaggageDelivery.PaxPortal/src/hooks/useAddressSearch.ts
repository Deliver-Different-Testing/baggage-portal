import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { addressAutocompleteApi } from '../api/addressAutocomplete'
import type { AddressDetail, AddressSearchResult } from '../types/address'

interface UseAddressSearchOptions {
  bookingId: string
  minChars?: number
  debounceMs?: number
}

export function useAddressSearch(options: UseAddressSearchOptions) {
  const { bookingId, minChars = 3, debounceMs = 300 } = options
  const [inputValue, setInputValue] = useState('')
  const [debouncedValue, setDebouncedValue] = useState('')

  const isLongEnough = inputValue.length >= minChars

  useEffect(() => {
    if (!isLongEnough) return
    const handle = setTimeout(() => setDebouncedValue(inputValue), debounceMs)
    return () => clearTimeout(handle)
  }, [inputValue, isLongEnough, debounceMs])

  const query = isLongEnough ? debouncedValue : ''

  const { data: suggestions = [], isLoading } = useQuery({
    queryKey: ['pax', 'addressAutocomplete', bookingId, query],
    queryFn: () => addressAutocompleteApi.autocomplete(bookingId, query),
    enabled: !!bookingId && query.length >= minChars,
    staleTime: 30_000,
  })

  const getDetails = async (id: string): Promise<AddressDetail> =>
    addressAutocompleteApi.lookup(bookingId, id)

  return {
    inputValue,
    setInputValue,
    suggestions,
    isLoading: isLoading && query.length >= minChars,
    getDetails,
  } as const
}

export type { AddressDetail, AddressSearchResult }
