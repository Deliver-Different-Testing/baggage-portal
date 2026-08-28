import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { setupServer } from 'msw/node'
import { http, HttpResponse } from 'msw'
import { ConfirmedScreen, PaxMobile } from './PaxMobile'
import { MantineTestProvider } from '../test/render'
import type { BookingSummary, ConfirmBookingRequest, TimeSlot } from '../api/client'

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
  trackingAvailable: true,
}

const slot: TimeSlot = {
  id: 'slot-1',
  runUtc: '2026-06-10T02:00:00Z',
  dayLabel: 'Today, Wed 10 Jun',
  label: '2:00 PM – 5:00 PM',
  firstAvailable: true,
}

const tomorrowSlot: TimeSlot = {
  id: 'slot-2',
  runUtc: '2026-06-10T21:00:00Z',
  dayLabel: 'Tomorrow, Thu 11 Jun',
  label: '9:00 AM – 12:00 PM',
  firstAvailable: false,
}

function renderConfirmed(bookingId: string, overrides: Partial<BookingSummary> = {}) {
  return render(
    <MemoryRouter>
      <MantineTestProvider>
        <ConfirmedScreen
          summary={{ ...summary, ...overrides }}
          slot={slot}
          bookingId={bookingId}
          address={summary.deliveryAddress}
          passengerName="Test Passenger"
          passengerPhone="+64211234567"
          passengerEmail="test@example.com"
          atlOption={{ id: 7, name: 'Safe Place' }}
          accessNotes="Behind the blue bin"
        />
      </MantineTestProvider>
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

  it('narrates the job number, the file reference and the chosen window', () => {
    renderConfirmed('token-abc-123')

    expect(screen.getByText(/^job number$/i)).toBeInTheDocument()
    expect(screen.getByText('URG-179252')).toBeInTheDocument()
    expect(screen.getByText(/^file reference$/i)).toBeInTheDocument()
    expect(screen.getByText('AKLNZ12345')).toBeInTheDocument()
    expect(screen.getByText('Delivery window')).toBeInTheDocument()
    expect(screen.getByText('Today, Wed 10 Jun')).toBeInTheDocument()
    expect(screen.getByText('2:00 PM – 5:00 PM')).toBeInTheDocument()
  })

  it('leads with the job number and closes with the file reference', () => {
    renderConfirmed('token-abc-123')

    const jobNumber = screen.getByText('URG-179252')
    const fileReference = screen.getByText('AKLNZ12345')
    const authorityToLeave = screen.getByText('Authority to leave')

    expect(jobNumber.compareDocumentPosition(authorityToLeave) & 4).toBeTruthy()
    expect(authorityToLeave.compareDocumentPosition(fileReference) & 4).toBeTruthy()
  })

  it('drops the job number tag when Despatch has no number for the job', () => {
    renderConfirmed('token-abc-123', { jobNumber: '' })

    expect(screen.queryByText(/^job number$/i)).not.toBeInTheDocument()
  })

  it('scrolls back to the top so the passenger lands on the hero', () => {
    const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => {})

    renderConfirmed('token-abc-123')

    expect(scrollTo).toHaveBeenCalledWith(0, 0)
    scrollTo.mockRestore()
  })

  it('greys out tracking until a courier is actually under way', async () => {
    renderConfirmed('token-abc-123', { trackingAvailable: false })

    expect(screen.queryByRole('link', { name: /track your delivery/i })).not.toBeInTheDocument()

    const button = screen.getByRole('button', { name: /track your delivery/i })
    expect(button).toBeDisabled()

    expect(
      screen.getByText(/tracking will be available once your delivery starts/i),
    ).toBeInTheDocument()
  })

  it('issues a full docket of what was submitted', () => {
    renderConfirmed('token-abc-123')

    expect(screen.getByText('Deliver to')).toBeInTheDocument()
    expect(screen.getByText('123 Test St')).toBeInTheDocument()
    expect(screen.getByText('Suburb, Auckland 1010')).toBeInTheDocument()

    expect(screen.getByText('Contact')).toBeInTheDocument()
    expect(screen.getByText('Test Passenger')).toBeInTheDocument()
    expect(screen.getByText('+64211234567')).toBeInTheDocument()
    expect(screen.getByText('test@example.com')).toBeInTheDocument()

    expect(screen.getByText('Authority to leave')).toBeInTheDocument()
    expect(screen.getByText('Safe Place')).toBeInTheDocument()
    expect(screen.getByText(/behind the blue bin/i)).toBeInTheDocument()
  })

  it('reads the extra delivery information back with the address', () => {
    render(
      <MemoryRouter>
        <MantineTestProvider>
          <ConfirmedScreen
            summary={summary}
            slot={slot}
            bookingId="token-abc-123"
            address={{ ...summary.deliveryAddress, line2: 'Apartment 4B, ring the buzzer' }}
            passengerName="Test Passenger"
            passengerPhone="+64211234567"
            passengerEmail="test@example.com"
            atlOption={undefined}
            accessNotes=""
          />
        </MantineTestProvider>
      </MemoryRouter>,
    )

    expect(screen.getByText('Apartment, unit or suite')).toBeInTheDocument()
    expect(screen.getByText('Apartment 4B, ring the buzzer')).toBeInTheDocument()
  })

  it('leaves no empty extra-information block when the booking has none', () => {
    renderConfirmed('token-abc-123')

    expect(screen.queryByText(/apartment, unit or suite/i)).not.toBeInTheDocument()
  })

  it('keeps the Ink hero rather than introducing a green one', () => {
    const { container } = renderConfirmed('token-abc-123')

    const hero = container.querySelector('[style*="ink-9"]')
    expect(hero).not.toBeNull()
    expect(container.querySelector('[style*="green-8"]')).toBeNull()
    expect(screen.getByText('Confirmed')).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: /you're all set/i })).toBeInTheDocument()
  })

  it('shows the Powered by Deliver DFRNT footer', () => {
    renderConfirmed('token-abc-123')

    expect(screen.getByText(/powered by/i)).toBeInTheDocument()
    expect(screen.getByRole('img', { name: /deliver dfrnt/i })).toBeInTheDocument()
  })

  it('signs off with the attribution alone', () => {
    renderConfirmed('token-abc-123')

    const footer = within(screen.getByRole('contentinfo'))
    expect(footer.queryByText('Test Air')).not.toBeInTheDocument()
    expect(footer.queryByRole('link', { name: /call/i })).not.toBeInTheDocument()
    expect(footer.getByRole('img', { name: /deliver dfrnt/i })).toBeInTheDocument()
  })
})

