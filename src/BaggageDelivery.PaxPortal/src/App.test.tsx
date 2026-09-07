import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { App } from './App'
import { MantineTestProvider } from './test/render'

function renderAt(path: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <MemoryRouter initialEntries={[path]}>
      <MantineTestProvider>
        <QueryClientProvider client={queryClient}>
          <App />
        </QueryClientProvider>
      </MantineTestProvider>
    </MemoryRouter>,
  )
}

describe('App routing', () => {
  it('sends an old in-app tracking link to the expired screen', async () => {
    renderAt('/t/token-abc-123')

    expect(await screen.findByText(/booking not found/i)).toBeInTheDocument()
  })
})
