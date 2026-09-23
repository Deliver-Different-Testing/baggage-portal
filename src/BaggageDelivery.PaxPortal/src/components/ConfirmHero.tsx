import { memo, type CSSProperties, type ReactNode } from 'react'
import { Box, Center, Container, Group, Skeleton, Text, Title } from '@mantine/core'
import { LuggageIcon } from './Icon'
import { BagTag } from './Docket'
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

function heroBody(airlineLabel: string) {
  const handler = airlineLabel.trim() || 'Your airline'
  return `${handler} passed your delayed bag to us. Pick a time and check where it should go.`
}

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

export const ConfirmHero = memo(function ConfirmHero({
  summary,
  accent,
  title,
  body,
}: {
  summary: BookingSummary
  accent: AirlineAccent
  title?: string
  body?: string
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
        {title ?? HERO_TITLE}
      </Title>
      {summary.fileReference && (
        <Box mt="md">
          <BagTag
            label="File reference"
            value={summary.fileReference}
            caption={summary.passengerName}
            accent={accent.accent}
          />
        </Box>
      )}

      <Text mt="md" style={HERO_BODY_STYLE}>
        {body ?? heroBody(summary.airlineLabel)}
      </Text>
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
        <Skeleton height={64} width={230} radius={tokens.radius.tile} />
      </Box>
      <Skeleton height={15} width={300} radius="sm" mt="md" />
    </HeroShell>
  )
}
