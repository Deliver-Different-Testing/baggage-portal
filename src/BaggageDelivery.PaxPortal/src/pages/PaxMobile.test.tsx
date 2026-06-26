import { afterAll, afterEach, beforeAll, describe, expect, it } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { ThemeProvider } from '@mui/material/styles'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { setupServer } from 'msw/node'
import { http, HttpResponse } from 'msw'
import { ConfirmedScreen, PaxMobile } from './PaxMobile'
import { theme } from '../styles/theme'
import type { BookingSummary, ConfirmBookingRequest, TimeSlot } from '../api/client'

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

  it('shows the Powered by Deliver DFRNT footer', () => {
    renderConfirmed('token-abc-123')

    expect(screen.getByText(/powered by/i)).toBeInTheDocument()
    expect(screen.getByRole('img', { name: /deliver dfrnt/i })).toBeInTheDocument()
  })
})

describe('PaxMobile — Authority to Leave submit', () => {
  const atlOptions = [
    { id: 5, name: 'Front door' },
    { id: 6, name: 'Back door' },
  ]

  let lastConfirmBody: ConfirmBookingRequest | null = null

  const server = setupServer(
    http.get('*/pax/:id/booking', () =>
      HttpResponse.json({ ...summary, atlOptions }),
    ),
    http.get('*/pax/:id/booking/timeslots', () => HttpResponse.json([slot])),
    http.post('*/pax/:id/booking/confirm', async ({ request }) => {
      lastConfirmBody = (await request.json()) as ConfirmBookingRequest
      return HttpResponse.json({ status: 'Released', releasedAtUtc: '2026-06-10T00:00:00Z' })
    }),
  )

  beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
  afterEach(() => {
    server.resetHandlers()
    lastConfirmBody = null
  })
  afterAll(() => server.close())

  function renderForm() {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    return render(
      <MemoryRouter initialEntries={['/c/token-xyz']}>
        <ThemeProvider theme={theme}>
          <QueryClientProvider client={queryClient}>
            <Routes>
              <Route path="/c/:id" element={<PaxMobile />} />
            </Routes>
          </QueryClientProvider>
        </ThemeProvider>
      </MemoryRouter>,
    )
  }

  it('posts the selected ATL option id as a number', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your/i)
    await waitFor(() => expect(screen.getByRole('switch')).toBeEnabled())

    // Turn ATL on (auto-selects the first option), then pick the second.
    await user.click(screen.getByRole('switch'))
    await user.click(await screen.findByRole('radio', { name: 'Back door' }))

    await user.click(screen.getByRole('button', { name: /confirm delivery/i }))

    await waitFor(() => expect(lastConfirmBody).not.toBeNull())
    expect(lastConfirmBody!.atlOptionId).toBe(6)
    expect(typeof lastConfirmBody!.atlOptionId).toBe('number')
  })

  it('posts null when Authority to Leave stays off', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your/i)
    await waitFor(() => expect(screen.getByRole('button', { name: /confirm delivery/i })).toBeEnabled())

    await user.click(screen.getByRole('button', { name: /confirm delivery/i }))

    await waitFor(() => expect(lastConfirmBody).not.toBeNull())
    expect(lastConfirmBody!.atlOptionId).toBeNull()
  })
})
