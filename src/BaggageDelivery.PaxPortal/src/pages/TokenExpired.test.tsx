import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { TokenExpired } from './TokenExpired'
import { MantineTestProvider } from '../test/render'

describe('TokenExpired', () => {
  it('renders the not-found message and the Powered by Deliver DFRNT footer', () => {
    render(
      <MantineTestProvider>
        <TokenExpired />
      </MantineTestProvider>,
    )

    expect(screen.getByText(/booking not found/i)).toBeInTheDocument()
    expect(screen.getByText(/powered by/i)).toBeInTheDocument()
    expect(screen.getByRole('img', { name: /deliver dfrnt/i })).toBeInTheDocument()
  })
})
