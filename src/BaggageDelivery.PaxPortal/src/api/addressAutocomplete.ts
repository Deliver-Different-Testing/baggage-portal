import { apiClient } from './client'
import type { AddressDetail, AddressSearchResult } from '../types/address'

export const addressAutocompleteApi = {
  autocomplete: async (
    bookingId: string,
    text: string,
  ): Promise<AddressSearchResult[]> => {
    const response = await apiClient.get<AddressSearchResult[]>(
      `/pax/${bookingId}/address/autocomplete`,
      { params: { text } },
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
