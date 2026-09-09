import { apiClient } from './client'
import type {
  AddressDto,
  AmendBookingRequest,
  AvailableServicesResponse,
  BookingSummary,
  ConfirmBookingRequest,
  DevLinks,
  TimeSlot,
} from './client'

export async function getBooking(id: string): Promise<BookingSummary> {
  const { data } = await apiClient.get<BookingSummary>(`/pax/${id}/booking`)
  return data
}

export async function getServices(
  id: string,
  address: AddressDto,
): Promise<AvailableServicesResponse> {
  const { data } = await apiClient.post<AvailableServicesResponse>(`/pax/${id}/booking/services`, {
    address,
  })
  return data
}

export async function requestAddressHelp(
  id: string,
  body: {
    address: AddressDto
    passengerName: string
    passengerPhone?: string | null
    passengerEmail?: string | null
  },
): Promise<void> {
  await apiClient.post(`/pax/${id}/booking/address-help`, body)
}

export async function getTimeslots(
  id: string,
  date?: string,
  service?: { jobTypeId: number; scheduleId: number | null } | null,
): Promise<TimeSlot[]> {
  const params: Record<string, string | number> = {}
  if (date) params.date = date
  if (service) {
    params.jobTypeId = service.jobTypeId
    if (service.scheduleId !== null) params.scheduleId = service.scheduleId
  }

  const { data } = await apiClient.get<TimeSlot[]>(`/pax/${id}/booking/timeslots`, {
    params: Object.keys(params).length > 0 ? params : undefined,
  })
  return data
}

export async function confirmBooking(id: string, body: ConfirmBookingRequest): Promise<void> {
  await apiClient.post(`/pax/${id}/booking/confirm`, body)
}

export async function amendBooking(id: string, body: AmendBookingRequest): Promise<void> {
  await apiClient.post(`/pax/${id}/booking/amend`, body)
}

export async function getDevLinks(): Promise<DevLinks> {
  const { data } = await apiClient.get<DevLinks>('/dev/links')
  return data
}
