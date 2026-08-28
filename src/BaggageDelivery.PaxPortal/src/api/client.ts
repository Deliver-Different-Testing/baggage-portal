import axios, { type AxiosRequestConfig, type InternalAxiosRequestConfig } from 'axios'

const baseURL = import.meta.env.VITE_API_URL ?? '/api/v1'

export const apiClient = axios.create({
  baseURL,
  withCredentials: true,
  headers: { 'X-Requested-With': 'XMLHttpRequest' },
})

export const ANTIFORGERY_TOKEN_PATH = '/antiforgery/token'

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

let tokenRequest: Promise<unknown> | null = null

async function ensureXsrfToken(forceRefresh = false): Promise<string | null> {
  const existing = readXsrfCookie()
  if (existing && !forceRefresh) return existing

  tokenRequest ??= apiClient.get(ANTIFORGERY_TOKEN_PATH).finally(() => {
    tokenRequest = null
  })

  try {
    await tokenRequest
  } catch {
    return null
  }
  return readXsrfCookie()
}

apiClient.interceptors.request.use(async (config: InternalAxiosRequestConfig) => {
  const method = config.method?.toLowerCase()
  if (method && MUTATING_METHODS.has(method)) {
    const token = await ensureXsrfToken()
    if (token) {
      config.headers.set('X-XSRF-TOKEN', token)
    }
  }
  return config
})

type RetriableConfig = InternalAxiosRequestConfig & { xsrfRetried?: boolean }

apiClient.interceptors.response.use(
  (r) => r,
  async (error) => {
    if (error.response?.status === 404) {
      return Promise.reject({ ...error, normalisedKind: 'not_found' })
    }

    const config = error.config as RetriableConfig | undefined
    if (error.response?.status === 400 && config && !config.xsrfRetried && !error.response.data) {
      config.xsrfRetried = true
      const token = await ensureXsrfToken(true)
      if (token) {
        config.headers.set('X-XSRF-TOKEN', token)
        return apiClient.request(config)
      }
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
  fileReference: string
  airlineLabel: string
  airlineCode?: string | null
  supportPhone: string
  passengerName: string
  passengerPhone?: string | null
  passengerEmail?: string | null
  deliveryAddress: AddressDto
  earliestSlotUtc: string
  latestSlotUtc: string
  atlOptions: AtlOption[]
  defaultAtlOptionId?: number | null
}

export type AtlOption = {
  id: number
  name: string
}

export type TimeSlot = {
  id: string
  runUtc: string
  dayLabel: string
  label: string
  firstAvailable: boolean
}

export type ConfirmBookingRequest = {
  address: AddressDto
  deliveryTimeUtc: string
  atlOptionId: number | null
  accessNotes?: string | null
  passengerName: string
  passengerPhone?: string | null
  passengerEmail?: string | null
}

export type DevLinks = {
  jobId: number
  token: string
  confirmUrl: string
  trackUrl: string
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
