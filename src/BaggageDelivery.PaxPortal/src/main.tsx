import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import '@fontsource-variable/plus-jakarta-sans/index.css'
import '@mantine/core/styles.css'
import '@mantine/notifications/styles.css'
import './index.css'

import { ColorModeProvider } from './hooks/ColorModeProvider'
import { ThemedApp } from './ThemedApp'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ColorModeProvider>
      <ThemedApp />
    </ColorModeProvider>
  </StrictMode>,
)
