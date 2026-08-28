import { memo, type CSSProperties, type ReactNode } from 'react'
import { Box, Center, Container, Group, Skeleton, Stepper, Text, Title } from '@mantine/core'
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
const HERO_TITLE_STYLE: CSSProperties = { ...tokens.type.heroTitle, color: 'inherit' }
const HERO_BODY_STYLE: CSSProperties = {
  color: onBrandScrim.bodyText,
  maxWidth: 400,
  fontSize: 15,
}

const HERO_TITLE = 'Confirm your baggage delivery'
const HERO_BODY =
  "Review your details below and pick a delivery window. We'll text you when our driver is on the way."

/**
 * The Ink-Blue band itself. Shared by the loaded hero and its skeleton so the two
 * cannot drift in height or padding — the skeleton exists precisely so that nothing
 * moves when the booking arrives.
 */
function HeroShell({ topRule, children }: { topRule?: string; children: ReactNode }) {
  return (
    <Box
      px={tokens.hero.px}
      pt={tokens.hero.pt}
      pb={tokens.hero.pb}
      style={
        topRule ? { ...HERO_BOX_STYLE, borderTop: `3px solid ${topRule}` } : HERO_BOX_STYLE
      }
    >
      <FlightPathBackdrop />
      {/* Above the backdrop, which sits at z-index 0 inside the hero. */}
      <Container size={tokens.hero.measure} px={0} style={{ position: 'relative', zIndex: 1 }}>
        {children}
      </Container>
    </Box>
  )
}

/** The delivery lifecycle, not a form wizard — no onStepClick, so every step is inert. */
function HeroStepper() {
  return (
    <Stepper active={0} size="xs" className="pax-hero-stepper" mt="xl">
      <Stepper.Step label="Confirm" />
      <Stepper.Step label="In transit" />
      <Stepper.Step label="Delivered" />
    </Stepper>
  )
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
    <HeroShell topRule={accent.accent}>
      <Group gap="sm" align="center" mb="lg" wrap="nowrap">
        <Center
          w={36}
          h={36}
          style={{
            borderRadius: tokens.radius.md,
            backgroundColor: accent.accentTint,
            flexShrink: 0,
          }}
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
        {HERO_TITLE}
      </Title>
      {/* The WorldTracer file reference — the reference the airline and the
          helpline both quote. Jobs without one show nothing rather than an
          internal id. */}
      {summary.fileReference && (
        <Box mt="md">
          <PunchedTag
            label="File reference"
            value={summary.fileReference}
            accent={accent.accent}
            onScrim
          />
        </Box>
      )}
      <Text mt="md" style={HERO_BODY_STYLE}>
        {HERO_BODY}
      </Text>

      <HeroStepper />
    </HeroShell>
  )
})

/**
 * The hero while the booking is still in flight.
 *
 * It shows the real headline and the real body copy, because neither depends on the
 * booking — only the airline name and the file reference do, and those are the only
 * things skeletoned. A passenger arriving from an SMS link therefore reads what the
 * page is for immediately, instead of watching a spinner on an empty screen and then
 * having the whole page appear underneath them.
 */
export function ConfirmHeroSkeleton() {
  return (
    <HeroShell>
      <Group gap="sm" align="center" mb="lg" wrap="nowrap">
        <Center
          w={36}
          h={36}
          style={{
            borderRadius: tokens.radius.md,
            backgroundColor: onBrandScrim.fill,
            flexShrink: 0,
          }}
        >
          <LuggageIcon size={20} color={onBrandScrim.text} />
        </Center>
        <Skeleton height={16} width={150} radius="sm" />
      </Group>

      <Title order={1} style={HERO_TITLE_STYLE}>
        {HERO_TITLE}
      </Title>
      <Box mt="md">
        <Skeleton height={46} width={210} radius={tokens.radius.tile} />
      </Box>
      <Text mt="md" style={HERO_BODY_STYLE}>
        {HERO_BODY}
      </Text>

      <HeroStepper />
    </HeroShell>
  )
}
