import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { ThemeProvider } from '@mui/material/styles'
import { ConfirmedScreen } from './PaxMobile'
import { theme } from '../styles/theme'
import type { BookingSummary, TimeSlot } from '../api/client'

const summary: BookingSummary = {
  bookingId: 1,
  jobId: 42,
  reference: 'REF-42',
  airlineLabel: 'Test Air',
  passengerName: 'Test Passenger',
  passengerPhone: '+64211234567',
  passengerEmail: 'test@example.com',
  deliveryAddress: {
    line1: '123 Test St',
    suburb: 'Suburb',
    city: 'Auckland',
    postCode: '1010',
    country: 'NZ',
  },
  earliestSlotUtc: '2026-06-10T00:00:00Z',
  latestSlotUtc: '2026-06-11T00:00:00Z',
  atlOptions: [],
}

const slot: TimeSlot = {
  id: 'slot-1',
  startUtc: '2026-06-10T02:00:00Z',
  endUtc: '2026-06-10T04:00:00Z',
  label: '2pm – 4pm',
  firstAvailable: true,
}

function renderConfirmed(bookingId: string) {
  return render(
    <MemoryRouter>
      <ThemeProvider theme={theme}>
        <ConfirmedScreen summary={summary} slot={slot} bookingId={bookingId} />
      </ThemeProvider>
    </MemoryRouter>,
  )
}

describe('ConfirmedScreen', () => {
  it('renders a tracking link pointing to /t/:bookingId', () => {
    renderConfirmed('token-abc-123')

    const link = screen.getByRole('link', { name: /track your delivery/i })
    expect(link).toHaveAttribute('href', '/t/token-abc-123')
  })

  it('keeps the SMS hint copy alongside the in-page CTA', () => {
    renderConfirmed('token-abc-123')

    expect(
      screen.getByText(/we'll also text you when our driver is on the way/i),
    ).toBeInTheDocument()
  })
})