describe('PaxMobile — Authority to Leave submit', () => {
  const atlOptions = [
    { id: 5, name: 'Front door' },
    { id: 6, name: 'Back door' },
    { id: 7, name: 'Safe Place' },
  ]

  const booking = (overrides: Partial<BookingSummary> = {}) => ({
    ...summary,
    atlOptions,
    defaultAtlOptionId: 5,
    ...overrides,
  })

  let lastConfirmBody: ConfirmBookingRequest | null = null

  const server = setupServer(
    http.get('*/antiforgery/token', () => {
      document.cookie = 'XSRF-TOKEN=request-token-abc'
      return new HttpResponse(null, { status: 204 })
    }),
    http.get('*/pax/:id/booking', () => HttpResponse.json(booking())),
    http.get('*/pax/:id/booking/timeslots', () => HttpResponse.json([slot])),
    http.post('*/pax/:id/booking/confirm', async ({ request }) => {
      lastConfirmBody = (await request.json()) as ConfirmBookingRequest
      return HttpResponse.json({ status: 'Released', releasedAtUtc: '2026-06-10T00:00:00Z' })
    }),
  )

  beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))

  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    vi.setSystemTime(new Date('2026-06-10T01:00:00Z'))
  })

  afterEach(() => {
    vi.useRealTimers()
    server.resetHandlers()
    lastConfirmBody = null
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

  it('gives every section a real heading, not body-weight text', async () => {
    renderForm()

    await screen.findByRole('heading', { level: 2, name: /your details/i })
    expect(screen.getByRole('heading', { level: 2, name: /delivery address/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 2, name: /delivery window/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 2, name: /authority to leave/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: /confirm your baggage delivery/i })).toBeInTheDocument()
  })

  it('shows what the page is for while the booking is still loading', async () => {
    let release: (() => void) | undefined
    const held = new Promise<void>((resolve) => {
      release = resolve
    })
    server.use(
      http.get('*/pax/:id/booking', async () => {
        await held
        return HttpResponse.json(booking())
      }),
    )

    renderForm()

    expect(
      await screen.findByRole('heading', { level: 1, name: /confirm your baggage delivery/i }),
    ).toBeInTheDocument()

    release?.()
    expect(await screen.findByText('Test Air')).toBeInTheDocument()
  })

  it('offers a retry, not an unreachable support desk, when the booking will not load', async () => {
    let attempts = 0
    server.use(
      http.get('*/pax/:id/booking', () => {
        attempts += 1
        return attempts === 1 ? new HttpResponse(null, { status: 500 }) : HttpResponse.json(booking())
      }),
    )

    const user = userEvent.setup()
    renderForm()

    expect(await screen.findByText(/couldn't load your booking/i)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /try again/i }))

    expect(await screen.findByText('Test Air')).toBeInTheDocument()
  })

  it('narrates the worldtracer file reference, not the job id', async () => {
    server.use(
      http.get('*/pax/:id/booking', () =>
        HttpResponse.json(booking({ fileReference: 'AKLNZ179252' })),
      ),
    )

    renderForm()

    expect(await screen.findByText('AKLNZ179252')).toBeInTheDocument()
    expect(screen.getByText(/^file reference$/i)).toBeInTheDocument()
    expect(screen.queryByText(/REF · 42/)).not.toBeInTheDocument()
  })

  it('omits the reference line when the job carries no file reference', async () => {
    server.use(
      http.get('*/pax/:id/booking', () =>
        HttpResponse.json(booking({ fileReference: '' })),
      ),
    )

    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    expect(screen.queryByText(/file reference/i)).not.toBeInTheDocument()
  })

  it('heads the slot picker "Delivery window" rather than "Delivery time"', async () => {
    renderForm()

    expect(await screen.findByText('Delivery window')).toBeInTheDocument()
    expect(screen.queryByText('Delivery time')).not.toBeInTheDocument()
  })

  it('shows the date above the window on every option', async () => {
    server.use(
      http.get('*/pax/:id/booking/timeslots', () =>
        HttpResponse.json([slot, tomorrowSlot]),
      ),
    )

    renderForm()

    expect(await screen.findByText('Today, Wed 10 Jun')).toBeInTheDocument()
    expect(screen.getByText('2:00 PM – 5:00 PM')).toBeInTheDocument()
    expect(screen.getByText('Tomorrow, Thu 11 Jun')).toBeInTheDocument()
    expect(screen.getByText('9:00 AM – 12:00 PM')).toBeInTheDocument()
  })

  it('offers a retry when the delivery windows fail to load', async () => {
    let timeslotRequests = 0
    server.use(
      http.get('*/pax/:id/booking/timeslots', () => {
        timeslotRequests += 1
        return timeslotRequests === 1
          ? new HttpResponse(null, { status: 500 })
          : HttpResponse.json([slot])
      }),
    )

    const user = userEvent.setup()
    renderForm()

    expect(await screen.findByText(/couldn't load the available delivery windows/i))
      .toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /try again/i }))

    expect(await screen.findByText('2:00 PM – 5:00 PM')).toBeInTheDocument()
  })

  it('says so, with a number to call, when there are no windows at all', async () => {
    server.use(http.get('*/pax/:id/booking/timeslots', () => HttpResponse.json([])))

    renderForm()

    const message = await screen.findByText(/no delivery windows available for this booking yet/i)
    expect(message.parentElement).toHaveTextContent('0800 267 5494')
  })

  it('names what the passenger loses if the chosen run departs', async () => {
    renderForm()

    expect(await screen.findByText(/time left to keep this window/i)).toBeInTheDocument()
  })

  it('escalates the run-start notice inside the last ten minutes', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    try {
      vi.setSystemTime(new Date('2026-06-10T01:48:00Z'))
      server.use(http.get('*/pax/:id/booking/timeslots', () => HttpResponse.json([slot])))

      renderForm()

      const countdown = await screen.findByText('12:00')
      const calm = countdown.parentElement as HTMLElement
      expect(calm.style.backgroundColor).toBe('var(--dd-surface-container-high)')

      await act(async () => {
        vi.advanceTimersByTime(150_000)
      })

      const urgent = (await screen.findByText('9:30')).parentElement as HTMLElement
      expect(urgent.style.backgroundColor).toBe('var(--mantine-color-orange-light)')
    } finally {
      vi.useRealTimers()
    }
  })

  it('counts down to the start of the selected run, and retargets when a later one is picked', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    try {
      vi.setSystemTime(new Date('2026-06-10T01:50:00Z'))
      server.use(
        http.get('*/pax/:id/booking/timeslots', () =>
          HttpResponse.json([slot, tomorrowSlot]),
        ),
      )

      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
      renderForm()

      expect(await screen.findByText('10:00')).toBeInTheDocument()

      await act(async () => {
        vi.advanceTimersByTime(5_000)
      })
      expect(screen.getByText('9:55')).toBeInTheDocument()

      await user.click(screen.getByText('Tomorrow, Thu 11 Jun'))

      expect(await screen.findByText(/^19:\d{2}:\d{2}$/)).toBeInTheDocument()
    } finally {
      vi.useRealTimers()
    }
  })

  it('refetches the windows and drops the selection once the run has departed', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    try {
      vi.setSystemTime(new Date('2026-06-10T01:59:00Z'))
      let timeslotRequests = 0
      server.use(
        http.get('*/pax/:id/booking/timeslots', () => {
          timeslotRequests += 1
          return HttpResponse.json(timeslotRequests === 1 ? [slot] : [tomorrowSlot])
        }),
      )

      renderForm()

      await screen.findByText('Today, Wed 10 Jun')

      await act(async () => {
        vi.advanceTimersByTime(61_000)
      })

      await waitFor(() => expect(timeslotRequests).toBe(2))
      expect(await screen.findByText('Tomorrow, Thu 11 Jun')).toBeInTheDocument()
      expect(screen.queryByText('Today, Wed 10 Jun')).not.toBeInTheDocument()
    } finally {
      vi.useRealTimers()
    }
  })

  async function confirmAddress(user: ReturnType<typeof userEvent.setup>) {
    await user.click(await screen.findByRole('checkbox', { name: /this address is correct/i }))
  }

  async function editAddress(user: ReturnType<typeof userEvent.setup>) {
    await user.click(await screen.findByRole('button', { name: /^edit$/i }))
    await screen.findByRole('textbox', { name: /street name/i })
  }

  async function openReview(user: ReturnType<typeof userEvent.setup>) {
    await user.click(screen.getByRole('button', { name: /review delivery/i }))
  }

  async function reviewAndConfirm(user: ReturnType<typeof userEvent.setup>) {
    await openReview(user)
    await user.click(await screen.findByRole('button', { name: /^confirm delivery$/i }))
  }

  function reviewDialog() {
    return screen.queryByRole('dialog', { name: /check your delivery details/i })
  }

  async function enableAtl(user: ReturnType<typeof userEvent.setup>) {
    await waitFor(() => expect(screen.getByRole('switch')).toBeEnabled())
    await user.click(screen.getByRole('switch'))
  }

  it('opens on the saved address to confirm, not on a form to fill in', async () => {
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)

    expect(screen.getByText('123 Test St')).toBeInTheDocument()
    expect(screen.getByText('Suburb, Auckland 1010')).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: /this address is correct/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^edit$/i })).toBeInTheDocument()

    expect(screen.queryByRole('textbox', { name: /street name/i })).not.toBeInTheDocument()
    expect(
      screen.queryByRole('textbox', { name: /apartment, unit or suite/i }),
    ).not.toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: /search address/i })).not.toBeInTheDocument()

    expect(screen.getByRole('textbox', { name: /full name/i })).toBeEnabled()
  })

  it('reveals the whole address form behind Edit', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await editAddress(user)

    expect(screen.getByRole('combobox', { name: /search address/i })).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: /street number/i })).toHaveValue('123')
    expect(screen.getByRole('textbox', { name: /street name/i })).toHaveValue('Test St')
    expect(screen.getByRole('textbox', { name: /apartment, unit or suite/i })).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: /suburb/i })).toHaveValue('Suburb')
    expect(screen.getByRole('textbox', { name: /city/i })).toHaveValue('Auckland')
    expect(screen.getByRole('textbox', { name: /postcode/i })).toHaveValue('1010')
    expect(screen.getByRole('textbox', { name: /country/i })).toHaveValue('NZ')
  })

  it('relabels the locality fields for a US address', async () => {
    server.use(
      http.get('*/pax/:id/booking', () =>
        HttpResponse.json(
          booking({
            deliveryAddress: {
              line3: '350',
              line4: '5th Ave',
              line5: 'New York',
              line6: 'NY',
              line7: '10118',
              country: 'US',
            },
          }),
        ),
      ),
    )

    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await editAddress(user)

    expect(screen.getByRole('textbox', { name: /^city$/i })).toHaveValue('New York')
    expect(screen.getByRole('textbox', { name: /^state$/i })).toHaveValue('NY')
    expect(screen.getByRole('textbox', { name: /zip code/i })).toHaveValue('10118')
    expect(screen.queryByRole('textbox', { name: /suburb/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('textbox', { name: /postcode/i })).not.toBeInTheDocument()
  })

  it('drops stale coordinates when the passenger retypes the street', async () => {
    server.use(
      http.get('*/pax/:id/booking', () =>
        HttpResponse.json(
          booking({
            deliveryAddress: {
              ...summary.deliveryAddress,
              latitude: -36.8485,
              longitude: 174.7633,
            },
          }),
        ),
      ),
    )

    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await editAddress(user)
    await user.clear(screen.getByRole('textbox', { name: /street name/i }))
    await user.type(screen.getByRole('textbox', { name: /street name/i }), 'Other St')
    await confirmAddress(user)
    await reviewAndConfirm(user)

    await waitFor(() => expect(lastConfirmBody).not.toBeNull())
    expect(lastConfirmBody!.address.latitude).toBeNull()
    expect(lastConfirmBody!.address.longitude).toBeNull()
  })

  it('keeps the coordinates that came with the booking when the street is untouched', async () => {
    server.use(
      http.get('*/pax/:id/booking', () =>
        HttpResponse.json(
          booking({
            deliveryAddress: {
              ...summary.deliveryAddress,
              latitude: -36.8485,
              longitude: 174.7633,
            },
          }),
        ),
      ),
    )

    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await confirmAddress(user)
    await reviewAndConfirm(user)

    await waitFor(() => expect(lastConfirmBody).not.toBeNull())
    expect(lastConfirmBody!.address.latitude).toBe(-36.8485)
    expect(lastConfirmBody!.address.longitude).toBe(174.7633)
  })

  it('posts each address line under its own key', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await confirmAddress(user)
    await reviewAndConfirm(user)

    await waitFor(() => expect(lastConfirmBody).not.toBeNull())
    expect(lastConfirmBody!.address).toMatchObject({
      line3: '123',
      line4: 'Test St',
      line5: 'Suburb',
      line6: 'Auckland',
      line7: '1010',
      country: 'NZ',
    })
  })

  it('closes the editor again once the passenger says the address is correct', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await editAddress(user)

    await confirmAddress(user)

    await waitFor(() =>
      expect(screen.queryByRole('textbox', { name: /street name/i })).not.toBeInTheDocument(),
    )
    expect(screen.queryByRole('combobox', { name: /search address/i })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^edit$/i })).toBeInTheDocument()
  })

  it('posts extra delivery information as the second address line', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await editAddress(user)
    await user.type(
      screen.getByRole('textbox', { name: /apartment, unit or suite/i }),
      'Apartment 4B, ring the buzzer',
    )

    await confirmAddress(user)
    await reviewAndConfirm(user)

    await waitFor(() => expect(lastConfirmBody).not.toBeNull())
    expect(lastConfirmBody!.address.line2).toBe('Apartment 4B, ring the buzzer')
  })

  it('shows the saved extra delivery information without opening the editor', async () => {
    server.use(
      http.get('*/pax/:id/booking', () =>
        HttpResponse.json(
          booking({
            deliveryAddress: { ...summary.deliveryAddress, line2: 'Gate code 1234' },
          }),
        ),
      ),
    )

    renderForm()

    expect(await screen.findByText('Apartment, unit or suite')).toBeInTheDocument()
    expect(screen.getByText('Gate code 1234')).toBeInTheDocument()
  })

  it('leaves no empty extra-information block when the booking has none', async () => {
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    expect(screen.queryByText(/apartment, unit or suite/i)).not.toBeInTheDocument()
  })

  it('reads the extra delivery information back in the review dialog', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await editAddress(user)
    await user.type(
      screen.getByRole('textbox', { name: /apartment, unit or suite/i }),
      'Apartment 4B, ring the buzzer',
    )
    await confirmAddress(user)
    await openReview(user)

    const dialog = within(
      await screen.findByRole('dialog', { name: /check your delivery details/i }),
    )
    expect(dialog.getByText('Apartment, unit or suite')).toBeInTheDocument()
    expect(dialog.getByText('Apartment 4B, ring the buzzer')).toBeInTheDocument()
  })

  it('reopens the editor when a field hidden behind it is invalid', async () => {
    server.use(
      http.get('*/pax/:id/booking', () =>
        HttpResponse.json(
          booking({ deliveryAddress: { ...summary.deliveryAddress, line5: '' } }),
        ),
      ),
    )

    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await confirmAddress(user)
    await openReview(user)

    expect(await screen.findByRole('textbox', { name: /suburb/i })).toBeInTheDocument()
    expect(screen.getByText(/please enter your suburb/i)).toBeInTheDocument()
    expect(reviewDialog()).not.toBeInTheDocument()
    expect(lastConfirmBody).toBeNull()
  })

  it('blocks submit until the address is confirmed', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() => expect(screen.getByRole('button', { name: /review delivery/i })).toBeEnabled())

    await openReview(user)

    expect(
      await screen.findByText(/please confirm your delivery address is correct/i),
    ).toBeInTheDocument()
    expect(reviewDialog()).not.toBeInTheDocument()
    expect(lastConfirmBody).toBeNull()
  })

  it('arrives with Authority to Leave off', async () => {
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)

    await waitFor(() => expect(screen.getByRole('switch')).toBeEnabled())
    expect(screen.getByRole('switch')).not.toBeChecked()
    expect(screen.queryByRole('radio', { name: 'Front door' })).not.toBeInTheDocument()
  })

  it('selects the front door once the passenger switches Authority to Leave on', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await enableAtl(user)

    expect(await screen.findByRole('radio', { name: 'Front door' })).toBeChecked()
  })

  it('never offers an option the server has excluded', async () => {
    server.use(
      http.get('*/pax/:id/booking', () =>
        HttpResponse.json(
          booking({ atlOptions: [{ id: 5, name: 'Front door' }, { id: 9, name: 'Reception' }] }),
        ),
      ),
    )

    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await enableAtl(user)

    expect(await screen.findByRole('radio', { name: 'Front door' })).toBeInTheDocument()
    expect(screen.queryByRole('radio', { name: /letter box/i })).not.toBeInTheDocument()
  })

  it('posts the selected ATL option id as a number', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await enableAtl(user)

    await user.click(await screen.findByRole('radio', { name: 'Back door' }))

    await confirmAddress(user)
    await reviewAndConfirm(user)

    await waitFor(() => expect(lastConfirmBody).not.toBeNull())
    expect(lastConfirmBody!.atlOptionId).toBe(6)
    expect(typeof lastConfirmBody!.atlOptionId).toBe('number')
  })

  it('blocks submit and requires additional details when Safe Place is selected', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await enableAtl(user)

    await user.click(await screen.findByRole('radio', { name: 'Safe Place' }))

    await confirmAddress(user)
    await openReview(user)

    expect(
      await screen.findByText(/please describe the safe place/i),
    ).toBeInTheDocument()
    expect(reviewDialog()).not.toBeInTheDocument()
    expect(lastConfirmBody).toBeNull()
  })

  it('submits once additional details are provided for Safe Place', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await enableAtl(user)

    await user.click(await screen.findByRole('radio', { name: 'Safe Place' }))
    await user.type(
      screen.getByRole('textbox', { name: /additional details/i }),
      'Behind the garden shed',
    )

    await confirmAddress(user)
    await reviewAndConfirm(user)

    await waitFor(() => expect(lastConfirmBody).not.toBeNull())
    expect(lastConfirmBody!.atlOptionId).toBe(7)
    expect(lastConfirmBody!.accessNotes).toBe('Behind the garden shed')
  })

  it('blocks submit when the timeslot has no runUtc instead of posting without a delivery time', async () => {
    server.use(
      http.get('*/pax/:id/booking/timeslots', () =>
        HttpResponse.json([
          {
            id: 'slot-1',
            startUtc: '2026-06-10T02:00:00Z',
            endUtc: '2026-06-10T04:00:00Z',
            label: '2pm – 4pm',
            firstAvailable: true,
          },
        ]),
      ),
    )

    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() => expect(screen.getByRole('button', { name: /review delivery/i })).toBeEnabled())

    await confirmAddress(user)
    await openReview(user)

    expect(await screen.findByText(/please pick a delivery window/i)).toBeInTheDocument()
    expect(reviewDialog()).not.toBeInTheDocument()
    expect(lastConfirmBody).toBeNull()
  })

  it('shows a server validation message instead of the generic retry toast', async () => {
    server.use(
      http.post('*/pax/:id/booking/confirm', () =>
        HttpResponse.json(
          { errors: { AccessNotes: ['Additional details must be 120 characters or fewer.'] } },
          { status: 400 },
        ),
      ),
    )

    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() => expect(screen.getByRole('button', { name: /review delivery/i })).toBeEnabled())

    await confirmAddress(user)
    await reviewAndConfirm(user)

    expect(await screen.findByText(/120 characters or fewer/i)).toBeInTheDocument()
    expect(screen.queryByText(/could not submit your confirmation/i)).not.toBeInTheDocument()
  })

  it('posts null when the passenger leaves Authority to Leave off', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() => expect(screen.getByRole('switch')).toBeEnabled())
    expect(screen.getByRole('switch')).not.toBeChecked()

    await confirmAddress(user)
    await reviewAndConfirm(user)

    await waitFor(() => expect(lastConfirmBody).not.toBeNull())
    expect(lastConfirmBody!.atlOptionId).toBeNull()
  })

  it('posts the country from the booking summary unchanged', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() => expect(screen.getByRole('button', { name: /review delivery/i })).toBeEnabled())

    await confirmAddress(user)
    await reviewAndConfirm(user)

    await waitFor(() => expect(lastConfirmBody).not.toBeNull())
    expect(lastConfirmBody!.address.country).toBe('NZ')
  })

  it('blocks submit and prompts for a country when the stored one is unresolved', async () => {
    server.use(
      http.get('*/pax/:id/booking', () =>
        HttpResponse.json(
          booking({ deliveryAddress: { ...summary.deliveryAddress, country: '' } }),
        ),
      ),
    )

    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() => expect(screen.getByRole('button', { name: /review delivery/i })).toBeEnabled())

    await editAddress(user)
    expect(screen.getByRole('textbox', { name: /country/i })).toHaveValue('')

    await confirmAddress(user)
    await openReview(user)

    expect(await screen.findByText(/please enter your country/i)).toBeInTheDocument()
    expect(reviewDialog()).not.toBeInTheDocument()
    expect(lastConfirmBody).toBeNull()
  })

  it('posts a country the passenger typed into the field', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() => expect(screen.getByRole('button', { name: /review delivery/i })).toBeEnabled())

    await editAddress(user)
    const country = screen.getByRole('textbox', { name: /country/i })
    await user.clear(country)
    await user.type(country, 'Australia')

    await confirmAddress(user)
    await reviewAndConfirm(user)

    await waitFor(() => expect(lastConfirmBody).not.toBeNull())
    expect(lastConfirmBody!.address.country).toBe('Australia')
  })

  it('shows the server country message on the field when the spelling is unrecognisable', async () => {
    server.use(
      http.post('*/pax/:id/booking/confirm', () =>
        HttpResponse.json(
          { errors: { 'Address.Country': ["We couldn't recognise the country on your delivery address."] } },
          { status: 400 },
        ),
      ),
    )

    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() => expect(screen.getByRole('button', { name: /review delivery/i })).toBeEnabled())

    await confirmAddress(user)
    await reviewAndConfirm(user)

    const country = await screen.findByRole('textbox', { name: /country/i })
    await waitFor(() => expect(country).toHaveAttribute('aria-invalid', 'true'))
    expect(
      screen.getAllByText(/couldn't recognise the country on your delivery address/i).length,
    ).toBeGreaterThan(0)
    await waitFor(() => expect(country).toBeEnabled())
    expect(screen.getByRole('checkbox', { name: /this address is correct/i })).not.toBeChecked()
  })

  it('reads the booking back before sending it', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /review delivery/i })).toBeEnabled(),
    )
    await enableAtl(user)
    await user.click(await screen.findByRole('radio', { name: 'Safe Place' }))
    await user.type(
      screen.getByRole('textbox', { name: /additional details/i }),
      'Behind the garden shed',
    )
    await confirmAddress(user)

    await openReview(user)

    const dialog = within(await screen.findByRole('dialog'))
    expect(dialog.getByText('2:00 PM – 5:00 PM')).toBeInTheDocument()
    expect(dialog.getByText('Today, Wed 10 Jun')).toBeInTheDocument()
    expect(dialog.getByText('123 Test St')).toBeInTheDocument()
    expect(dialog.getByText('Suburb, Auckland 1010')).toBeInTheDocument()
    expect(dialog.getByText('NZ')).toBeInTheDocument()
    expect(dialog.getByText('Test Passenger')).toBeInTheDocument()
    expect(dialog.getByText('+64211234567')).toBeInTheDocument()
    expect(dialog.getByText('test@example.com')).toBeInTheDocument()
    expect(dialog.getByText('Safe Place')).toBeInTheDocument()
    expect(dialog.getByText(/behind the garden shed/i)).toBeInTheDocument()

    expect(lastConfirmBody).toBeNull()
  })

  it('spells out that nobody may leave the bag when Authority to Leave is off', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() => expect(screen.getByRole('switch')).toBeEnabled())
    await confirmAddress(user)
    await openReview(user)

    const dialog = within(await screen.findByRole('dialog'))
    expect(dialog.getByText('Not authorised')).toBeInTheDocument()
    expect(dialog.getByText(/someone will need to be there to take the bag/i)).toBeInTheDocument()
  })

  it('reads the file reference back with the delivery details', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() => expect(screen.getByRole('switch')).toBeEnabled())
    await confirmAddress(user)
    await openReview(user)

    const dialog = within(await screen.findByRole('dialog'))
    expect(dialog.getByText(/^file reference$/i)).toBeInTheDocument()
    expect(dialog.getByText('AKLNZ12345')).toBeInTheDocument()
  })

  it('omits the file reference from the read-back when the job carries none', async () => {
    server.use(
      http.get('*/pax/:id/booking', () => HttpResponse.json(booking({ fileReference: '' }))),
    )
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() => expect(screen.getByRole('switch')).toBeEnabled())
    await confirmAddress(user)
    await openReview(user)

    const dialog = within(await screen.findByRole('dialog'))
    expect(dialog.queryByText(/file reference/i)).not.toBeInTheDocument()
  })

  it('sends nothing when the passenger goes back to edit', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /review delivery/i })).toBeEnabled(),
    )
    await confirmAddress(user)
    await openReview(user)

    await user.click(await screen.findByRole('button', { name: /edit details/i }))

    await waitFor(() => expect(reviewDialog()).not.toBeInTheDocument())
    expect(lastConfirmBody).toBeNull()
    expect(screen.getByRole('checkbox', { name: /this address is correct/i })).toBeChecked()
  })

  it('drops the read-back when the chosen run departs, without sending', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    try {
      vi.setSystemTime(new Date('2026-06-10T01:59:00Z'))
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
      renderForm()

      await screen.findByText(/confirm your baggage delivery/i)
      await waitFor(() =>
        expect(screen.getByRole('button', { name: /review delivery/i })).toBeEnabled(),
      )
      await confirmAddress(user)
      await openReview(user)
      expect(await screen.findByRole('dialog')).toBeInTheDocument()

      await act(async () => {
        vi.advanceTimersByTime(61_000)
      })

      await waitFor(() => expect(reviewDialog()).not.toBeInTheDocument())
      expect(lastConfirmBody).toBeNull()
    } finally {
      vi.useRealTimers()
    }
  })

  it('closes the read-back when the server rejects the confirmation', async () => {
    server.use(
      http.post('*/pax/:id/booking/confirm', () =>
        HttpResponse.json(
          { errors: { AccessNotes: ['Additional details must be 120 characters or fewer.'] } },
          { status: 400 },
        ),
      ),
    )

    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /review delivery/i })).toBeEnabled(),
    )
    await confirmAddress(user)
    await reviewAndConfirm(user)

    await waitFor(() => expect(reviewDialog()).not.toBeInTheDocument())
    expect(await screen.findByText(/120 characters or fewer/i)).toBeInTheDocument()
  })

  it('leads the hero with the task, with the airline as the identity line above it', async () => {
    renderForm()

    const [airline] = await screen.findAllByText('Test Air')
    const task = screen.getByRole('heading', { level: 1, name: /Confirm your baggage delivery/i })

    expect(airline.compareDocumentPosition(task) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })

  it('sets the file reference as a baggage tag under the task', async () => {
    renderForm()

    const value = await screen.findByText('AKLNZ12345')
    const label = screen.getByText(/^file reference$/i)
    const task = screen.getByRole('heading', { level: 1, name: /Confirm your baggage delivery/i })

    expect(task.compareDocumentPosition(value) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(label.compareDocumentPosition(value) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })

  it('gives the delivery window more weight than the day it falls on', async () => {
    renderForm()

    const hours = await screen.findByText('2:00 PM – 5:00 PM')
    const day = screen.getByText('Today, Wed 10 Jun')

    expect(hours).toHaveStyle({ fontWeight: '700' })
    expect(day).not.toHaveStyle({ fontWeight: '700' })
  })

  it('reports each section as complete or outstanding on its header chip', async () => {
    const user = userEvent.setup()
    renderForm()

    expect(await screen.findByLabelText('Your details — complete')).toBeInTheDocument()
    expect(
      screen.getByLabelText('Delivery address — not filled in yet'),
    ).toBeInTheDocument()

    await confirmAddress(user)

    expect(await screen.findByLabelText('Delivery address — complete')).toBeInTheDocument()
  })

  it('marks Authority to leave off rather than outstanding when it is switched off', async () => {
    server.use(
      http.get('*/pax/:id/booking', () =>
        HttpResponse.json(booking({ atlOptions: [], defaultAtlOptionId: null })),
      ),
    )
    renderForm()

    expect(await screen.findByLabelText('Authority to leave — off')).toBeInTheDocument()
  })

  it('flips a section back to outstanding when a required field is emptied', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByLabelText('Your details — complete')
    await user.clear(screen.getByRole('textbox', { name: /full name/i }))

    expect(
      await screen.findByLabelText('Your details — not filled in yet'),
    ).toBeInTheDocument()
  })

  async function windowOptions() {
    const group = await screen.findByRole('radiogroup', { name: /delivery window/i })
    return within(group).getAllByRole('radio')
  }

  it('exposes the delivery windows as one keyboard-navigable radio group', async () => {
    server.use(
      http.get('*/pax/:id/booking/timeslots', () => HttpResponse.json([slot, tomorrowSlot])),
    )
    const user = userEvent.setup()
    renderForm()

    const options = await windowOptions()
    expect(options).toHaveLength(2)

    expect(options[0]).toHaveAttribute('aria-checked', 'true')
    expect(options[0]).toHaveAttribute('tabindex', '0')
    expect(options[1]).toHaveAttribute('tabindex', '-1')

    options[0].focus()
    await user.keyboard('{ArrowDown}')

    await waitFor(async () =>
      expect((await windowOptions())[1]).toHaveAttribute('aria-checked', 'true'),
    )
    const moved = await windowOptions()
    expect(moved[0]).toHaveAttribute('aria-checked', 'false')
    expect(moved[1]).toHaveFocus()
  })

  it('wraps arrow-key selection and jumps to the ends with Home and End', async () => {
    server.use(
      http.get('*/pax/:id/booking/timeslots', () => HttpResponse.json([slot, tomorrowSlot])),
    )
    const user = userEvent.setup()
    renderForm()

    ;(await windowOptions())[0].focus()

    await user.keyboard('{ArrowUp}')
    await waitFor(async () =>
      expect((await windowOptions())[1]).toHaveAttribute('aria-checked', 'true'),
    )

    await user.keyboard('{Home}')
    await waitFor(async () =>
      expect((await windowOptions())[0]).toHaveAttribute('aria-checked', 'true'),
    )

    await user.keyboard('{End}')
    await waitFor(async () =>
      expect((await windowOptions())[1]).toHaveAttribute('aria-checked', 'true'),
    )
  })

  it('orders the read-back footer the way Despatch does, and never reverses it in CSS', async () => {
    const user = userEvent.setup()
    renderForm()

    await waitFor(() =>
      expect(screen.getByRole('button', { name: /review delivery/i })).toBeEnabled(),
    )
    await confirmAddress(user)
    await user.click(screen.getByRole('button', { name: /review delivery/i }))

    const dialog = within(await screen.findByRole('dialog'))
    const confirm = dialog.getByRole('button', { name: /confirm delivery/i })
    const edit = dialog.getByRole('button', { name: /edit details/i })

    expect(edit.compareDocumentPosition(confirm) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()

    const footer = confirm.closest('.dd-dialog-footer')
    expect(footer).not.toBeNull()
    expect(footer).toContainElement(edit)
  })

  it('carries the Despatch dialog header — a titled bar with its own close button', async () => {
    const user = userEvent.setup()
    renderForm()

    await waitFor(() =>
      expect(screen.getByRole('button', { name: /review delivery/i })).toBeEnabled(),
    )
    await confirmAddress(user)
    await user.click(screen.getByRole('button', { name: /review delivery/i }))

    const dialog = within(await screen.findByRole('dialog'))
    expect(dialog.getByRole('heading', { level: 2, name: /check your delivery details/i })).toBeInTheDocument()
    expect(dialog.getByText(/we'll book this as soon as you confirm/i)).toBeInTheDocument()
    expect(dialog.getByRole('button', { name: /close dialog/i })).toBeInTheDocument()
  })

  it('signs the confirm page off with the attribution alone', async () => {
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)

    const footer = within(screen.getByRole('contentinfo'))
    expect(footer.queryByText('Test Air')).not.toBeInTheDocument()
    expect(footer.queryByRole('link', { name: /call/i })).not.toBeInTheDocument()
    expect(footer.queryByText(/need help\?/i)).not.toBeInTheDocument()
    expect(footer.getByText(/powered by/i)).toBeInTheDocument()
  })

  it('names the bar button for what it does — review, not confirm', async () => {
    renderForm()

    const bar = await screen.findByRole('button', { name: /review delivery/i })
    expect(bar).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /review and confirm/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^confirm delivery$/i })).not.toBeInTheDocument()
  })

  it('shows the three delivery stages with Confirm current', async () => {
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)

    expect(screen.getByText('Confirm')).toBeInTheDocument()
    expect(screen.getByText('In transit')).toBeInTheDocument()
    expect(screen.getByText('Delivered')).toBeInTheDocument()

    const stepper = document.querySelector('.mantine-Stepper-root') as HTMLElement
    const steps = within(stepper).getAllByRole('button')
    expect(steps).toHaveLength(3)
    expect(steps[0]).toHaveAttribute('data-progress')
    steps.forEach((step) => expect(step).toHaveAttribute('tabindex', '-1'))
  })
})

