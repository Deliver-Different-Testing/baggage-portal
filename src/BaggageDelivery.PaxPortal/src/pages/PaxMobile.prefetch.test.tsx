import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { setupServer } from 'msw/node'
import { http, HttpResponse } from 'msw'
import { PaxMobile } from './PaxMobile'
import { MantineTestProvider } from '../test/render'
import { queryClient } from '../api/queryClient'
import { prefetchRouteData } from '../api/prefetch'
import type { BookingSummary, TimeSlot } from '../api/client'

const summary: BookingSummary = {
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
}

const slot: TimeSlot = {
  id: 'slot-1',
  runUtc: '2026-06-10T02:00:00Z',
  dayLabel: 'Today, Wed 10 Jun',
  label: '2:00 PM – 5:00 PM',
  firstAvailable: true,
}

let bookingRequests = 0
let timeslotRequests = 0

const server = setupServer(
  http.get('*/pax/:id/booking', () => {
    bookingRequests += 1
    return HttpResponse.json(summary)
  }),
  http.get('*/pax/:id/booking/timeslots', () => {
    timeslotRequests += 1
    return HttpResponse.json([slot])
  }),
  http.get('*/antiforgery/token', () => new HttpResponse(null, { status: 204 })),
)

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
beforeEach(() => {
  queryClient.clear()
  bookingRequests = 0
  timeslotRequests = 0
})
afterEach(() => server.resetHandlers())
afterAll(() => server.close())

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/c/token-xyz']}>
      <MantineTestProvider>
        <QueryClientProvider client={queryClient}>
          <Routes>
            <Route path="/c/:id" element={<PaxMobile />} />
          </Routes>
        </QueryClientProvider>
      </MantineTestProvider>
    </MemoryRouter>,
  )
}

describe('PaxMobile — entry prefetch handover', () => {
  it('reuses the prefetched booking instead of refetching it on mount', async () => {
    await prefetchRouteData('/c/token-xyz')
    expect(bookingRequests).toBe(1)

    renderPage()

    expect(await screen.findByLabelText(/full name/i)).toBeInTheDocument()
    expect(bookingRequests).toBe(1)
  })

  it('reuses the prefetched delivery windows instead of refetching them on mount', async () => {
    await prefetchRouteData('/c/token-xyz')
    expect(timeslotRequests).toBe(1)

    renderPage()

    expect(await screen.findByLabelText(/full name/i)).toBeInTheDocument()
    expect(timeslotRequests).toBe(1)
  })
})
