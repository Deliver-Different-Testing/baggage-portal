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

export async function warmAntiforgeryToken(): Promise<void> {
  await ensureXsrfToken()
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
  line1?: string | null
  line2?: string | null
  line3?: string | null
  line4: string
  line5: string
  line6: string
  line7?: string | null
  country: string
  latitude?: number | null
  longitude?: number | null
}

export type ServiceAddressDto = {
  line5?: string | null
  line7?: string | null
  latitude?: number | null
  longitude?: number | null
}

export type BookingSummary = {
  bookingId: number
  jobId: number
  jobNumber: string
  fileReference: string
  airlineLabel: string
  airlineCode?: string | null
  supportPhone: string
  passengerName: string
  passengerPhone?: string | null
  passengerEmail?: string | null
  deliveryAddress: AddressDto
  deliveryNotes?: string[]
  earliestSlotUtc: string
  latestSlotUtc: string
  atlOptions: AtlOption[]
  defaultAtlOptionId?: number | null
  trackingAvailable?: boolean
  trackingUrl?: string | null
  bookingLeadTimeMinutes?: number | null
  confirmation?: BookingConfirmation | null
}

export type BookingConfirmation = {
  confirmedAtUtc: string
  deliveryTimeUtc?: string | null
  dayLabel: string
  windowLabel: string
  atlOptionId?: number | null
  accessNotes?: string | null
  editableUntilUtc?: string | null
  canEdit?: boolean
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

export type AvailableService = {
  jobTypeId: number
  scheduleId: number | null
  name: string
  description: string | null
  bookDateUtc: string | null
  durationMinutes: number | null
  isScheduled: boolean
}

export type AvailableServicesResponse = {
  services: AvailableService[]
  noServiceAvailable: boolean
}

export type ConfirmBookingRequest = {
  address: AddressDto
  deliveryTimeUtc: string
  serviceJobTypeId: number | null
  atlOptionId: number | null
  accessNotes?: string | null
  passengerName: string
  passengerPhone?: string | null
  passengerEmail?: string | null
}

export type AmendBookingRequest = {
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
}

export type { AxiosRequestConfig }
