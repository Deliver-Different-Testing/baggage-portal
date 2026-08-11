import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it } from 'vitest'
import { setupServer } from 'msw/node'
import { http, HttpResponse } from 'msw'
import { apiClient } from './client'

describe('apiClient antiforgery handling', () => {
  let tokenRequests = 0
  let sentTokenHeaders: (string | null)[] = []

  const server = setupServer(
    http.get('*/antiforgery/token', () => {
      tokenRequests += 1
      // jsdom won't apply Set-Cookie from msw, so seed the cookie directly —
      // the browser equivalent of the API's readable companion cookie.
      document.cookie = 'XSRF-TOKEN=request-token-abc'
      return new HttpResponse(null, { status: 204 })
    }),
    http.post('*/pax/:id/booking/confirm', ({ request }) => {
      sentTokenHeaders.push(request.headers.get('X-XSRF-TOKEN'))
      return HttpResponse.json({ status: 'Released', releasedAtUtc: '2026-06-10T00:00:00Z' })
    }),
    http.get('*/pax/:id/booking', () => HttpResponse.json({ jobId: 1 })),
  )

  beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
  beforeEach(() => {
    tokenRequests = 0
    sentTokenHeaders = []
    document.cookie = 'XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT'
  })
  afterEach(() => server.resetHandlers())
  afterAll(() => server.close())

  it('fetches a request token before a mutation when no cookie is present', async () => {
    await apiClient.post('/pax/abc/booking/confirm', {})

    expect(tokenRequests).toBe(1)
    expect(sentTokenHeaders).toEqual(['request-token-abc'])
  })

  it('does not fetch a token for read-only requests', async () => {
    await apiClient.get('/pax/abc/booking')

    expect(tokenRequests).toBe(0)
  })

  it('reuses an existing cookie instead of re-fetching', async () => {
    document.cookie = 'XSRF-TOKEN=already-here'

    await apiClient.post('/pax/abc/booking/confirm', {})

    expect(tokenRequests).toBe(0)
    expect(sentTokenHeaders).toEqual(['already-here'])
  })

  it('issues a single token fetch for concurrent mutations', async () => {
    await Promise.all([
      apiClient.post('/pax/abc/booking/confirm', {}),
      apiClient.post('/pax/abc/booking/confirm', {}),
      apiClient.post('/pax/abc/booking/confirm', {}),
    ])

    expect(tokenRequests).toBe(1)
    expect(sentTokenHeaders).toEqual([
      'request-token-abc',
      'request-token-abc',
      'request-token-abc',
    ])
  })

  it('re-mints the token and retries once when the server rejects a stale token', async () => {
    document.cookie = 'XSRF-TOKEN=stale-token'
    let attempts = 0

    server.use(
      http.post('*/pax/:id/booking/confirm', ({ request }) => {
        attempts += 1
        sentTokenHeaders.push(request.headers.get('X-XSRF-TOKEN'))
        // Antiforgery rejection is a bare 400 with no body.
        return attempts === 1
          ? new HttpResponse(null, { status: 400 })
          : HttpResponse.json({ status: 'Released', releasedAtUtc: '2026-06-10T00:00:00Z' })
      }),
    )

    const response = await apiClient.post('/pax/abc/booking/confirm', {})

    expect(response.status).toBe(200)
    expect(tokenRequests).toBe(1)
    expect(sentTokenHeaders).toEqual(['stale-token', 'request-token-abc'])
  })

  it('surfaces a validation 400 without retrying', async () => {
    server.use(
      http.post('*/pax/:id/booking/confirm', () =>
        HttpResponse.json({ errors: { passengerName: ['Required'] } }, { status: 400 }),
      ),
    )

    await expect(apiClient.post('/pax/abc/booking/confirm', {})).rejects.toMatchObject({
      response: { status: 400 },
    })
    expect(sentTokenHeaders).toEqual([])
  })
})
