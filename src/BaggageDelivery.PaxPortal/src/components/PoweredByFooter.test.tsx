import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { PoweredByFooter } from './PoweredByFooter'
import { MantineTestProvider } from '../test/render'

describe('PoweredByFooter', () => {
  it('renders the "Powered by" attribution with the Deliver DFRNT logo in a footer landmark', () => {
    render(
      <MantineTestProvider>
        <PoweredByFooter />
      </MantineTestProvider>,
    )

    expect(screen.getByText(/powered by/i)).toBeInTheDocument()

    const logo = screen.getByRole('img', { name: /deliver dfrnt/i })
    expect(logo).toHaveAttribute('src', '/dfrnt-logo-light.png')

    // Intrinsic dimensions stay on the element so the light/dark swap reserves
    // its box and doesn't shift the footer.
    expect(logo).toHaveAttribute('width', '118')
    expect(logo).toHaveAttribute('height', '40')

    expect(screen.getByRole('contentinfo')).toBeInTheDocument()
  })

  it('signs off with the client name and a dialable support number', () => {
    render(
      <MantineTestProvider>
        <PoweredByFooter clientName="Air New Zealand" supportPhone="0800 267 5494" />
      </MantineTestProvider>,
    )

    expect(screen.getByText('Air New Zealand')).toBeInTheDocument()
    expect(screen.getByText(/need help\?/i)).toBeInTheDocument()

    // Punctuation is for reading, not for dialling.
    const call = screen.getByRole('link', { name: /call 0800 267 5494/i })
    expect(call).toHaveAttribute('href', 'tel:08002675494')
  })

  it('drops the sign-off entirely when there is no client or number to show', () => {
    render(
      <MantineTestProvider>
        <PoweredByFooter />
      </MantineTestProvider>,
    )

    expect(screen.queryByText(/need help\?/i)).not.toBeInTheDocument()
    expect(screen.getByText(/powered by/i)).toBeInTheDocument()
  })
})
