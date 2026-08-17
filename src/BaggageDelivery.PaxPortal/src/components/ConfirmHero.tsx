import { memo, type CSSProperties } from 'react'
import { Box, Center, Container, Group, Stepper, Text, Title } from '@mantine/core'
import { LuggageIcon } from './Icon'
import { PunchedTag } from './Docket'
import { FlightPathBackdrop } from './FlightPathBackdrop'
import { onBrandScrim, tokens } from '../styles/mantineTheme'
import type { AirlineAccent } from '../styles/airlineAccent'
import type { BookingSummary } from '../api/client'

const HERO_BOX_STYLE: CSSProperties = {
  backgroundColor: onBrandScrim.heroBg,
  color: onBrandScrim.text,
  position: 'relative',
  overflow: 'hidden',
}
const HERO_AIRLINE_STYLE: CSSProperties = {
  color: onBrandScrim.text,
  fontWeight: 600,
  fontSize: 15,
  lineHeight: 1.2,
}
const HERO_TITLE_STYLE: CSSProperties = {
  fontSize: tokens.type.hero,
  lineHeight: 1.05,
  color: 'inherit',
  fontWeight: 700,
  letterSpacing: '-0.03em',
}
const HERO_BODY_STYLE: CSSProperties = {
  color: onBrandScrim.bodyText,
  maxWidth: 400,
  fontSize: 15,
}

/**
 * The Ink-Blue banner above the confirm form. Memoized and reading only from the
 * booking: nothing here changes as the passenger fills the form in, so it should
 * not re-render on every keystroke. `accent` must therefore be memoized by the
 * caller — a fresh object per render defeats this `memo`.
 */
export const ConfirmHero = memo(function ConfirmHero({
  summary,
  accent,
}: {
  summary: BookingSummary
  accent: AirlineAccent
}) {
  return (
    <Box
      px={{ base: 20, sm: 32 }}
      pt={{ base: 32, sm: 40 }}
      pb={{ base: 48, sm: 56 }}
      style={{
        ...HERO_BOX_STYLE,
        borderTop: `3px solid ${accent.accent}`,
      }}
    >
      <FlightPathBackdrop />
      {/* Above the backdrop, which sits at z-index 0 inside the hero. */}
      <Container size={520} px={0} style={{ position: 'relative', zIndex: 1 }}>
        <Group gap="sm" align="center" mb="lg" wrap="nowrap">
          <Center
            w={36}
            h={36}
            style={{ borderRadius: 12, backgroundColor: accent.accentTint, flexShrink: 0 }}
          >
            <LuggageIcon size={20} color={onBrandScrim.text} />
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
          <Box mt="md">
            <PunchedTag
              label="File reference"
              value={summary.reference}
              accent={accent.accent}
              onScrim
            />
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
