import { memo, type CSSProperties } from 'react'
import { Box, Center, Container, Group, Stepper, Text, Title } from '@mantine/core'
import { LuggageIcon } from './Icon'
import type { BookingSummary } from '../api/client'

// White-on-Ink scrim overlays for the brand hero. These are white alphas (not brand
// hex), so they read on the fixed Ink-Blue hero regardless of the primary colour.
const SCRIM_HERO = 'var(--mantine-color-ink-9)'
const SCRIM_FILL = 'rgba(255,255,255,0.18)'
const SCRIM_BODY = 'rgba(255,255,255,0.85)'

const HERO_BOX_STYLE: CSSProperties = {
  backgroundColor: SCRIM_HERO,
  color: '#fff',
  position: 'relative',
  overflow: 'hidden',
}
const HERO_AIRLINE_STYLE: CSSProperties = {
  color: 'var(--mantine-color-white)',
  fontWeight: 600,
  fontSize: 15,
  lineHeight: 1.2,
}
const HERO_TITLE_STYLE: CSSProperties = {
  fontSize: 'clamp(32px, 8vw, 40px)',
  lineHeight: 1.05,
  color: 'inherit',
  fontWeight: 700,
  letterSpacing: '-0.03em',
}
// The file reference set as the thing it actually is — a baggage tag. It is the
// only token on the page the passenger will be asked to read back, and it is the
// page's answer to the question they arrived with, which is whether anyone has
// their bag. Everything else in the hero stays quiet around it.
const TAG_STYLE: CSSProperties = {
  display: 'inline-flex',
  alignItems: 'stretch',
  borderRadius: 'var(--mantine-radius-sm)',
  backgroundColor: 'rgba(255,255,255,0.10)',
  border: '1px solid rgba(255,255,255,0.22)',
  overflow: 'hidden',
  maxWidth: '100%',
}
// The punch, in the hero's own colour so it reads as a hole through the tag
// rather than a dot printed on it.
const TAG_PUNCH_STYLE: CSSProperties = {
  width: 11,
  height: 11,
  borderRadius: '50%',
  backgroundColor: SCRIM_HERO,
  boxShadow: 'inset 0 0 0 1px rgba(255,255,255,0.28)',
}
const TAG_DIVIDER_STYLE: CSSProperties = {
  width: 1,
  backgroundColor: 'rgba(255,255,255,0.22)',
  flexShrink: 0,
}
const TAG_LABEL_STYLE: CSSProperties = {
  color: 'rgba(255,255,255,0.7)',
  fontSize: 9,
  fontWeight: 700,
  letterSpacing: '0.12em',
  textTransform: 'uppercase',
  lineHeight: 1.2,
}
const TAG_VALUE_STYLE: CSSProperties = {
  color: 'var(--mantine-color-white)',
  fontSize: 20,
  fontWeight: 700,
  letterSpacing: '0.06em',
  lineHeight: 1.15,
  fontVariantNumeric: 'tabular-nums',
  wordBreak: 'break-all',
}
const HERO_BODY_STYLE: CSSProperties = { color: SCRIM_BODY, maxWidth: 400, fontSize: 15 }

/**
 * The Ink-Blue banner above the confirm form. Memoized and reading only from the
 * booking: nothing here changes as the passenger fills the form in, so it should
 * not re-render on every keystroke.
 */
export const ConfirmHero = memo(function ConfirmHero({ summary }: { summary: BookingSummary }) {
  return (
    <Box
      px={{ base: 20, sm: 32 }}
      pt={{ base: 32, sm: 40 }}
      pb={{ base: 48, sm: 56 }}
      style={HERO_BOX_STYLE}
    >
      <Container size={520} px={0} style={{ position: 'relative' }}>
        <Group gap="sm" align="center" mb="lg" wrap="nowrap">
          <Center
            w={36}
            h={36}
            style={{ borderRadius: 12, backgroundColor: SCRIM_FILL, flexShrink: 0 }}
          >
            <LuggageIcon size={20} color="#fff" />
          </Center>
          <Box style={{ flex: 1, minWidth: 0 }}>
            {/* Who the file sits with, not what the page is for — the identity
                line above the task. */}
            <Text style={HERO_AIRLINE_STYLE}>{summary.airlineLabel}</Text>
          </Box>
        </Group>

        {/* The task carries the display type as well as the heading level. The
            airline name led at 40px before, which told the passenger who sent
            them here but never what the page wanted from them. */}
        <Title order={1} style={HERO_TITLE_STYLE}>
          Confirm your baggage delivery
        </Title>
        {/* The WorldTracer reference the helpline asks for. Jobs without one show
            nothing rather than an internal id. */}
        {summary.reference && (
          <Box mt="md" style={TAG_STYLE}>
            <Center px={10} style={{ flexShrink: 0 }}>
              <Box style={TAG_PUNCH_STYLE} />
            </Center>
            <Box style={TAG_DIVIDER_STYLE} />
            <Box px={14} py={8} style={{ minWidth: 0 }}>
              <Text style={TAG_LABEL_STYLE}>File reference</Text>
              <Text mt={3} style={TAG_VALUE_STYLE}>
                {summary.reference}
              </Text>
            </Box>
          </Box>
        )}
        <Text mt="md" style={HERO_BODY_STYLE}>
          Review your details below and pick a delivery window. We'll text you when our driver
          is on the way.
        </Text>

        {/* Delivery lifecycle, not a form wizard — no onStepClick, so Mantine
            renders every step non-interactive. Stock Stepper styling otherwise;
            index.css only re-points the palette for the Ink hero. */}
        <Stepper active={0} size="xs" className="pax-hero-stepper" mt="xl">
          <Stepper.Step label="Confirm" />
          <Stepper.Step label="In transit" />
          <Stepper.Step label="Delivered" />
        </Stepper>
      </Container>
    </Box>
  )
})
