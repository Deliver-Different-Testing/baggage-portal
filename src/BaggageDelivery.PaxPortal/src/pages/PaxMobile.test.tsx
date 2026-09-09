import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { setupServer } from 'msw/node'
import { http, HttpResponse } from 'msw'
import { PaxMobile } from './PaxMobile'
import { MantineTestProvider } from '../test/render'
import { economyRun, slot, summary, tomorrowSlot } from '../test/paxFixtures'
import type { BookingSummary, ConfirmBookingRequest } from '../api/client'


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
    http.post('*/pax/:id/booking/services', () =>
      HttpResponse.json({ services: [economyRun], noServiceAvailable: false }),
    ),
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

    await screen.findByRole('heading', { level: 2, name: /delivery window/i })
    expect(screen.getByRole('heading', { level: 2, name: /deliver to/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 2, name: /your details/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 2, name: /authority to leave/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: /confirm your baggage delivery/i })).toBeInTheDocument()
  })

  it('gathers what is outstanding into a summary, and takes focus to it', async () => {
    server.use(
      http.get('*/pax/:id/booking', () =>
        HttpResponse.json(
          booking({
            passengerName: '',
            deliveryAddress: { ...summary.deliveryAddress, line5: '' },
          }),
        ),
      ),
    )

    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()

    await openReview(user)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(/check these before confirming/i)
    expect(alert).toHaveFocus()
    expect(reviewDialog()).not.toBeInTheDocument()

    const listed = within(alert)
      .getAllByRole('link')
      .map((link) => link.textContent)
    expect(listed).toEqual(['Please enter your full name.', 'Please enter your suburb.'])
  })

  it('sends the passenger to the field a summary entry names', async () => {
    server.use(
      http.get('*/pax/:id/booking', () =>
        HttpResponse.json(booking({ passengerName: '' })),
      ),
    )

    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await openReview(user)

    const alert = await screen.findByRole('alert')
    await user.click(within(alert).getByRole('link', { name: /please enter your full name/i }))

    expect(screen.getByRole('textbox', { name: /full name/i })).toHaveFocus()
  })

  it('drops the summary once everything outstanding is filled in', async () => {
    server.use(
      http.get('*/pax/:id/booking', () =>
        HttpResponse.json(booking({ passengerName: '' })),
      ),
    )

    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await openReview(user)
    await screen.findByRole('alert')

    await user.type(screen.getByRole('textbox', { name: /full name/i }), 'Test Passenger')

    await waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument())
  })

  it('gives the passenger the job number to quote, not the worldtracer reference', async () => {
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)

    const help = screen.getByText(/any questions/i)
    expect(help).toHaveTextContent('quote tracking number URG-179252')
    expect(help).not.toHaveTextContent('AKLNZ12345')
    expect(within(help).getByRole('link', { name: '0800 267 5494' })).toHaveAttribute(
      'href',
      'tel:08002675494',
    )
  })

  it('leads with the passenger, then the address, the window and authority to leave', async () => {
    renderForm()

    const details = await screen.findByRole('heading', { level: 2, name: /your details/i })
    const window = screen.getByRole('heading', { level: 2, name: /delivery window/i })
    const deliverTo = screen.getByRole('heading', { level: 2, name: /deliver to/i })
    const atl = screen.getByRole('heading', { level: 2, name: /authority to leave/i })

    expect(details.compareDocumentPosition(deliverTo)).toBe(Node.DOCUMENT_POSITION_FOLLOWING)
    expect(deliverTo.compareDocumentPosition(window)).toBe(Node.DOCUMENT_POSITION_FOLLOWING)
    expect(window.compareDocumentPosition(atl)).toBe(Node.DOCUMENT_POSITION_FOLLOWING)
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

  it('leaves out a window whose run has already started', async () => {
    const started = { ...slot, id: 'slot-gone', runUtc: '2026-06-10T00:30:00Z' }
    server.use(
      http.get('*/pax/:id/booking/timeslots', () =>
        HttpResponse.json([started, slot, tomorrowSlot]),
      ),
    )

    renderForm()

    await screen.findByRole('radiogroup', { name: /delivery window/i })
    const options = screen.getAllByRole('radio')

    expect(options).toHaveLength(2)
    expect(options[0]).toHaveTextContent('2:00 PM – 5:00 PM')
    expect(options[0]).toHaveAttribute('aria-checked', 'true')
  })

  it('says there are no windows when every one of them has already started', async () => {
    server.use(
      http.get('*/pax/:id/booking/timeslots', () =>
        HttpResponse.json([{ ...slot, runUtc: '2026-06-10T00:30:00Z' }]),
      ),
    )

    renderForm()

    const message = await screen.findByRole('status')
    expect(message).toHaveTextContent(/no delivery windows available for this booking yet/i)
    expect(screen.queryByRole('radio')).not.toBeInTheDocument()
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

    const message = await screen.findByRole('status')
    expect(message).toHaveTextContent(/no delivery windows available for this booking yet/i)
    expect(message).toHaveTextContent('0800 267 5494')
  })

  it('names what the passenger loses if the chosen run departs', async () => {
    renderForm()

    expect(await screen.findByText(/time left to keep this window/i)).toBeInTheDocument()
  })

  it('escalates the run-start notice inside the last ten minutes', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    try {
      vi.setSystemTime(new Date('2026-06-10T01:20:00Z'))
      server.use(http.get('*/pax/:id/booking/timeslots', () => HttpResponse.json([slot])))

      renderForm()

      expect(await screen.findByText('40:00')).toBeInTheDocument()
      expect(screen.getByRole('timer')).not.toHaveAttribute('data-urgent')

      await act(async () => {
        vi.advanceTimersByTime(1_860_000)
      })

      expect(await screen.findByText('9:00')).toBeInTheDocument()
      expect(screen.getByRole('timer')).toHaveAttribute('data-urgent')
    } finally {
      vi.useRealTimers()
    }
  })

  it('counts down to the start of the selected run, and retargets when a later one is picked', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    try {
      vi.setSystemTime(new Date('2026-06-10T01:20:00Z'))
      server.use(
        http.get('*/pax/:id/booking/timeslots', () =>
          HttpResponse.json([slot, tomorrowSlot]),
        ),
      )

      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
      renderForm()

      expect(await screen.findByText('40:00')).toBeInTheDocument()

      await act(async () => {
        vi.advanceTimersByTime(5_000)
      })
      expect(screen.getByText('39:55')).toBeInTheDocument()

      await user.click(screen.getByText('Tomorrow, Thu 11 Jun'))

      expect(await screen.findByText(/^19:\d{2}:\d{2}$/)).toBeInTheDocument()
    } finally {
      vi.useRealTimers()
    }
  })

  it('refetches the windows and drops the selection once the run has departed', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    try {
      vi.setSystemTime(new Date('2026-06-10T01:20:00Z'))
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
        vi.advanceTimersByTime(2_460_000)
      })

      await waitFor(() => expect(timeslotRequests).toBe(2))
      expect(await screen.findByText('Tomorrow, Thu 11 Jun')).toBeInTheDocument()
      expect(screen.queryByText('Today, Wed 10 Jun')).not.toBeInTheDocument()
    } finally {
      vi.useRealTimers()
    }
  })

  async function editAddress(user: ReturnType<typeof userEvent.setup>) {
    await user.click(screen.getByRole('button', { name: /^edit$/i }))
    await screen.findByRole('textbox', { name: /street name/i })
  }

  function addressTick() {
    return screen.getByRole('checkbox', { name: /this address is correct/i })
  }

  async function tickAddress(user: ReturnType<typeof userEvent.setup>) {
    if (!(addressTick() as HTMLInputElement).checked) await user.click(addressTick())
  }

  async function openReview(user: ReturnType<typeof userEvent.setup>) {
    await tickAddress(user)
    await user.click(screen.getByRole('button', { name: /review delivery/i }))
  }

  async function chooseOfferedService(user: ReturnType<typeof userEvent.setup>) {
    const group = await screen.findByRole('radiogroup', { name: /delivery service/i })
    await user.click(await within(group).findByRole('radio', { name: /economy run/i }))
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

  it('opens on the saved address as a block to tick off, not editable fields', async () => {
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)

    expect(await screen.findByText('123 Test St')).toBeInTheDocument()
    expect(screen.getByText('Suburb, Auckland 1010')).toBeInTheDocument()
    expect(addressTick()).not.toBeChecked()
    expect(screen.getByRole('button', { name: /^edit$/i })).toBeInTheDocument()

    expect(screen.queryByRole('textbox', { name: /street name/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: /search address/i })).not.toBeInTheDocument()
  })

  it('reveals the whole address form once the passenger presses Edit', async () => {
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

  it('closes the editor and unticks when the address is edited after being confirmed', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await tickAddress(user)
    expect(addressTick()).toBeChecked()

    await editAddress(user)
    expect(addressTick()).not.toBeChecked()

    await user.click(screen.getByRole('button', { name: /^done$/i }))
    await waitFor(() =>
      expect(screen.queryByRole('textbox', { name: /street name/i })).not.toBeInTheDocument(),
    )
  })

  it('blocks the review until the address is ticked as correct', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await user.click(screen.getByRole('button', { name: /review delivery/i }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(/confirm your delivery address is correct/i)
    expect(reviewDialog()).not.toBeInTheDocument()
  })

  it('reopens the editor on the missing street number, rather than hiding the error', async () => {
    server.use(
      http.get('*/pax/:id/booking', () =>
        HttpResponse.json(
          booking({ deliveryAddress: { ...summary.deliveryAddress, line3: '' } }),
        ),
      ),
    )

    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await openReview(user)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(/please enter your street number/i)
    expect(reviewDialog()).not.toBeInTheDocument()

    const streetNumber = await screen.findByRole('textbox', { name: /street number/i })
    expect(streetNumber).toBeInTheDocument()
    expect(streetNumber).toHaveAccessibleDescription(/please enter your street number/i)
  })

  it('clears a field error as the passenger fixes it', async () => {
    server.use(
      http.get('*/pax/:id/booking', () =>
        HttpResponse.json(
          booking({
            passengerName: '',
            deliveryAddress: { ...summary.deliveryAddress, line3: '' },
          }),
        ),
      ),
    )

    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await openReview(user)

    const name = await screen.findByRole('textbox', { name: /full name/i })
    const streetNumber = await screen.findByRole('textbox', { name: /street number/i })
    expect(name).toHaveAccessibleDescription(/please enter your full name/i)
    expect(streetNumber).toHaveAccessibleDescription(/please enter your street number/i)

    await user.type(name, 'Test Passenger')
    expect(name).not.toHaveAccessibleDescription(/please enter your full name/i)
    expect(streetNumber).toHaveAccessibleDescription(/please enter your street number/i)

    await user.type(streetNumber, '123')
    expect(streetNumber).not.toHaveAccessibleDescription(/please enter your street number/i)
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
    await chooseOfferedService(user)
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
    await reviewAndConfirm(user)

    await waitFor(() => expect(lastConfirmBody).not.toBeNull())
    expect(lastConfirmBody!.address.latitude).toBe(-36.8485)
    expect(lastConfirmBody!.address.longitude).toBe(174.7633)
  })

  it('posts each address line under its own key', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
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

  it('routes the passenger to the airline when no service covers the new address', async () => {
    server.use(
      http.post('*/pax/:id/booking/services', () =>
        HttpResponse.json({ services: [], noServiceAvailable: true }),
      ),
      http.post('*/pax/:id/booking/address-help', () => HttpResponse.json({ requested: true })),
    )

    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await editAddress(user)
    await user.clear(screen.getByRole('textbox', { name: /suburb/i }))
    await user.type(screen.getByRole('textbox', { name: /suburb/i }), 'Haast')

    await screen.findByText(/we cannot deliver there/i)
    expect(screen.queryByRole('radiogroup', { name: /delivery window/i })).not.toBeInTheDocument()

    await user.click(await screen.findByRole('button', { name: /ask test air to contact me/i }))

    expect(await screen.findByText(/we've asked test air to contact you/i)).toBeInTheDocument()
    expect(lastConfirmBody).toBeNull()
  })

  it('does not check availability when the address is left alone', async () => {
    let servicesCalls = 0
    server.use(
      http.post('*/pax/:id/booking/services', () => {
        servicesCalls += 1
        return HttpResponse.json({ services: [economyRun], noServiceAvailable: false })
      }),
    )

    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await reviewAndConfirm(user)

    await waitFor(() => expect(lastConfirmBody).not.toBeNull())
    expect(servicesCalls).toBe(0)
    expect(lastConfirmBody!.serviceJobTypeId).toBeNull()
  })

  it('sends the chosen service with the confirmation', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await editAddress(user)
    await user.clear(screen.getByRole('textbox', { name: /street name/i }))
    await user.type(screen.getByRole('textbox', { name: /street name/i }), 'Other St')
    await chooseOfferedService(user)
    await reviewAndConfirm(user)

    await waitFor(() => expect(lastConfirmBody).not.toBeNull())
    expect(lastConfirmBody!.serviceJobTypeId).toBe(37)
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

    await chooseOfferedService(user)
    await reviewAndConfirm(user)

    await waitFor(() => expect(lastConfirmBody).not.toBeNull())
    expect(lastConfirmBody!.address.line2).toBe('Apartment 4B, ring the buzzer')
  })

  it('shows the saved extra delivery information in its own field', async () => {
    server.use(
      http.get('*/pax/:id/booking', () =>
        HttpResponse.json(
          booking({
            deliveryAddress: { ...summary.deliveryAddress, line2: 'Gate code 1234' },
          }),
        ),
      ),
    )

    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    expect(screen.getByText('Gate code 1234')).toBeInTheDocument()

    await editAddress(user)
    expect(screen.getByRole('textbox', { name: /apartment, unit or suite/i })).toHaveValue(
      'Gate code 1234',
    )
  })

  it('leaves the extra-information field empty when the booking has none', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await editAddress(user)
    expect(screen.getByRole('textbox', { name: /apartment, unit or suite/i })).toHaveValue('')
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

    await openReview(user)

    expect((await screen.findAllByText(/please describe the safe place/i)).length).toBeGreaterThan(0)
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

    await openReview(user)

    expect((await screen.findAllByText(/please pick a delivery window/i)).length).toBeGreaterThan(0)
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

    await reviewAndConfirm(user)

    await waitFor(() => expect(lastConfirmBody).not.toBeNull())
    expect(lastConfirmBody!.atlOptionId).toBeNull()
  })

  it('posts the country from the booking summary unchanged', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() => expect(screen.getByRole('button', { name: /review delivery/i })).toBeEnabled())

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

    await openReview(user)

    expect((await screen.findAllByText(/please enter your country/i)).length).toBeGreaterThan(0)
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

    await chooseOfferedService(user)
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

    await reviewAndConfirm(user)

    const country = await screen.findByRole('textbox', { name: /country/i })
    await waitFor(() => expect(country).toHaveAttribute('aria-invalid', 'true'))
    expect(
      screen.getAllByText(/couldn't recognise the country on your delivery address/i).length,
    ).toBeGreaterThan(0)
    await waitFor(() => expect(country).toBeEnabled())
  })

  it('names the window, the address and the number the driver calls before sending it', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /review delivery/i })).toBeEnabled(),
    )

    await openReview(user)

    const dialog = within(await screen.findByRole('dialog'))
    expect(dialog.getByText('2:00 PM – 5:00 PM')).toBeInTheDocument()
    expect(dialog.getByText('Today, Wed 10 Jun')).toBeInTheDocument()
    expect(dialog.getByText('123 Test St')).toBeInTheDocument()
    expect(dialog.getByText('Suburb, Auckland 1010')).toBeInTheDocument()
    expect(dialog.getByText('+64211234567')).toBeInTheDocument()

    expect(lastConfirmBody).toBeNull()
  })

  it('leaves the full docket on the page rather than repeating it in the dialog', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /review delivery/i })).toBeEnabled(),
    )

    expect(screen.getByRole('textbox', { name: /full name/i })).toHaveValue('Test Passenger')
    expect(screen.getByRole('textbox', { name: /email address/i })).toHaveValue('test@example.com')

    await openReview(user)

    const dialog = within(await screen.findByRole('dialog'))
    expect(dialog.queryByText('test@example.com')).not.toBeInTheDocument()
    expect(dialog.queryByText(/file reference/i)).not.toBeInTheDocument()
  })

  it('sends nothing when the passenger goes back to edit', async () => {
    const user = userEvent.setup()
    renderForm()

    await screen.findByText(/confirm your baggage delivery/i)
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /review delivery/i })).toBeEnabled(),
    )
    await openReview(user)

    await user.click(await screen.findByRole('button', { name: /edit details/i }))

    await waitFor(() => expect(reviewDialog()).not.toBeInTheDocument())
    expect(lastConfirmBody).toBeNull()
    expect(screen.getByText('123 Test St')).toBeInTheDocument()
  })

  it('drops the read-back when the chosen run departs, without sending', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    try {
      vi.setSystemTime(new Date('2026-06-10T01:20:00Z'))
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
      renderForm()

      await screen.findByText(/confirm your baggage delivery/i)
      await waitFor(() =>
        expect(screen.getByRole('button', { name: /review delivery/i })).toBeEnabled(),
      )
      await openReview(user)
      expect(await screen.findByRole('dialog')).toBeInTheDocument()

      await act(async () => {
        vi.advanceTimersByTime(2_460_000)
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
    await openReview(user)

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
    await openReview(user)

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

  it('sends the passenger to the tracking app, not a page inside this one', async () => {
    renderPage()

    await screen.findByRole('heading', { level: 1, name: /you're all set/i })

    expect(screen.getByRole('link', { name: /track your delivery/i })).toHaveAttribute(
      'href',
      'https://tracking.example.com/#/ENCRYPTED',
    )
  })
})
