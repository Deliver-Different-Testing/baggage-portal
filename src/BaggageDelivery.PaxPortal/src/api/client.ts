import axios, { type AxiosRequestConfig, type InternalAxiosRequestConfig } from 'axios'

const baseURL = import.meta.env.VITE_API_URL ?? '/api/v1'

export const apiClient = axios.create({
  baseURL,
  withCredentials: true,
  headers: { 'X-Requested-With': 'XMLHttpRequest' },
})

// Mirror inboundagent: the SPA reads the XSRF-TOKEN cookie set by the API and
// sends its value back as the X-XSRF-TOKEN header on every state-changing call.
// AddAntiforgery in the API validates the two match.
function readXsrfCookie(): string | null {
  if (typeof document === 'undefined') return null
  const prefix = 'XSRF-TOKEN='
  for (const part of document.cookie.split(';')) {
    const c = part.trim()
    if (c.startsWith(prefix)) return decodeURIComponent(c.substring(prefix.length))
  }
  return null
}

const MUTATING_METHODS = new Set(['post', 'put', 'patch', 'delete'])

apiClient.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const method = config.method?.toLowerCase()
  if (method && MUTATING_METHODS.has(method)) {
    const token = readXsrfCookie()
    if (token) {
      config.headers.set('X-XSRF-TOKEN', token)
    }
  }
  return config
})

apiClient.interceptors.response.use(
  (r) => r,
  (error) => {
    if (error.response?.status === 404) {
      return Promise.reject({ ...error, normalisedKind: 'not_found' })
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
  bookingId: number
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

export type { AxiosRequestConfig }
