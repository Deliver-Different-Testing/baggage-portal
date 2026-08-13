import { apiClient } from './client'
import type {
  BookingSummary,
  ConfirmBookingRequest,
  DevLinks,
  TimeSlot,
  TrackingTimeline,
} from './client'

export async function getBooking(id: string): Promise<BookingSummary> {
  const { data } = await apiClient.get<BookingSummary>(`/pax/${id}/booking`)
  return data
}

export async function getTimeslots(id: string, date?: string): Promise<TimeSlot[]> {
  const { data } = await apiClient.get<TimeSlot[]>(`/pax/${id}/booking/timeslots`, {
    params: date ? { date } : undefined,
  })
  return data
}

export async function confirmBooking(id: string, body: ConfirmBookingRequest): Promise<void> {
  await apiClient.post(`/pax/${id}/booking/confirm`, body)
}

export async function getTracking(id: string): Promise<TrackingTimeline> {
  const { data } = await apiClient.get<TrackingTimeline>(`/pax/${id}/tracking`)
  return data
}

export async function getDevLinks(): Promise<DevLinks> {
  const { data } = await apiClient.get<DevLinks>('/dev/links')
  return data
}
