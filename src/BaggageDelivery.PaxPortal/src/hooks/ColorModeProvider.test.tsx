import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { ColorModeProvider } from './ColorModeProvider'
import { useColorMode } from './colorModeContext'

const STORAGE_KEY = 'dd-color-mode'

function Probe() {
  const { mode, preference } = useColorMode()
  return <span data-testid="probe">{`${preference}/${mode}`}</span>
}

afterEach(() => {
  window.localStorage.clear()
  vi.restoreAllMocks()
})

describe('ColorModeProvider', () => {
  it('reads the stored preference once on mount', () => {
    const getItem = vi.spyOn(Storage.prototype, 'getItem')

    render(
      <ColorModeProvider>
        <Probe />
      </ColorModeProvider>,
    )

    expect(getItem.mock.calls.filter(([key]) => key === STORAGE_KEY)).toHaveLength(1)
  })

  it('resolves an explicit stored preference without consulting the OS', () => {
    window.localStorage.setItem(STORAGE_KEY, 'dark')

    render(
      <ColorModeProvider>
        <Probe />
      </ColorModeProvider>,
    )

    expect(screen.getByTestId('probe')).toHaveTextContent('dark/dark')
    expect(document.documentElement.style.colorScheme).toBe('dark')
  })

  it('falls back to the OS scheme when nothing is stored', () => {
    render(
      <ColorModeProvider>
        <Probe />
      </ColorModeProvider>,
    )

    // The jsdom matchMedia stub reports no dark preference.
    expect(screen.getByTestId('probe')).toHaveTextContent('system/light')
  })
})
