import type { ReactNode } from 'react'
import { MantineProvider } from '@mantine/core'
import { Notifications } from '@mantine/notifications'
import { dfrntTheme } from '../styles/mantineTheme'

export function MantineTestProvider({ children }: { children: ReactNode }) {
  return (
    <MantineProvider theme={dfrntTheme} env="test">
      <Notifications position="top-center" />
      {children}
    </MantineProvider>
  )
}
