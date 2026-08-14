import type { ReactNode } from 'react'
import { MantineProvider } from '@mantine/core'
import { Notifications } from '@mantine/notifications'
import { dfrntTheme } from '../styles/mantineTheme'

/**
 * Wraps components under test in the DFRNT MantineProvider. `env="test"` disables
 * Mantine transitions and portals so Collapse/Tooltip/Notifications resolve
 * synchronously under jsdom (otherwise Collapse never opens without a real
 * transitionend event).
 */
export function MantineTestProvider({ children }: { children: ReactNode }) {
  return (
    <MantineProvider theme={dfrntTheme} env="test">
      {/* Mirrors ThemedApp so toast copy is assertable — several failure paths
          only ever speak to the passenger through a notification. */}
      <Notifications position="top-center" />
      {children}
    </MantineProvider>
  )
}
