import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { setupServer } from 'msw/node'
import { http, HttpResponse } from 'msw'
import { PaxMobile } from './PaxMobile'
import { MantineTestProvider } from '../test/render'
import { slot, summary, tomorrowSlot } from '../test/paxFixtures'
import type { AmendBookingRequest, BookingSummary } from '../api/client'

const NOW = '2026-06-10T01:00:00Z'

const confirmation = {
  confirmedAtUtc: '2026-06-10T00:30:00Z',
  deliveryTimeUtc: '2026-06-10T21:00:00Z',
  dayLabel: 'Tomorrow, Thu 11 Jun',
  windowLabel: '9:00 AM – 12:00 PM',
  atlOptionId: 7,
  accessNotes: 'Behind the blue bin',
  editableUntilUtc: '2026-06-10T20:30:00Z',
  canEdit: true,
}

const booking = (overrides: Partial<BookingSummary> = {}): BookingSummary => ({
  ...summary,
  atlOptions: [
    { id: 5, name: 'Front door' },
    { id: 7, name: 'Safe Place' },
  ],
  defaultAtlOptionId: 5,
  bookingLeadTimeMinutes: 30,
  confirmation,
  ...overrides,
})

describe('PaxMobile — changing a confirmed booking', () => {
  let lastAmendBody: AmendBookingRequest | null = null
  let amendStatus: { status: number; type?: string } = { status: 200 }
  let current: BookingSummary = booking()

  const server = setupServer(
    http.get('*/antiforgery/token', () => new HttpResponse(null, { status: 204 })),
    http.get('*/pax/:id/booking', () => HttpResponse.json(current)),
    http.get('*/pax/:id/booking/timeslots', () => HttpResponse.json([slot, tomorrowSlot])),
    http.post('*/pax/:id/booking/amend', async ({ request }) => {
      lastAmendBody = (await request.json()) as AmendBookingRequest
      if (amendStatus.status !== 200) {
        return HttpResponse.json(
          { status: amendStatus.status, title: 'Nope', type: amendStatus.type },
          { status: amendStatus.status },
        )
      }
      return HttpResponse.json({ status: 'Updated', releasedAtUtc: NOW, trackingUrl: null })
    }),
  )

  beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    vi.setSystemTime(new Date(NOW))
    lastAmendBody = null
    amendStatus = { status: 200 }
    current = booking()
  })
  afterEach(() => {
    vi.useRealTimers()
    server.resetHandlers()
  })
  afterAll(() => server.close())

  function renderPage() {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
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

  async function openChangeForm() {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    renderPage()
    await screen.findByRole('heading', { level: 1, name: /you're all set/i })
    await user.click(screen.getByRole('button', { name: /change delivery details/i }))
    await screen.findByRole('heading', { level: 1, name: /change your delivery/i })
    return user
  }

  it('offers the change while the booking is still inside its change window', async () => {
    renderPage()

    await screen.findByRole('heading', { level: 1, name: /you're all set/i })

    expect(screen.getByRole('button', { name: /change delivery details/i })).toBeInTheDocument()
  })

  it('falls back to the phone number once the change window has closed', async () => {
    current = booking({ confirmation: { ...confirmation, canEdit: false } })
    renderPage()

    await screen.findByRole('heading', { level: 1, name: /you're all set/i })

    expect(
      screen.queryByRole('button', { name: /change delivery details/i }),
    ).not.toBeInTheDocument()
    expect(screen.getByText(/need to change something/i)).toBeInTheDocument()
  })

  it('opens the form already carrying what the passenger booked', async () => {
    await openChangeForm()

    expect(screen.getByDisplayValue('Test Passenger')).toBeInTheDocument()
    expect(screen.getByDisplayValue('Behind the blue bin')).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: /safe place/i })).toBeChecked()
  })

  it('holds the delivery address steady - changing it is still a phone call', async () => {
    await openChangeForm()

    const deliverTo = within(screen.getByRole('region', { name: /deliver to/i }))
    expect(deliverTo.getByText(/123 Test St/i)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^edit$/i })).not.toBeInTheDocument()
    expect(screen.queryByLabelText(/search address/i)).not.toBeInTheDocument()
  })

  it('saves the new window against the amend endpoint, leaving the address out of it', async () => {
    const user = await openChangeForm()

    await user.click(screen.getByRole('radio', { name: /2:00 PM . 5:00 PM/i }))
    await user.click(screen.getByRole('button', { name: /review changes/i }))
    await user.click(await screen.findByRole('button', { name: /save changes/i }))

    await waitFor(() => expect(lastAmendBody).not.toBeNull())
    expect(lastAmendBody).toMatchObject({
      deliveryTimeUtc: slot.runUtc,
      atlOptionId: 7,
      accessNotes: 'Behind the blue bin',
      passengerName: 'Test Passenger',
    })
    expect(lastAmendBody).not.toHaveProperty('address')
    expect(
      await screen.findByRole('heading', { level: 1, name: /you're all set/i }),
    ).toBeInTheDocument()
  })

  it('tells the passenger plainly when the window shut between opening and saving', async () => {
    amendStatus = { status: 409, type: 'urn:baggage:change-window-closed' }
    const user = await openChangeForm()

    await user.click(screen.getByRole('button', { name: /review changes/i }))
    await user.click(await screen.findByRole('button', { name: /save changes/i }))

    expect(await screen.findByText(/can no longer be changed online/i)).toBeInTheDocument()
  })

  it('lets the passenger back out without touching the booking', async () => {
    const user = await openChangeForm()

    await user.click(screen.getByRole('button', { name: /keep my current booking/i }))

    expect(await screen.findByRole('heading', { level: 1, name: /you're all set/i })).toBeInTheDocument()
    expect(lastAmendBody).toBeNull()
  })

  it('never offers a run that starts inside the booking lead time', async () => {
    vi.setSystemTime(new Date('2026-06-10T01:45:00Z'))
    const user = await openChangeForm()

    expect(screen.queryByRole('radio', { name: /2:00 PM . 5:00 PM/i })).not.toBeInTheDocument()
    expect(screen.getByRole('radio', { name: /9:00 AM . 12:00 PM/i })).toBeInTheDocument()
    expect(user).toBeDefined()
  })
})
