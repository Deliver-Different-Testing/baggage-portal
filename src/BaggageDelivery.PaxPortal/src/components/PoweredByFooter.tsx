import type { CSSProperties } from 'react'
import { Box, Group, Text } from '@mantine/core'
import { useColorMode } from '../hooks/colorModeContext'

interface PoweredByFooterProps {
  style?: CSSProperties
}

export function PoweredByFooter({ style }: PoweredByFooterProps) {
  const { mode } = useColorMode()
  const logoSrc = mode === 'dark' ? '/dfrnt-logo-dark.png' : '/dfrnt-logo-light.png'

  return (
    <Box component="footer" style={{ width: '100%', ...style }} py="lg" px="md">
      <Group gap={9} justify="center" align="center">
        <Text c="dimmed" style={{ fontSize: 16, letterSpacing: '0.04em' }}>
          Powered by
        </Text>
        <img
          src={logoSrc}
          alt="Deliver DFRNT"
          // Intrinsic 2334×792 (≈2.95:1); attributes let the browser reserve the
          // rendered 165×56 box so the logo swap doesn't shift layout.
          width={165}
          height={56}
          style={{ height: 56, width: 'auto', display: 'block' }}
        />
      </Group>
    </Box>
  )
}
