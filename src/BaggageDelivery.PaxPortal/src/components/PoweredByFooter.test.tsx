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

    expect(screen.queryByText(/need help\?/i)).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /call/i })).not.toBeInTheDocument()
    expect(screen.getByText(/powered by/i)).toBeInTheDocument()
  })
})
