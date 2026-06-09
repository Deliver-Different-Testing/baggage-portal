import axios from 'axios'

const baseURL = import.meta.env.VITE_API_URL ?? '/api/v1'

export const apiClient = axios.create({
  baseURL,
  withCredentials: true,
  headers: { 'X-Requested-With': 'XMLHttpRequest' },
})

apiClient.interceptors.response.use(
  (r) => r,
  (error) => {
    if (error.response?.status === 401 || error.response?.status === 410) {
      // Caller decides how to surface session expiry; we just normalise the error.
      return Promise.reject({
        ...error,
        normalisedKind: error.response.status === 410 ? 'expired' : 'unauthenticated',
      })
    }
    return Promise.reject(error)
  },
)

export type AddressDto = {
  line1: string
  line2?: string | null
  suburb?: string | null
  city: string
  postCode?: string | null
  country: string
  latitude?: number | null
  longitude?: number | null
}

export type BookingSummary = {
  jobId: number
  reference: string
  airlineLabel: string
  passengerName: string
  passengerPhone?: string | null
  passengerEmail?: string | null
  deliveryAddress: AddressDto
  earliestSlotUtc: string
  latestSlotUtc: string
}

export type TimeSlot = {
  id: string
  startUtc: string
  endUtc: string
  label: string
  firstAvailable: boolean
}

export type ConfirmBookingRequest = {
  address: AddressDto
  timeSlotStartUtc: string
  timeSlotEndUtc: string
  atlOption: string
  accessNotes?: string | null
  phoneOverride?: string | null
}

export type TrackingEvent = {
  status: string
  atUtc: string
  locationLabel?: string | null
  description?: string | null
}

export type TrackingTimeline = {
  jobId: number
  currentStatus: string
  events: TrackingEvent[]
  etaWindowStartUtc?: string | null
  etaWindowEndUtc?: string | null
  courierFirstName?: string | null
  vehicleLabel?: string | null
}
