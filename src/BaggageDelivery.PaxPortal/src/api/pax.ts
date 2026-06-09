import { apiClient } from './client'
import type {
  BookingSummary,
  ConfirmBookingRequest,
  TimeSlot,
  TrackingTimeline,
} from './client'

export async function createSession(token: string): Promise<BookingSummary> {
  const { data } = await apiClient.post<{ booking: BookingSummary; cookieExpiresAtUtc: string }>(
    '/pax/session',
    { token },
  )
  return data.booking
}

export async function getBooking(): Promise<BookingSummary> {
  const { data } = await apiClient.get<BookingSummary>('/pax/booking')
  return data
}

export async function getTimeslots(date?: string): Promise<TimeSlot[]> {
  const { data } = await apiClient.get<TimeSlot[]>('/pax/booking/timeslots', {
    params: date ? { date } : undefined,
  })
  return data
}

export async function confirmBooking(body: ConfirmBookingRequest): Promise<void> {
  await apiClient.post('/pax/booking/confirm', body)
}

export async function getTracking(): Promise<TrackingTimeline> {
  const { data } = await apiClient.get<TrackingTimeline>('/pax/tracking')
  return data
}
