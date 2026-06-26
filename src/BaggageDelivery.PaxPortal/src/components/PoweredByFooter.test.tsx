import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { ThemeProvider } from '@mui/material/styles'
import { PoweredByFooter } from './PoweredByFooter'
import { theme } from '../styles/theme'

describe('PoweredByFooter', () => {
  it('renders the "Powered by" attribution with the Deliver DFRNT logo in a footer landmark', () => {
    render(
      <ThemeProvider theme={theme}>
        <PoweredByFooter />
      </ThemeProvider>,
    )

    expect(screen.getByText(/powered by/i)).toBeInTheDocument()

    const logo = screen.getByRole('img', { name: /deliver dfrnt/i })
    expect(logo).toHaveAttribute('src', '/dfrnt-logo.png')

    expect(screen.getByRole('contentinfo')).toBeInTheDocument()
  })
})