describe('PaxMobile — a booking that is already confirmed', () => {
  const confirmedBooking = {
    ...summary,
    atlOptions: [{ id: 7, name: 'Safe Place' }],
    trackingAvailable: false,
    confirmation: {
      confirmedAtUtc: '2026-06-10T00:30:00Z',
      deliveryTimeUtc: '2026-06-10T21:00:00Z',
      dayLabel: 'Tomorrow, Thu 11 Jun',
      windowLabel: '9:00 AM – 12:00 PM',
      atlOptionId: 7,
      accessNotes: 'Behind the blue bin',
    },
  }

  const server = setupServer(
    http.get('*/antiforgery/token', () => new HttpResponse(null, { status: 204 })),
    http.get('*/pax/:id/booking', () => HttpResponse.json(confirmedBooking)),
    http.get('*/pax/:id/booking/timeslots', () => HttpResponse.json([slot])),
  )

  beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
  afterEach(() => server.resetHandlers())
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

  it('reopens as the confirmation, never as a form the passenger can resubmit', async () => {
    renderPage()

    expect(await screen.findByRole('heading', { level: 1, name: /you're all set/i })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /review and confirm/i })).not.toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { level: 1, name: /confirm your baggage delivery/i }),
    ).not.toBeInTheDocument()
  })

  it('reads the stored booking back rather than the first available window', async () => {
    renderPage()

    await screen.findByRole('heading', { level: 1, name: /you're all set/i })

    expect(screen.getByText('Tomorrow, Thu 11 Jun')).toBeInTheDocument()
    expect(screen.getByText('9:00 AM – 12:00 PM')).toBeInTheDocument()
    expect(screen.getByText('Safe Place')).toBeInTheDocument()
    expect(screen.getByText(/behind the blue bin/i)).toBeInTheDocument()
    expect(screen.getByText('Test Passenger')).toBeInTheDocument()
  })

  it('holds tracking closed until the job is under way', async () => {
    renderPage()

    await screen.findByRole('heading', { level: 1, name: /you're all set/i })

    expect(screen.getByRole('button', { name: /track your delivery/i })).toBeDisabled()
  })
})
