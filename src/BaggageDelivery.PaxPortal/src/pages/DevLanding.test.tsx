import { afterAll, afterEach, beforeAll, describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { setupServer } from 'msw/node'
import { http, HttpResponse } from 'msw'
import { DevLanding } from './DevLanding'
import { MantineTestProvider } from '../test/render'
import type { DevLinks } from '../api/client'

const links: DevLinks = {
  jobId: 67,
  token: 'token-abc-123',
  confirmUrl: 'http://baggagedelivery.local.deliverdifferent.com:5173/c/token-abc-123',
  trackUrl: 'http://baggagedelivery.local.deliverdifferent.com:5173/t/token-abc-123',
}

describe('DevLanding', () => {
  const server = setupServer(http.get('*/dev/links', () => HttpResponse.json(links)))

  beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
  afterEach(() => server.resetHandlers())
  afterAll(() => server.close())

  function renderLanding() {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    return render(
      <MemoryRouter initialEntries={['/']}>
        <MantineTestProvider>
          <QueryClientProvider client={queryClient}>
            <Routes>
              <Route path="/" element={<DevLanding />} />
              <Route path="/expired" element={<div>expired screen</div>} />
            </Routes>
          </QueryClientProvider>
        </MantineTestProvider>
      </MemoryRouter>,
    )
  }

  it('links to the SPA paths for the minted token, not the absolute dev-server URLs', async () => {
    renderLanding()

    const [confirm, track] = await screen.findAllByRole('link', { name: /open/i })

    expect(confirm).toHaveAttribute('href', '/c/token-abc-123')
    expect(track).toHaveAttribute('href', '/t/token-abc-123')
  })

  it('shows the job id and the full magic-link URLs to copy', async () => {
    renderLanding()

    expect(await screen.findByText(/job 67/i)).toBeInTheDocument()
    expect(screen.getByText(links.confirmUrl)).toBeInTheDocument()
    expect(screen.getByText(links.trackUrl)).toBeInTheDocument()
  })

  it('redirects to /expired when the endpoint is absent, which is production behaviour', async () => {
    server.use(http.get('*/dev/links', () => new HttpResponse(null, { status: 404 })))

    renderLanding()

    expect(await screen.findByText('expired screen')).toBeInTheDocument()
  })
})
