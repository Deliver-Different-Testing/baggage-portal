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
  reference: 'REF-42',
  airlineLabel: 'Test Air',
  supportPhone: '0800 267 5494',
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

function renderConfirmed(bookingId: string) {
  return render(
    <MemoryRouter>
      <MantineTestProvider>
        <ConfirmedScreen
          summary={summary}
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

  it('narrates the file reference and the chosen window', () => {
    renderConfirmed('token-abc-123')

    // The reference is set as a baggage tag, same as the confirm hero: label and
    // value are separate nodes because the helpline asks for the value alone.
    expect(screen.getByText(/^file reference$/i)).toBeInTheDocument()
    expect(screen.getByText('REF-42')).toBeInTheDocument()
    expect(screen.getByText('Delivery window')).toBeInTheDocument()
    expect(screen.getByText('Today, Wed 10 Jun')).toBeInTheDocument()
    expect(screen.getByText('2:00 PM – 5:00 PM')).toBeInTheDocument()
  })

  it('issues a full docket of what was submitted', () => {
    // This is the passenger's only record of the booking and the screen they are
    // most likely to screenshot, so it reads back every field they filled in.
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

  it('keeps the Ink hero rather than introducing a green one', () => {
    const { container } = renderConfirmed('token-abc-123')

    // A full-bleed green band was a fourth brand colour at the moment the
    // passenger is most likely to remember, and it dropped the airline theme.
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

    // The carrier name and the support number both left the footer: the name only
    // repeated the hero, and the number belongs where the passenger is stuck, not
    // under a screen that already told them everything worked.
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

  // The server resolves which option the page arrives on, so the portal never has
  // to know a LeaveNotHomeId or match a display name.
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

  // The slot fixtures depart at fixed 2026-06-10 instants, so on any real clock
  // past that date every run has already left: useRunStartCountdown fires onExpire
  // on mount, which drops the selection and closes the review dialog. That raced
  // every click in this describe. Pin the clock just before the first departure so
  // the countdown behaves as it does for a passenger with a live booking; the few
  // tests that need a different instant still call setSystemTime themselves.
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

  it('narrates the baggage file reference, not the job id', async () => {
    server.use(
      http.get('*/pax/:id/booking', () =>
        HttpResponse.json(booking({ reference: 'AKLA2633476' })),
      ),
    )

    renderForm()

    // Label and value are separate nodes on the tag, so they are matched apart.
    expect(await screen.findByText('AKLA2633476')).toBeInTheDocument()
    expect(screen.getByText(/^file reference$/i)).toBeInTheDocument()
    expect(screen.queryByText(/REF · 42/)).not.toBeInTheDocument()
  })

  it('omits the reference line when the job carries no file reference', async () => {
    server.use(
      http.get('*/pax/:id/booking', () =>
        HttpResponse.json(booking({ reference: '' })),
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

    // Both halves are visible, so the passenger can tell today from tomorrow
    // without decoding a time on its own.
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

    // The card used to render as an empty box, which reads as the page being
    // broken rather than as something the passenger can do anything about.
    expect(await screen.findByText(/couldn't load the available delivery windows/i))
      .toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /try again/i }))

    expect(await screen.findByText('2:00 PM – 5:00 PM')).toBeInTheDocument()
  })

  it('says so, with a number to call, when there are no windows at all', async () => {
    server.use(http.get('*/pax/:id/booking/timeslots', () => HttpResponse.json([])))

    renderForm()

    // The number also sits in the footer sign-off, so match it on the empty-state
    // line itself rather than page-wide.
    const message = await screen.findByText(/no delivery windows available for this booking yet/i)
    expect(message).toHaveTextContent('0800 267 5494')
  })

  it('tells the passenger to confirm before the chosen run departs', async () => {
    renderForm()

    expect(
      await screen.findByText(/make sure you confirm your booking before the chosen run time/i),
    ).toBeInTheDocument()
    expect(screen.queryByText(/confirm within 10 minutes/i)).not.toBeInTheDocument()
  })

  it('counts down to the start of the selected run, and retargets when a later one is picked', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    try {
      // slot departs at 02:00Z, tomorrowSlot at 21:00Z.
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

      // Nineteen hours and change out — the note says "the chosen run time", so
      // the deadline has to follow the choice.
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
          // The second response drops the departed first window, so the refreshed
          // list can only be showing tomorrow if the refetch landed.
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

  // The address gate gets in the way of every submit on purpose. Tick it after any
  // address edits — ticking closes the editor.
  async function confirmAddress(user: ReturnType<typeof userEvent.setup>) {
    await user.click(await screen.findByRole('checkbox', { name: /this address is correct/i }))
  }

  // Almost every bag goes to the address already on the booking, so the fields
  // live behind Edit rather than in front of the passenger.
  async function editAddress(user: ReturnType<typeof userEvent.setup>) {
    await user.click(await screen.findByRole('button', { name: /^edit$/i }))
    await screen.findByRole('textbox', { name: /street address/i })
  }

  // The bar button only opens the read-back now; the dialog's own button sends.
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

  it('opens on the saved address to confirm, not on a form to fill in', async () => {
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)

    // Read-first: the address the bag is already going to, and one tick to say so.
    expect(screen.getByText('123 Test St')).toBeInTheDocument()
    expect(screen.getByText('Suburb, Auckland 1010')).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: /this address is correct/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^edit$/i })).toBeInTheDocument()

    // Everything else is the exception path and stays out of the way.
    expect(screen.queryByRole('textbox', { name: /street address/i })).not.toBeInTheDocument()
    expect(
      screen.queryByRole('textbox', { name: /extra delivery information/i }),
    ).not.toBeInTheDocument()
    // By role, not by label: a collapsed Mantine Collapse keeps its children
    // mounted but out of the accessibility tree, which is the state that matters.
    expect(screen.queryByRole('combobox', { name: /search address/i })).not.toBeInTheDocument()

    // The passenger's own details are not the address, and stay editable.
    expect(screen.getByRole('textbox', { name: /full name/i })).toBeEnabled()
  })

  it('reveals the whole address form behind Edit', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await editAddress(user)

    expect(screen.getByRole('combobox', { name: /search address/i })).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: /street address/i })).toHaveValue('123 Test St')
    expect(screen.getByRole('textbox', { name: /extra delivery information/i })).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: /suburb/i })).toHaveValue('Suburb')
    expect(screen.getByRole('textbox', { name: /city/i })).toHaveValue('Auckland')
    expect(screen.getByRole('textbox', { name: /postcode/i })).toHaveValue('1010')
    expect(screen.getByRole('textbox', { name: /country/i })).toHaveValue('NZ')
  })

  it('closes the editor again once the passenger says the address is correct', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await editAddress(user)

    await confirmAddress(user)

    await waitFor(() =>
      expect(screen.queryByRole('textbox', { name: /street address/i })).not.toBeInTheDocument(),
    )
    // The search is a tool for changing the address — no reason to keep offering
    // it once the passenger has said the address is right.
    // By role, not by label: a collapsed Mantine Collapse keeps its children
    // mounted but out of the accessibility tree, which is the state that matters.
    expect(screen.queryByRole('combobox', { name: /search address/i })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^edit$/i })).toBeInTheDocument()
  })

  it('posts extra delivery information as the second address line', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await editAddress(user)
    await user.type(
      screen.getByRole('textbox', { name: /extra delivery information/i }),
      'Apartment 4B, ring the buzzer',
    )

    await confirmAddress(user)
    await reviewAndConfirm(user)

    await waitFor(() => expect(lastConfirmBody).not.toBeNull())
    expect(lastConfirmBody!.address.line2).toBe('Apartment 4B, ring the buzzer')
  })

  it('reopens the editor when a field hidden behind it is invalid', async () => {
    server.use(
      http.get('*/pax/:id/booking', () =>
        HttpResponse.json(
          booking({ deliveryAddress: { ...summary.deliveryAddress, suburb: '' } }),
        ),
      ),
    )

    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await confirmAddress(user)
    await openReview(user)

    // An error on a collapsed field is an error the passenger cannot see, let
    // alone fix.
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

  it('arrives with Authority to Leave on and the default option selected', async () => {
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)

    // Front door is the normal handoff point for a suitcase; making the passenger
    // opt into it costs two taps on the path almost everyone takes.
    await waitFor(() => expect(screen.getByRole('switch')).toBeChecked())
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

    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)

    expect(await screen.findByRole('radio', { name: 'Front door' })).toBeInTheDocument()
    expect(screen.queryByRole('radio', { name: /letter box/i })).not.toBeInTheDocument()
  })

  it('posts the selected ATL option id as a number', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() => expect(screen.getByRole('switch')).toBeEnabled())

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
    await waitFor(() => expect(screen.getByRole('switch')).toBeEnabled())

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
    await waitFor(() => expect(screen.getByRole('switch')).toBeEnabled())

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
    // The shape served before the per-client economy-run change: it still matches
    // by id, so the slot reads as selected, but runUtc is undefined and
    // JSON.stringify drops deliveryTimeUtc from the body entirely.
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

  it('posts null when the passenger switches Authority to Leave off', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() => expect(screen.getByRole('switch')).toBeChecked())

    await user.click(screen.getByRole('switch'))

    await confirmAddress(user)
    await reviewAndConfirm(user)

    await waitFor(() => expect(lastConfirmBody).not.toBeNull())
    expect(lastConfirmBody!.atlOptionId).toBeNull()
  })

  // The country is server-normalised and has no input of its own — the portal is
  // a passthrough. This pins that, so client-side mangling can't creep back in.
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

    // Correctable: the editor reopens on a field the passenger could not otherwise
    // see, the message lands on it rather than only in the toast, and the gate
    // reopens so they can actually act on it.
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
    // Suburb and city are separate places, so they take a comma; the postcode
    // belongs to the city and follows it on a space, as on an envelope.
    expect(dialog.getByText('Suburb, Auckland 1010')).toBeInTheDocument()
    expect(dialog.getByText('NZ')).toBeInTheDocument()
    expect(dialog.getByText('Test Passenger')).toBeInTheDocument()
    expect(dialog.getByText('+64211234567')).toBeInTheDocument()
    expect(dialog.getByText('test@example.com')).toBeInTheDocument()
    expect(dialog.getByText('Safe Place')).toBeInTheDocument()
    expect(dialog.getByText(/behind the garden shed/i)).toBeInTheDocument()

    // Opening the read-back must not have sent anything.
    expect(lastConfirmBody).toBeNull()
  })

  it('spells out that nobody may leave the bag when Authority to Leave is off', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() => expect(screen.getByRole('switch')).toBeChecked())
    await user.click(screen.getByRole('switch'))
    await confirmAddress(user)
    await openReview(user)

    const dialog = within(await screen.findByRole('dialog'))
    expect(dialog.getByText('Not authorised')).toBeInTheDocument()
    expect(dialog.getByText(/someone will need to be there to take the bag/i)).toBeInTheDocument()
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
    // The form is still behind it with everything the passenger entered.
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

      // The window on the docket no longer exists — confirming it would book a run
      // that has departed.
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

    // Field errors and the address gate are behind the scrim — the dialog has to go
    // for the passenger to fix what failed.
    await waitFor(() => expect(reviewDialog()).not.toBeInTheDocument())
    expect(await screen.findByText(/120 characters or fewer/i)).toBeInTheDocument()
  })

  it('leads the hero with the task, with the airline as the identity line above it', async () => {
    renderForm()

    // The airline appears twice now — hero and footer sign-off. The hero is first.
    const [airline] = await screen.findAllByText('Test Air')
    const task = screen.getByRole('heading', { level: 1, name: /Confirm your baggage delivery/i })

    // The airline name led at display size before, which told the passenger whose
    // system this is but never what the page wanted from them. The task carries the
    // display type now; the airline sits above it as the identity line.
    expect(airline.compareDocumentPosition(task) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })

  it('sets the file reference as a baggage tag under the task', async () => {
    renderForm()

    // The one token the passenger will be read back over the phone, and the
    // page's answer to the question they arrived with — does anyone have my bag.
    const value = await screen.findByText('REF-42')
    const label = screen.getByText(/^file reference$/i)
    const task = screen.getByRole('heading', { level: 1, name: /Confirm your baggage delivery/i })

    expect(task.compareDocumentPosition(value) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(label.compareDocumentPosition(value) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })

  it('gives the delivery window more weight than the day it falls on', async () => {
    renderForm()

    // The day is context; the hours are what the passenger is choosing between.
    // Emphasis sat on the day, leaving the decisive value in the dimmed line.
    const hours = await screen.findByText('2:00 PM – 5:00 PM')
    const day = screen.getByText('Today, Wed 10 Jun')

    // The window card is the one decision on the page, so the hours carry the
    // page's heaviest body weight — not the same 600 as every section title.
    expect(hours).toHaveStyle({ fontWeight: '700' })
    expect(day).not.toHaveStyle({ fontWeight: '700' })
  })

  it('reports each section as complete or outstanding on its header chip', async () => {
    const user = userEvent.setup()
    renderForm()

    // The chip replaced a glyph that only restated the title in a picture. It now
    // carries state, so the column of chips is a progress read.
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

    // An empty box on an optional section reads as an unfinished task.
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

  // The ATL list renders its own radios, so every window assertion is scoped to the
  // window group rather than to the page.
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

    // Roving tab index: the group is one tab stop, not one per window. Before this
    // the rows carried `outline: none` and no focus rule, so the one decision on
    // the page could not be reached — let alone made — from a keyboard.
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
    // Focus follows selection, or the next arrow press comes from the old row.
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

  it('puts Confirm delivery ahead of the way back out of the read-back', async () => {
    const user = userEvent.setup()
    renderForm()

    await waitFor(() =>
      expect(screen.getByRole('button', { name: /review delivery/i })).toBeEnabled(),
    )
    await confirmAddress(user)
    await user.click(screen.getByRole('button', { name: /review delivery/i }))

    // "Edit details" was a bare subtle button above the primary, which read as a
    // stray link rather than the other half of the choice.
    const dialog = within(await screen.findByRole('dialog'))
    const confirm = dialog.getByRole('button', { name: /confirm delivery/i })
    const edit = dialog.getByRole('button', { name: /edit details/i })

    expect(confirm.compareDocumentPosition(edit) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })

  it('signs the confirm page off with the attribution alone', async () => {
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)

    // Nothing in the footer competes with the form: no carrier name repeating the
    // hero, and no phone number inviting the passenger to stop and call.
    const footer = within(screen.getByRole('contentinfo'))
    expect(footer.queryByText('Test Air')).not.toBeInTheDocument()
    expect(footer.queryByRole('link', { name: /call/i })).not.toBeInTheDocument()
    expect(footer.queryByText(/need help\?/i)).not.toBeInTheDocument()
    expect(footer.getByText(/powered by/i)).toBeInTheDocument()
  })

  it('names the bar button for what it does — review, not confirm', async () => {
    renderForm()

    // Tapping it only opens the read-back. Calling it "confirm" told the passenger
    // the booking was being made, so some never opened the docket they were meant
    // to check.
    const bar = await screen.findByRole('button', { name: /review delivery/i })
    expect(bar).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /review and confirm/i })).not.toBeInTheDocument()
    // "Confirm delivery" belongs to the dialog, which hasn't been opened.
    expect(screen.queryByRole('button', { name: /^confirm delivery$/i })).not.toBeInTheDocument()
  })

  it('shows the three delivery stages with Confirm current', async () => {
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)

    expect(screen.getByText('Confirm')).toBeInTheDocument()
    expect(screen.getByText('In transit')).toBeInTheDocument()
    expect(screen.getByText('Delivered')).toBeInTheDocument()

    // Lifecycle state, not a wizard: Confirm is the step in progress, and no step
    // is reachable by keyboard or click. Scoped to the stepper — other buttons on
    // the page also read as "Confirm …".
    const stepper = document.querySelector('.mantine-Stepper-root') as HTMLElement
    const steps = within(stepper).getAllByRole('button')
    expect(steps).toHaveLength(3)
    expect(steps[0]).toHaveAttribute('data-progress')
    steps.forEach((step) => expect(step).toHaveAttribute('tabindex', '-1'))
  })
})
