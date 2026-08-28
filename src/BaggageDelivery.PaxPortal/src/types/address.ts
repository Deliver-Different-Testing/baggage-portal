export interface AddressSearchResult {
  id: string
  title: string
  street: string
  suburb: string
  city: string
  state: string
  postalCode: string
  countryCode: string
}

export interface AddressDetail {
  streetNumber: string
  street: string
  suburb: string
  city: string
  state: string
  stateCode: string
  postalCode: string
  countryCode: string
  latitude?: number | null
  longitude?: number | null
}
