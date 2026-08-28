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
    return 'system'
  }
  return 'system'
}

function getSystemMode(): Md3Mode {
  try {
    if (window.matchMedia?.(DARK_QUERY).matches) return 'dark'
  } catch {
    return 'light'
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
    return
  }
}

export function ColorModeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreferenceState] = useState<ColorModePreference>(readStoredPreference)
  const [mode, setModeState] = useState<Md3Mode>(() => resolveMode(preference))
  const preferenceRef = useRef<ColorModePreference>(preference)

  const setPreference = useCallback((next: ColorModePreference) => {
    preferenceRef.current = next
    setPreferenceState(next)
    setModeState(resolveMode(next))
    persist(next)
  }, [])

  useEffect(() => {
    if (typeof window === 'undefined' || !window.matchMedia) return
    const mql = window.matchMedia(DARK_QUERY)
    const onChange = (e: MediaQueryListEvent) => {
      if (preferenceRef.current === 'system') setModeState(e.matches ? 'dark' : 'light')
    }
    mql.addEventListener?.('change', onChange)
    return () => mql.removeEventListener?.('change', onChange)
  }, [])

  useEffect(() => {
    document.documentElement.style.colorScheme = mode
  }, [mode])

  const value = useMemo(
    () => ({ mode, preference, setPreference }),
    [mode, preference, setPreference],
  )
  return <ColorModeContext.Provider value={value}>{children}</ColorModeContext.Provider>
}
