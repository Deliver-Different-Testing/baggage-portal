import { memo, type CSSProperties } from 'react'
import { Anchor, Box, Group, Stack, Text } from '@mantine/core'
import { useColorMode } from '../hooks/colorModeContext'

interface PoweredByFooterProps {
  style?: CSSProperties
  /** The airline the baggage file sits with — same name the hero leads with. */
  clientName?: string
  /** Shown as "Need help? Call …". Omitted when the tenant has no number. */
  supportPhone?: string
}

// tel: wants digits (and a leading +) only — "09-333 3333" dials as 093333333.
function telHref(phone: string) {
  return `tel:${phone.replace(/[^\d+]/g, '')}`
}

// Memoized: it sits below the confirm form and owes nothing to what the passenger
// is typing, but re-rendering it re-renders the logo image on every keystroke.
export const PoweredByFooter = memo(function PoweredByFooter({
  style,
  clientName,
  supportPhone,
}: PoweredByFooterProps) {
  const { mode } = useColorMode()
  const logoSrc = mode === 'dark' ? '/dfrnt-logo-dark.png' : '/dfrnt-logo-light.png'

  return (
    <Box component="footer" style={{ width: '100%', ...style }} py="lg" px="md">
      {(clientName || supportPhone) && (
        <Stack gap={2} align="center" mb="md">
          {clientName && (
            <Text size="sm" fw={600}>
              {clientName}
            </Text>
          )}
          {supportPhone && (
            <Text size="sm" c="dimmed">
              Need help?{' '}
              {/* The one thing worth tapping down here, so it gets the link colour
                  and a taller hit area than inline text would give it. */}
              <Anchor
                href={telHref(supportPhone)}
                fw={600}
                style={{ display: 'inline-block', paddingBlock: 4 }}
              >
                Call {supportPhone}
              </Anchor>
            </Text>
          )}
        </Stack>
      )}
      <Group gap={8} justify="center" align="center">
        <Text c="dimmed" style={{ fontSize: 13, letterSpacing: '0.04em' }}>
          Powered by
        </Text>
        <img
          src={logoSrc}
          alt="Deliver DFRNT"
          // Intrinsic 2334×792 (≈2.95:1); attributes let the browser reserve the
          // rendered 118×40 box so the logo swap doesn't shift layout. Sized under
          // the airline sign-off above it — the carrier is who the passenger is
          // dealing with, and the mark was outweighing them at 56px.
          width={118}
          height={40}
          style={{ height: 40, width: 'auto', display: 'block' }}
        />
      </Group>
    </Box>
  )
})
