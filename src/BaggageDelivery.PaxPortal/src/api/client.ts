import axios, { type AxiosRequestConfig, type InternalAxiosRequestConfig } from 'axios'

const baseURL = import.meta.env.VITE_API_URL ?? '/api/v1'

export const apiClient = axios.create({
  baseURL,
  withCredentials: true,
  headers: { 'X-Requested-With': 'XMLHttpRequest' },
})

// ASP.NET Core antiforgery is a pair: an HttpOnly cookie token the browser holds
// and a request token we must echo in a header. GET /antiforgery/token issues both,
// putting the request token in the readable XSRF-TOKEN cookie.
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

// Fetched lazily rather than on page load so the token can't go stale between
// mount and submit, and so an evicted cookie recovers on its own. Concurrent
// mutations share one in-flight request.
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
    // Let the mutation proceed and surface the server's own error.
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

    // A rejected antiforgery token yields a bare 400 with no body; model-validation
    // failures always carry a ProblemDetails payload. Re-mint once and retry — this
    // is the recovery path when the server's key ring no longer matches our cookie.
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
  reference: string
  airlineLabel: string
  airlineCode?: string | null
  /** "Need help? Call …" — the client's own phone, or the tenant's support line. Empty to hide. */
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
  /** Date line, e.g. `Tomorrow, Fri 15 Aug`. Rendered server-side in the tenant's timezone. */
  dayLabel: string
  /** Delivery window, e.g. `9:00 AM – 12:00 PM`. */
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

// GET /api/v1/dev/links — mapped by the API in Development only. Outside
// Development the route does not exist, and the 404 is what sends / to /expired.
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
