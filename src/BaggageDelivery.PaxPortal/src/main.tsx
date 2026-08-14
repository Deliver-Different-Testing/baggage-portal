import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import '@fontsource-variable/plus-jakarta-sans/index.css'
import '@mantine/core/styles.css'
import '@mantine/notifications/styles.css'
import './index.css'

import { ColorModeProvider } from './hooks/ColorModeProvider'
import { prefetchRouteData } from './api/prefetch'
import { ThemedApp } from './ThemedApp'

// Before React mounts: the passenger's booking is fetched in parallel with the
// lazy route chunk rather than after it.
void prefetchRouteData(window.location.pathname)

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ColorModeProvider>
      <ThemedApp />
    </ColorModeProvider>
  </StrictMode>,
)
