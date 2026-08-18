import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { setupServer } from 'msw/node'
import { http, HttpResponse } from 'msw'
import { Tracking } from './Tracking'
import { MantineTestProvider } from '../test/render'
import type { TrackingTimeline } from '../api/client'

// The page formats in the viewer's own locale and zone, so the expectations are
// built with the same Intl options rather than hard-coded strings.
const TIME_FMT: Intl.DateTimeFormatOptions = { hour: 'numeric', minute: '2-digit' }
const LONG_DATE_FMT: Intl.DateTimeFormatOptions = {
  weekday: 'long',
  day: 'numeric',
  month: 'long',
}

const ETA_START = '2026-06-10T02:00:00Z'
const ETA_END = '2026-06-10T05:00:00Z'

const HOUR_MS = 60 * 60 * 1000
const DAY_MS = 24 * HOUR_MS

// Pinned, not the wall clock: the page shows bare clock time only while an event
// falls on the same local day as now, and a day-and-time stamp otherwise. Built
// in local time so it lands at midday in whatever zone the runner uses — a suite
// run just after local midnight (CI runs in UTC) would push the "2h ago" event
// onto the previous day, and a run in early January would push the week-old one
// into the previous year.
const NOW = new Date(2026, 5, 10, 12, 0, 0)
const hoursAgo = (h: number) => new Date(NOW.getTime() - h * HOUR_MS).toISOString()

const timeline: TrackingTimeline = {
  jobId: 42,
  currentStatus: 'OutForDelivery',
  etaWindowStartUtc: ETA_START,
  etaWindowEndUtc: ETA_END,
  courierFirstName: 'Sam',
  vehicleLabel: 'Van 12',
  events: [
    {
      status: 'OutForDelivery',
      atUtc: hoursAgo(2),
      locationLabel: 'Auckland Depot',
      description: 'On board with the driver.',
    },
    { status: 'AtDepot', atUtc: hoursAgo(5) },
  ],
}

const server = setupServer(http.get('*/pax/:id/tracking', () => HttpResponse.json(timeline)))

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
afterEach(() => server.resetHandlers())
afterAll(() => server.close())

function renderTracking() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <MemoryRouter initialEntries={['/t/token-xyz']}>
      <MantineTestProvider>
        <QueryClientProvider client={queryClient}>
          <Routes>
            <Route path="/t/:id" element={<Tracking />} />
          </Routes>
        </QueryClientProvider>
      </MantineTestProvider>
    </MemoryRouter>,
  )
}

describe('Tracking', () => {
  beforeEach(() => {
    // shouldAdvanceTime keeps MSW and React Query's async resolution moving while
    // the clock the page reads stays anchored to NOW.
    vi.useFakeTimers({ shouldAdvanceTime: true })
    vi.setSystemTime(NOW)
  })
  afterEach(() => vi.useRealTimers())

  it('renders the ETA window as a local time range with its date', async () => {
    renderTracking()

    const start = new Date(ETA_START)
    const end = new Date(ETA_END)
    const expectedRange = `${start.toLocaleTimeString(undefined, TIME_FMT)} – ${end.toLocaleTimeString(undefined, TIME_FMT)}`

    expect(await screen.findByText(expectedRange)).toBeInTheDocument()
    expect(
      screen.getByText(start.toLocaleDateString(undefined, LONG_DATE_FMT)),
    ).toBeInTheDocument()
  })

  it('falls back to a pending note when no window has been set', async () => {
    server.use(
      http.get('*/pax/:id/tracking', () =>
        HttpResponse.json({ ...timeline, etaWindowStartUtc: null, etaWindowEndUtc: null }),
      ),
    )

    renderTracking()

    expect(await screen.findByText(/pending — we'll update this/i)).toBeInTheDocument()
  })

  it('writes known statuses out in English rather than splitting the enum', async () => {
    renderTracking()

    // Splitting PascalCase title-cases every word, so the passenger was reading
    // "Out For Delivery" and "At Depot".
    expect(await screen.findByText('Out for delivery')).toBeInTheDocument()
    expect(screen.getByText('At our depot')).toBeInTheDocument()
    expect(screen.queryByText('Out For Delivery')).not.toBeInTheDocument()
    expect(screen.queryByText('At Depot')).not.toBeInTheDocument()
  })

  it('falls back to the split for a status it has no label for', async () => {
    server.use(
      http.get('*/pax/:id/tracking', () =>
        HttpResponse.json({
          ...timeline,
          currentStatus: 'HeldAtCustoms',
          events: [{ status: 'HeldAtCustoms', atUtc: hoursAgo(1) }],
        }),
      ),
    )

    renderTracking()

    // Readable, and never a raw enum name — in the hero badge as well as the
    // timeline entry, which is why both matches are expected here.
    expect(await screen.findAllByText('Held At Customs')).toHaveLength(2)
  })

  it('lists the most recent event first, led by the clock time', async () => {
    renderTracking()

    await screen.findByText('Out for delivery')

    const items = screen.getAllByText(/^(Out for delivery|At our depot)$/)
    expect(items[0]).toHaveTextContent('Out for delivery')

    // "2h ago" is the gloss; the hour it happened is the fact, so both are shown
    // with the clock time leading.
    expect(screen.getByText(new Date(timeline.events[0].atUtc).toLocaleTimeString(undefined, TIME_FMT)))
      .toBeInTheDocument()
    expect(screen.getByText('2h ago')).toBeInTheDocument()
    expect(screen.getByText('5h ago')).toBeInTheDocument()
    expect(screen.getByText('Auckland Depot')).toBeInTheDocument()
    expect(screen.getByText('On board with the driver.')).toBeInTheDocument()
  })

  it('drops to a day-and-month date once an event is over a day old', async () => {
    const lastWeek = new Date(NOW.getTime() - 7 * DAY_MS)
    server.use(
      http.get('*/pax/:id/tracking', () =>
        HttpResponse.json({
          ...timeline,
          events: [{ status: 'AtDepot', atUtc: lastWeek.toISOString() }],
        }),
      ),
    )

    renderTracking()

    expect(
      await screen.findByText(
        lastWeek.toLocaleDateString(undefined, { day: 'numeric', month: 'short' }),
      ),
    ).toBeInTheDocument()
  })

  it('adds the year once an event falls outside the current one', async () => {
    const lastYear = new Date(NOW.getTime() - 400 * DAY_MS)
    server.use(
      http.get('*/pax/:id/tracking', () =>
        HttpResponse.json({
          ...timeline,
          events: [{ status: 'AtDepot', atUtc: lastYear.toISOString() }],
        }),
      ),
    )

    renderTracking()

    expect(
      await screen.findByText(
        lastYear.toLocaleDateString(undefined, {
          day: 'numeric',
          month: 'short',
          year: 'numeric',
        }),
      ),
    ).toBeInTheDocument()
  })

  it('names the driver and their vehicle', async () => {
    renderTracking()

    expect(await screen.findByText('Sam')).toBeInTheDocument()
    expect(screen.getByText('Van 12')).toBeInTheDocument()
  })
})
