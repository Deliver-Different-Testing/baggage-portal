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
      <Container size={tokens.hero.measure} px={0} style={{ position: 'relative', zIndex: 1 }}>
        {children}
      </Container>
    </Box>
  )
}

function HeroStepper() {
  return (
    <Stepper active={0} size="xs" className="pax-hero-stepper" mt="xl">
      <Stepper.Step label="Confirm" />
      <Stepper.Step label="In transit" />
      <Stepper.Step label="Delivered" />
    </Stepper>
  )
}

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
          <Text style={HERO_AIRLINE_STYLE}>{summary.airlineLabel}</Text>
        </Box>
      </Group>

      <Title order={1} style={HERO_TITLE_STYLE}>
        {HERO_TITLE}
      </Title>
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
