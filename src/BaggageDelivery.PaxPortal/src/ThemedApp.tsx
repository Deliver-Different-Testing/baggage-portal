import { MantineProvider } from '@mantine/core'
import { Notifications } from '@mantine/notifications'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router-dom'

import { dfrntTheme, dfrntCssVariablesResolver } from './styles/mantineTheme'
import { useColorMode } from './hooks/colorModeContext'
import { App } from './App'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
      networkMode: 'offlineFirst',
    },
    mutations: {
      networkMode: 'online',
    },
  },
})

// `mode` comes from ColorModeProvider (defaults to the OS 'system' preference and
// follows prefers-color-scheme live) and is the single source of truth for the
// scheme — feed it to forceColorScheme rather than Mantine's own auto switch.
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
