import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { setupServer } from 'msw/node'
import { http, HttpResponse } from 'msw'
import { AddressAutocomplete } from './AddressAutocomplete'
import { MantineTestProvider } from '../test/render'
import type { AddressDetail, AddressSearchResult } from '../types/address'

const suggestions: AddressSearchResult[] = [
  {
    id: 'paf-1',
    title: '123 Queen Street, Auckland Central, Auckland',
    street: '123 Queen Street',
    suburb: 'Auckland Central',
    city: 'Auckland',
    state: 'Auckland',
    postalCode: '1010',
    countryCode: 'NZ',
  },
  {
    id: 'paf-2',
    title: '123 Queen Street, Onehunga, Auckland',
    street: '123 Queen Street',
    suburb: 'Onehunga',
    city: 'Auckland',
    state: 'Auckland',
    postalCode: '1061',
    countryCode: 'NZ',
  },
]

const detail: AddressDetail = {
  street: '123 Queen Street',
  suburb: 'Onehunga',
  city: 'Auckland',
  state: 'Auckland',
  stateCode: 'AUK',
  postalCode: '1061',
  countryCode: 'NZ',
}

const server = setupServer(
  http.get('*/pax/:id/address/autocomplete', () => HttpResponse.json(suggestions)),
  http.get('*/pax/:id/address/lookup/:addressId', () => HttpResponse.json(detail)),
)

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
afterEach(() => server.resetHandlers())
afterAll(() => server.close())

function renderAutocomplete(onAddressSelect = vi.fn()) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <MantineTestProvider>
      <QueryClientProvider client={queryClient}>
        <AddressAutocomplete bookingId="token-xyz" onAddressSelect={onAddressSelect} />
      </QueryClientProvider>
    </MantineTestProvider>,
  )
  return { onAddressSelect }
}

describe('AddressAutocomplete', () => {
  it('offers every suggestion the server ranked, in order', async () => {
    const user = userEvent.setup()
    renderAutocomplete()

    await user.type(screen.getByPlaceholderText(/start typing an address/i), '123 Queen')

    const options = await screen.findAllByRole('option')
    expect(options.map((o) => o.textContent)).toEqual(suggestions.map((s) => s.title))
  })

  it('does not search until the input is long enough to be an address', async () => {
    const user = userEvent.setup()
    let searches = 0
    server.use(
      http.get('*/pax/:id/address/autocomplete', () => {
        searches += 1
        return HttpResponse.json(suggestions)
      }),
    )

    renderAutocomplete()
    await user.type(screen.getByPlaceholderText(/start typing an address/i), '12')

    await waitFor(() => expect(searches).toBe(0))
  })

  it('reports the looked-up detail and shows the full address it resolved to', async () => {
    const user = userEvent.setup()
    const { onAddressSelect } = renderAutocomplete()

    const input = screen.getByPlaceholderText(/start typing an address/i)
    await user.type(input, '123 Queen')

    const options = await screen.findAllByRole('option')
    await user.click(options[1])

    await waitFor(() => expect(onAddressSelect).toHaveBeenCalledWith(detail))
    expect(input).toHaveValue('123 Queen Street, Onehunga, Auckland, AUK, 1061')
  })
})
