import { memo, type CSSProperties } from 'react'
import { Box, Group, Text } from '@mantine/core'
import { useColorMode } from '../hooks/colorModeContext'

interface PoweredByFooterProps {
  style?: CSSProperties
}

/**
 * The sign-off: an attribution mark and nothing else.
 *
 * The airline name and the support number used to sit above it. The name repeated
 * the hero, which already leads with the carrier the file sits with, and a phone
 * number at the foot of a form is an invitation to stop filling it in and call. The
 * number still appears where it is the passenger's only way forward — the
 * empty-window state on the confirm page.
 *
 * Memoized: it owes nothing to what the passenger is typing, but re-rendering it
 * re-renders the logo image on every keystroke.
 */
export const PoweredByFooter = memo(function PoweredByFooter({ style }: PoweredByFooterProps) {
  const { mode } = useColorMode()
  const logoSrc = mode === 'dark' ? '/dfrnt-logo-dark.png' : '/dfrnt-logo-light.png'

  return (
    <Box component="footer" style={{ width: '100%', ...style }} py="xl" px="md">
      <Group gap={10} justify="center" align="center">
        <Text c="dimmed" style={{ fontSize: 14, letterSpacing: '0.04em' }}>
          Powered by
        </Text>
        <img
          src={logoSrc}
          alt="Deliver DFRNT"
          // Intrinsic 2334×792 (≈2.95:1); attributes let the browser reserve the
          // rendered 147×50 box so the logo swap doesn't shift layout.
          width={147}
          height={50}
          style={{ height: 50, width: 'auto', display: 'block' }}
        />
      </Group>
    </Box>
  )
})
