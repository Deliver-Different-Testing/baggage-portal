import { MantineProvider } from '@mantine/core'
import { Notifications } from '@mantine/notifications'
import { QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router-dom'

import { dfrntTheme, dfrntCssVariablesResolver } from './styles/mantineTheme'
import { useColorMode } from './hooks/colorModeContext'
import { queryClient } from './api/queryClient'
import { App } from './App'

export function ThemedApp() {
  const { mode } = useColorMode()
  return (
    <MantineProvider
      theme={dfrntTheme}
      forceColorScheme={mode}
      cssVariablesResolver={dfrntCssVariablesResolver}
    >
      <Notifications position="top-center" />
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <App />
        </BrowserRouter>
      </QueryClientProvider>
    </MantineProvider>
  )
}
