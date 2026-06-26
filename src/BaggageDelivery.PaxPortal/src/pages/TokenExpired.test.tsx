import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { ThemeProvider } from '@mui/material/styles'
import { TokenExpired } from './TokenExpired'
import { theme } from '../styles/theme'

describe('TokenExpired', () => {
  it('renders the not-found message and the Powered by Deliver DFRNT footer', () => {
    render(
      <ThemeProvider theme={theme}>
        <TokenExpired />
      </ThemeProvider>,
    )

    expect(screen.getByText(/booking not found/i)).toBeInTheDocument()
    expect(screen.getByText(/powered by/i)).toBeInTheDocument()
    expect(screen.getByRole('img', { name: /deliver dfrnt/i })).toBeInTheDocument()
  })
})
