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
    expect(logo).toHaveAttribute('width', '147')
    expect(logo).toHaveAttribute('height', '50')

    expect(screen.getByRole('contentinfo')).toBeInTheDocument()
  })

  it('carries the attribution and nothing else', () => {
    render(
      <MantineTestProvider>
        <PoweredByFooter />
      </MantineTestProvider>,
    )

    // The carrier name repeated the hero, and a phone number at the foot of a form
    // is an invitation to stop filling it in and call. The number still appears on
    // the empty-window state, where it is the passenger's only way forward.
    expect(screen.queryByText(/need help\?/i)).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /call/i })).not.toBeInTheDocument()
    expect(screen.getByText(/powered by/i)).toBeInTheDocument()
  })
})
