import { memo, type CSSProperties } from 'react'
import { Box, Group, Text } from '@mantine/core'
import { useColorMode } from '../hooks/colorModeContext'

interface PoweredByFooterProps {
  style?: CSSProperties
}

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
          width={147}
          height={50}
          style={{ height: 50, width: 'auto', display: 'block' }}
        />
      </Group>
    </Box>
  )
})
