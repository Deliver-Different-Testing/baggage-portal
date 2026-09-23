import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it } from 'vitest'
import { setupServer } from 'msw/node'
import { http, HttpResponse } from 'msw'
import { queryClient } from './queryClient'
import { prefetchRouteData } from './prefetch'

const booking = { bookingId: 1, jobId: 42, fileReference: 'AKLNZ12345' }
const confirmedBooking = {
  ...booking,
  confirmation: {
    confirmedAtUtc: '2026-06-09T21:00:00Z',
    dayLabel: 'Today, Wed 10 Jun',
    windowLabel: '2:00 PM - 5:00 PM',
  },
}
const timeslots = [{ id: 'slot-1', runUtc: '2026-06-10T02:00:00Z' }]

let tokenRequests = 0
let timeslotRequests = 0

const server = setupServer(
  http.get('*/pax/:id/booking', () => HttpResponse.json(booking)),
  http.get('*/pax/:id/booking/timeslots', () => {
    timeslotRequests += 1
    return HttpResponse.json(timeslots)
  }),
  http.get('*/antiforgery/token', () => {
    tokenRequests += 1
    return new HttpResponse(null, { status: 204 })
  }),
)

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
beforeEach(() => {
  queryClient.clear()
  tokenRequests = 0
  timeslotRequests = 0
  document.cookie = 'XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT'
})
afterEach(() => server.resetHandlers())
afterAll(() => server.close())

describe('prefetchRouteData', () => {
  it('primes the booking and its windows for a confirm URL', async () => {
    await prefetchRouteData('/c/token-xyz')

    expect(queryClient.getQueryData(['pax', 'booking', 'token-xyz'])).toEqual(booking)
    expect(queryClient.getQueryData(['pax', 'timeslots', 'token-xyz'])).toEqual(timeslots)
  })

  it('decodes the token so the key matches the one useParams produces', async () => {
    await prefetchRouteData('/c/a%2Bb')

    expect(queryClient.getQueryData(['pax', 'booking', 'a+b'])).toEqual(booking)
  })

  it.each(['/', '/expired', '/internal/process-map', '/c/', '/nope/token', '/t/token-xyz'])(
    'fetches nothing for %s',
    async (path) => {
      await prefetchRouteData(path)

      expect(queryClient.getQueryCache().getAll()).toHaveLength(0)
    },
  )

  it('leaves the primed booking fresh so the page does not refetch it', async () => {
    await prefetchRouteData('/c/token-xyz')

    expect(
      queryClient.getQueryCache().find({ queryKey: ['pax', 'booking', 'token-xyz'] })?.isStale(),
    ).toBe(false)
  })

  it('warms the antiforgery token alongside the booking', async () => {
    await prefetchRouteData('/c/token-xyz')

    expect(tokenRequests).toBe(1)
  })

  it('skips the delivery windows for a booking that is already confirmed', async () => {
    server.use(http.get('*/pax/:id/booking', () => HttpResponse.json(confirmedBooking)))

    await prefetchRouteData('/c/token-xyz')

    expect(queryClient.getQueryData(['pax', 'booking', 'token-xyz'])).toEqual(confirmedBooking)
    expect(timeslotRequests).toBe(0)
  })

  it('primes the delivery windows for a booking that still needs confirming', async () => {
    await prefetchRouteData('/c/token-xyz')

    expect(timeslotRequests).toBe(1)
  })

  it('skips the delivery windows when the booking itself fails', async () => {
    server.use(http.get('*/pax/:id/booking', () => new HttpResponse(null, { status: 500 })))

    await expect(prefetchRouteData('/c/token-xyz')).resolves.toBeUndefined()
    expect(timeslotRequests).toBe(0)
  })

  it('never rejects when the token is already dead', async () => {
    server.use(http.get('*/pax/:id/booking', () => new HttpResponse(null, { status: 404 })))

    await expect(prefetchRouteData('/c/token-xyz')).resolves.toBeUndefined()
    expect(queryClient.getQueryData(['pax', 'booking', 'token-xyz'])).toBeUndefined()
  })
})
