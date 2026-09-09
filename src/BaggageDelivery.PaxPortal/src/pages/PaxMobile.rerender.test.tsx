import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { setupServer } from 'msw/node'
import { http, HttpResponse } from 'msw'
import { PaxMobile } from './PaxMobile'
import { MantineTestProvider } from '../test/render'
import type { BookingSummary, TimeSlot } from '../api/client'

const footer = vi.hoisted(() => ({ renders: 0 }))
vi.mock('../components/PoweredByFooter', async () => {
  const { memo } = await import('react')
  return {
    PoweredByFooter: memo(() => {
      footer.renders += 1
      return null
    }),
  }
})

const hero = vi.hoisted(() => ({ renders: 0 }))
vi.mock('../components/Icon', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../components/Icon')>()),
  LuggageIcon: () => {
    hero.renders += 1
    return null
  },
}))

const addressSection = vi.hoisted(() => ({ renders: 0 }))
vi.mock('../components/confirm/AddressSection', async () => {
  const { memo } = await import('react')
  return {
    AddressSection: memo(() => {
      addressSection.renders += 1
      return null
    }),
  }
})

const atlSection = vi.hoisted(() => ({ renders: 0 }))
vi.mock('../components/confirm/AtlSection', async () => {
  const { memo } = await import('react')
  return {
    AtlSection: memo(() => {
      atlSection.renders += 1
      return null
    }),
  }
})

const windowSection = vi.hoisted(() => ({ renders: 0 }))
vi.mock('../components/confirm/WindowSection', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../components/confirm/WindowSection')>()
  const { createElement, memo } = await import('react')
  return {
    WindowSection: memo((props: Parameters<typeof actual.WindowSection>[0]) => {
      windowSection.renders += 1
      return createElement(actual.WindowSection, props)
    }),
  }
})

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

const server = setupServer(
  http.get('*/pax/:id/booking', () => HttpResponse.json(summary)),
  http.get('*/pax/:id/booking/timeslots', () => HttpResponse.json([slot])),
)

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
afterEach(() => {
  server.resetHandlers()
  footer.renders = 0
  hero.renders = 0
  addressSection.renders = 0
  atlSection.renders = 0
  windowSection.renders = 0
})
afterAll(() => server.close())

function renderForm() {
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

const FORTY_MINUTES_OUT = new Date('2026-06-10T01:20:00Z')

describe('PaxMobile — run start countdown', () => {
  it('ticks the time left before the run down every second', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    try {
      vi.setSystemTime(FORTY_MINUTES_OUT)
      renderForm()

      expect(await screen.findByText('40:00')).toBeInTheDocument()

      await act(async () => {
        vi.advanceTimersByTime(5_000)
      })

      expect(screen.getByText('39:55')).toBeInTheDocument()
    } finally {
      vi.useRealTimers()
    }
  })

  it('keeps the tick off the rest of the form', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    try {
      vi.setSystemTime(FORTY_MINUTES_OUT)
      renderForm()

      await screen.findByText('40:00')
      const before = footer.renders

      await act(async () => {
        vi.advanceTimersByTime(5_000)
      })

      expect(screen.getByText('39:55')).toBeInTheDocument()
      expect(footer.renders).toBe(before)
    } finally {
      vi.useRealTimers()
    }
  })
})

describe('PaxMobile — typing in the form', () => {
  it('leaves the hero and the footer alone', async () => {
    const user = userEvent.setup()
    renderForm()

    const name = await screen.findByLabelText(/full name/i)
    const heroBefore = hero.renders
    const footerBefore = footer.renders

    await user.type(name, ' Jr')

    expect(name).toHaveValue('Test Passenger Jr')
    expect(hero.renders).toBe(heroBefore)
    expect(footer.renders).toBe(footerBefore)
  })

  it('leaves the other form sections alone', async () => {
    const user = userEvent.setup()
    renderForm()

    const name = await screen.findByLabelText(/full name/i)
    const addressBefore = addressSection.renders
    const atlBefore = atlSection.renders
    const windowBefore = windowSection.renders

    await user.type(name, ' Jr')

    expect(name).toHaveValue('Test Passenger Jr')
    expect(addressSection.renders).toBe(addressBefore)
    expect(atlSection.renders).toBe(atlBefore)
    expect(windowSection.renders).toBe(windowBefore)
  })
})
