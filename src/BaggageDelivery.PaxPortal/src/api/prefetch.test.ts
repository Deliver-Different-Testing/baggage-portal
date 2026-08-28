import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it } from 'vitest'
import { setupServer } from 'msw/node'
import { http, HttpResponse } from 'msw'
import { queryClient } from './queryClient'
import { prefetchRouteData } from './prefetch'

const booking = { bookingId: 1, jobId: 42, fileReference: 'AKLNZ12345' }
const timeslots = [{ id: 'slot-1', runUtc: '2026-06-10T02:00:00Z' }]
const tracking = { jobId: 42, currentStatus: 'OutForDelivery', events: [] }

const server = setupServer(
  http.get('*/pax/:id/booking', () => HttpResponse.json(booking)),
  http.get('*/pax/:id/booking/timeslots', () => HttpResponse.json(timeslots)),
  http.get('*/pax/:id/tracking', () => HttpResponse.json(tracking)),
)

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
beforeEach(() => queryClient.clear())
afterEach(() => server.resetHandlers())
afterAll(() => server.close())

describe('prefetchRouteData', () => {
  it('primes the booking and its windows for a confirm URL', async () => {
    await prefetchRouteData('/c/token-xyz')

    expect(queryClient.getQueryData(['pax', 'booking', 'token-xyz'])).toEqual(booking)
    expect(queryClient.getQueryData(['pax', 'timeslots', 'token-xyz'])).toEqual(timeslots)
  })

  it('primes only the timeline for a tracking URL', async () => {
    await prefetchRouteData('/t/token-xyz')

    expect(queryClient.getQueryData(['pax', 'tracking', 'token-xyz'])).toEqual(tracking)
    expect(queryClient.getQueryData(['pax', 'booking', 'token-xyz'])).toBeUndefined()
  })

  it('decodes the token so the key matches the one useParams produces', async () => {
    await prefetchRouteData('/c/a%2Bb')

    expect(queryClient.getQueryData(['pax', 'booking', 'a+b'])).toEqual(booking)
  })

  it.each(['/', '/expired', '/internal/process-map', '/c/', '/nope/token'])(
    'fetches nothing for %s',
    async (path) => {
      await prefetchRouteData(path)

      expect(queryClient.getQueryCache().getAll()).toHaveLength(0)
    },
  )

  it('never rejects when the token is already dead', async () => {
    server.use(http.get('*/pax/:id/booking', () => new HttpResponse(null, { status: 404 })))

    await expect(prefetchRouteData('/c/token-xyz')).resolves.toBeUndefined()
    expect(queryClient.getQueryData(['pax', 'booking', 'token-xyz'])).toBeUndefined()
  })
})
