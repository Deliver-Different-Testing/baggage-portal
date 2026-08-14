import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { setupServer } from 'msw/node'
import { http, HttpResponse } from 'msw'
import { ConfirmedScreen, PaxMobile } from './PaxMobile'
import { MantineTestProvider } from '../test/render'
import { SLOT_HOLD_MS } from '../hooks/useSlotHoldCountdown'
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
        <ConfirmedScreen summary={summary} slot={slot} bookingId={bookingId} />
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

    expect(screen.getByText(/file reference · REF-42/i)).toBeInTheDocument()
    expect(screen.getByText('Delivery window')).toBeInTheDocument()
    expect(screen.getByText('Today, Wed 10 Jun')).toBeInTheDocument()
    expect(screen.getByText('2:00 PM – 5:00 PM')).toBeInTheDocument()
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

  it('signs off with the airline and a number to call', () => {
    renderConfirmed('token-abc-123')

    const footer = within(screen.getByRole('contentinfo'))
    expect(footer.getByText('Test Air')).toBeInTheDocument()
    expect(footer.getByRole('link', { name: /call 0800 267 5494/i })).toHaveAttribute(
      'href',
      'tel:08002675494',
    )
  })
})

describe('PaxMobile — Authority to Leave submit', () => {
  const atlOptions = [
    { id: 5, name: 'Front door' },
    { id: 6, name: 'Back door' },
    { id: 7, name: 'Safe Place' },
  ]

  let lastConfirmBody: ConfirmBookingRequest | null = null

  const server = setupServer(
    http.get('*/antiforgery/token', () => {
      document.cookie = 'XSRF-TOKEN=request-token-abc'
      return new HttpResponse(null, { status: 204 })
    }),
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
        HttpResponse.json({ ...summary, reference: 'AKLA2633476', atlOptions }),
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
        HttpResponse.json({ ...summary, reference: '', atlOptions }),
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

  it('warns that the slot is only held for ten minutes', async () => {
    renderForm()

    expect(
      await screen.findByText(/confirm within 10 minutes to secure your selected time slot/i),
    ).toBeInTheDocument()
  })

  it('refetches the windows and drops the selection when the hold lapses', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    try {
      let timeslotRequests = 0
      server.use(
        http.get('*/pax/:id/booking/timeslots', () => {
          timeslotRequests += 1
          // The second response drops the stale first window, so the refreshed
          // list can only be showing tomorrow if the refetch landed.
          return HttpResponse.json(timeslotRequests === 1 ? [slot] : [tomorrowSlot])
        }),
      )

      renderForm()

      await screen.findByText('Today, Wed 10 Jun')

      await act(async () => {
        vi.advanceTimersByTime(SLOT_HOLD_MS)
      })

      await waitFor(() => expect(timeslotRequests).toBe(2))
      expect(await screen.findByText('Tomorrow, Thu 11 Jun')).toBeInTheDocument()
      expect(screen.queryByText('Today, Wed 10 Jun')).not.toBeInTheDocument()
    } finally {
      vi.useRealTimers()
    }
  })

  // The address gate gets in the way of every submit on purpose. Tick it after any
  // address edits — ticking disables the inputs.
  async function confirmAddress(user: ReturnType<typeof userEvent.setup>) {
    await user.click(await screen.findByRole('checkbox', { name: /this address is correct/i }))
  }

  // The bar button only opens the read-back now; the dialog's own button sends.
  async function openReview(user: ReturnType<typeof userEvent.setup>) {
    await user.click(screen.getByRole('button', { name: /review and confirm/i }))
  }

  async function reviewAndConfirm(user: ReturnType<typeof userEvent.setup>) {
    await openReview(user)
    await user.click(await screen.findByRole('button', { name: /^confirm delivery$/i }))
  }

  function reviewDialog() {
    return screen.queryByRole('dialog', { name: /check your delivery details/i })
  }

  it('leaves every field editable on load with no edit buttons', async () => {
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)

    expect(screen.getByRole('textbox', { name: /full name/i })).toBeEnabled()
    expect(screen.getByRole('textbox', { name: /street address/i })).toBeEnabled()
    expect(screen.getByRole('textbox', { name: /country/i })).toBeEnabled()
    expect(screen.queryByRole('button', { name: /edit/i })).not.toBeInTheDocument()
  })

  it('blocks submit until the address is confirmed', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() => expect(screen.getByRole('button', { name: /review and confirm/i })).toBeEnabled())

    await openReview(user)

    expect(
      await screen.findByText(/please confirm your delivery address is correct/i),
    ).toBeInTheDocument()
    expect(reviewDialog()).not.toBeInTheDocument()
    expect(lastConfirmBody).toBeNull()
  })

  it('locks the address once confirmed and unlocks it again when unticked', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    const street = screen.getByRole('textbox', { name: /street address/i })
    // Scoped to the input: Autocomplete's options popup shares the label.
    expect(screen.getByLabelText(/search address/i, { selector: 'input' })).toBeInTheDocument()

    await confirmAddress(user)

    await waitFor(() => expect(street).toBeDisabled())
    expect(screen.getByRole('textbox', { name: /country/i })).toBeDisabled()
    // The search is a tool for changing the address — no reason to offer it once
    // the passenger has said the address is right.
    expect(
      screen.queryByLabelText(/search address/i, { selector: 'input' }),
    ).not.toBeInTheDocument()

    await user.click(screen.getByRole('checkbox', { name: /this address is correct/i }))

    await waitFor(() => expect(street).toBeEnabled())
  })

  it('posts the selected ATL option id as a number', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() => expect(screen.getByRole('switch')).toBeEnabled())

    // Turn ATL on (auto-selects the first option), then pick the second.
    await user.click(screen.getByRole('switch'))
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

    await user.click(screen.getByRole('switch'))
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

    await user.click(screen.getByRole('switch'))
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
    await waitFor(() => expect(screen.getByRole('button', { name: /review and confirm/i })).toBeEnabled())

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
    await waitFor(() => expect(screen.getByRole('button', { name: /review and confirm/i })).toBeEnabled())

    await confirmAddress(user)
    await reviewAndConfirm(user)

    expect(await screen.findByText(/120 characters or fewer/i)).toBeInTheDocument()
    expect(screen.queryByText(/could not submit your confirmation/i)).not.toBeInTheDocument()
  })

  it('posts null when Authority to Leave stays off', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() => expect(screen.getByRole('button', { name: /review and confirm/i })).toBeEnabled())

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
    await waitFor(() => expect(screen.getByRole('button', { name: /review and confirm/i })).toBeEnabled())

    await confirmAddress(user)
    await reviewAndConfirm(user)

    await waitFor(() => expect(lastConfirmBody).not.toBeNull())
    expect(lastConfirmBody!.address.country).toBe('NZ')
  })

  it('blocks submit and prompts for a country when the stored one is unresolved', async () => {
    server.use(
      http.get('*/pax/:id/booking', () =>
        HttpResponse.json({
          ...summary,
          atlOptions,
          deliveryAddress: { ...summary.deliveryAddress, country: '' },
        }),
      ),
    )

    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() => expect(screen.getByRole('button', { name: /review and confirm/i })).toBeEnabled())

    expect(screen.getByRole('textbox', { name: /country/i })).toBeEnabled()

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
    await waitFor(() => expect(screen.getByRole('button', { name: /review and confirm/i })).toBeEnabled())

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
    await waitFor(() => expect(screen.getByRole('button', { name: /review and confirm/i })).toBeEnabled())

    await confirmAddress(user)
    await reviewAndConfirm(user)

    // Correctable: the message lands on the country field itself, not only in the
    // toast, and the gate reopens so the passenger can actually act on it.
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
      expect(screen.getByRole('button', { name: /review and confirm/i })).toBeEnabled(),
    )
    await user.click(screen.getByRole('switch'))
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
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /review and confirm/i })).toBeEnabled(),
    )
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
      expect(screen.getByRole('button', { name: /review and confirm/i })).toBeEnabled(),
    )
    await confirmAddress(user)
    await openReview(user)

    await user.click(await screen.findByRole('button', { name: /edit details/i }))

    await waitFor(() => expect(reviewDialog()).not.toBeInTheDocument())
    expect(lastConfirmBody).toBeNull()
    // The form is still behind it with everything the passenger entered.
    expect(screen.getByRole('checkbox', { name: /this address is correct/i })).toBeChecked()
  })

  it('drops the read-back when the ten-minute hold lapses, without sending', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    try {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
      renderForm()

      await screen.findByText(/confirm your baggage delivery/i)
      await waitFor(() =>
        expect(screen.getByRole('button', { name: /review and confirm/i })).toBeEnabled(),
      )
      await confirmAddress(user)
      await openReview(user)
      expect(await screen.findByRole('dialog')).toBeInTheDocument()

      await act(async () => {
        vi.advanceTimersByTime(SLOT_HOLD_MS)
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
      expect(screen.getByRole('button', { name: /review and confirm/i })).toBeEnabled(),
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

    expect(hours).toHaveStyle({ fontWeight: '600' })
    expect(day).not.toHaveStyle({ fontWeight: '600' })
  })

  it('puts Confirm delivery ahead of the way back out of the read-back', async () => {
    const user = userEvent.setup()
    renderForm()

    await waitFor(() =>
      expect(screen.getByRole('button', { name: /review and confirm/i })).toBeEnabled(),
    )
    await confirmAddress(user)
    await user.click(screen.getByRole('button', { name: /review and confirm/i }))

    // "Edit details" was a bare subtle button above the primary, which read as a
    // stray link rather than the other half of the choice.
    const dialog = within(await screen.findByRole('dialog'))
    const confirm = dialog.getByRole('button', { name: /confirm delivery/i })
    const edit = dialog.getByRole('button', { name: /edit details/i })

    expect(confirm.compareDocumentPosition(edit) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
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
