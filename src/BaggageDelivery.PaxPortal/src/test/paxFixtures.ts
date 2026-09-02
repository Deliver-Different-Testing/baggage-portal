import type { BookingSummary, TimeSlot } from '../api/client'

export const summary: BookingSummary = {
  bookingId: 1,
  jobId: 42,
  jobNumber: 'URG-179252',
  fileReference: 'AKLNZ12345',
  airlineLabel: 'Test Air',
  supportPhone: '0800 267 5494',
  passengerName: 'Test Passenger',
  passengerPhone: '+64211234567',
  passengerEmail: 'test@example.com',
  deliveryAddress: {
    line3: '123',
    line4: 'Test St',
    line5: 'Suburb',
    line6: 'Auckland',
    line7: '1010',
    country: 'NZ',
  },
  earliestSlotUtc: '2026-06-10T00:00:00Z',
  latestSlotUtc: '2026-06-11T00:00:00Z',
  atlOptions: [],
  trackingAvailable: true,
  trackingUrl: 'https://tracking.example.com/#/ENCRYPTED',
}

export const slot: TimeSlot = {
  id: 'slot-1',
  runUtc: '2026-06-10T02:00:00Z',
  dayLabel: 'Today, Wed 10 Jun',
  label: '2:00 PM – 5:00 PM',
  firstAvailable: true,
}

export const tomorrowSlot: TimeSlot = {
  id: 'slot-2',
  runUtc: '2026-06-10T21:00:00Z',
  dayLabel: 'Tomorrow, Thu 11 Jun',
  label: '9:00 AM – 12:00 PM',
  firstAvailable: false,
}
