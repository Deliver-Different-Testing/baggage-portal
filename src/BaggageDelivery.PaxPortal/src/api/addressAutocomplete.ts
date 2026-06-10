import { apiClient } from './client'
import type { AddressDetail, AddressSearchResult } from '../types/address'

export const addressAutocompleteApi = {
  autocomplete: async (
    bookingId: string,
    text: string,
    countryCode?: string,
  ): Promise<AddressSearchResult[]> => {
    const params: Record<string, string> = { text }
    if (countryCode) params.countryCode = countryCode
    const response = await apiClient.get<AddressSearchResult[]>(
      `/pax/${bookingId}/address/autocomplete`,
      { params },
    )
    return response.data
  },

  lookup: async (bookingId: string, addressId: string): Promise<AddressDetail> => {
    const response = await apiClient.get<AddressDetail>(
      `/pax/${bookingId}/address/lookup/${encodeURIComponent(addressId)}`,
    )
    return response.data
  },
}
