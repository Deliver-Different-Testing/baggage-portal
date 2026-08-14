/**
 * DFRNT light/dark color-mode provider. Portable copy from the IntegrationManager
 * AdminPortal. Defaults to the OS theme ('system'), persists an explicit
 * 'light'/'dark' pick to localStorage, and follows OS changes live.
 *
 * PaxPortal uses auto-dark (prefers-color-scheme) with no manual toggle — the
 * default 'system' preference is never changed, but `setPreference` is retained
 * so a toggle can be added later without touching this file.
 *
 * Wire it ABOVE MantineProvider and feed the resolved `mode` into
 * `<MantineProvider forceColorScheme={mode}>` so there is a single source of truth.
 */
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'
import type { Md3Mode } from '../styles/md3'
import {
  ColorModeContext,
  type ColorModePreference,
} from './colorModeContext'

const STORAGE_KEY = 'dd-color-mode'
const DARK_QUERY = '(prefers-color-scheme: dark)'

function readStoredPreference(): ColorModePreference {
  if (typeof window === 'undefined') return 'system'
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY)
    if (stored === 'light' || stored === 'dark' || stored === 'system') return stored
  } catch {
    /* localStorage unavailable — fall through to default */
  }
  return 'system'
}

function getSystemMode(): Md3Mode {
  try {
    if (window.matchMedia?.(DARK_QUERY).matches) return 'dark'
  } catch {
    /* matchMedia unavailable — fall through to default */
  }
  return 'light'
}

function resolveMode(preference: ColorModePreference): Md3Mode {
  return preference === 'system' ? getSystemMode() : preference
}

function persist(preference: ColorModePreference): void {
  try {
    window.localStorage.setItem(STORAGE_KEY, preference)
  } catch {
    /* ignore */
  }
}

export function ColorModeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreferenceState] = useState<ColorModePreference>(readStoredPreference)
  // Derived from the state above rather than a second read — the initializer only
  // runs on mount, so `preference` is already the stored value here.
  const [mode, setModeState] = useState<Md3Mode>(() => resolveMode(preference))
  const preferenceRef = useRef<ColorModePreference>(preference)

  const setPreference = useCallback((next: ColorModePreference) => {
    preferenceRef.current = next
    setPreferenceState(next)
    setModeState(resolveMode(next))
    persist(next)
  }, [])

  // Follow OS theme changes live while the preference is 'system'.
  useEffect(() => {
    if (typeof window === 'undefined' || !window.matchMedia) return
    const mql = window.matchMedia(DARK_QUERY)
    const onChange = (e: MediaQueryListEvent) => {
      if (preferenceRef.current === 'system') setModeState(e.matches ? 'dark' : 'light')
    }
    mql.addEventListener?.('change', onChange)
    return () => mql.removeEventListener?.('change', onChange)
  }, [])

  // Keep native UI (scrollbars, form controls, autofill) in sync with the mode.
  useEffect(() => {
    document.documentElement.style.colorScheme = mode
  }, [mode])

  const value = useMemo(
    () => ({ mode, preference, setPreference }),
    [mode, preference, setPreference],
  )
  return <ColorModeContext.Provider value={value}>{children}</ColorModeContext.Provider>
}
