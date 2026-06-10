import { useEffect, useRef, useState } from 'react'
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
  const timerRef = useRef<ReturnType<typeof setTimeout>>(undefined)

  const inputIsLongEnough = inputValue.length >= minChars

  useEffect(() => {
    if (!inputIsLongEnough) return
    timerRef.current = setTimeout(() => {
      setDebouncedValue(inputValue)
    }, debounceMs)
    return () => clearTimeout(timerRef.current)
  }, [inputValue, inputIsLongEnough, debounceMs])

  const effectiveDebouncedValue = inputIsLongEnough ? debouncedValue : ''

  const { data: suggestions = [], isLoading } = useQuery({
    queryKey: ['pax', 'addressAutocomplete', bookingId, effectiveDebouncedValue],
    queryFn: () => addressAutocompleteApi.autocomplete(bookingId, effectiveDebouncedValue),
    enabled: !!bookingId && effectiveDebouncedValue.length >= minChars,
    staleTime: 30_000,
  })

  const getDetails = async (id: string): Promise<AddressDetail> =>
    addressAutocompleteApi.lookup(bookingId, id)

  return {
    inputValue,
    setInputValue,
    suggestions,
    isLoading: isLoading && effectiveDebouncedValue.length >= minChars,
    getDetails,
  } as const
}

export type { AddressDetail, AddressSearchResult }
