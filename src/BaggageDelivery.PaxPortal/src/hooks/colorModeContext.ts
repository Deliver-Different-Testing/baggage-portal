/**
 * DFRNT color-mode context + hook. Split from the provider so Vite's react-refresh
 * "only export components" rule stays happy (the provider component lives in
 * ColorModeProvider.tsx). Defaults to the OS theme ('system').
 */
import { createContext, useContext } from 'react'
import type { Md3Mode } from '../styles/md3'

export type ColorModePreference = Md3Mode | 'system'

export interface ColorModeContextValue {
  mode: Md3Mode
  preference: ColorModePreference
  setPreference: (preference: ColorModePreference) => void
}

const DEFAULT_VALUE: ColorModeContextValue = {
  mode: 'light',
  preference: 'system',
  setPreference: () => {},
}

export const ColorModeContext = createContext<ColorModeContextValue>(DEFAULT_VALUE)

export function useColorMode(): ColorModeContextValue {
  return useContext(ColorModeContext)
}
