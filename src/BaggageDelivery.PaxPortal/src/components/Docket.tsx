/**
 * The docket motif — the passenger portal's structural vocabulary.
 *
 * Baggage moves on printed artifacts: tags, WorldTracer file references, manifests,
 * dockets. These three primitives borrow that vocabulary so the product reads as
 * purpose-built rather than as a themed form. The rule they encode:
 *
 *   round = you touch it   ·   square (2px) = it's printed
 *
 * Interactive controls keep their 8–12px radii and pill buttons; anything carrying a
 * fact the passenger reads back drops to `tokens.radius.tile` with a hairline rule.
 */
import type { CSSProperties, ReactNode } from 'react'
import { Box, Center, Text } from '@mantine/core'
import { onBrandScrim, tokens } from '../styles/mantineTheme'

/** Where an {@link Eyebrow} is sitting, which decides how it gets its contrast. */
export type EyebrowTone =
  /** On a plain page/card surface — the page-level dimmed grey. */
  | 'dimmed'
  /** Inside a brand-tinted tile, where the dimmed grey is mixed for the wrong ground. */
  | 'onTint'
  /** On the Ink-Blue hero, where only a white alpha reads. */
  | 'onScrim'

const TONE_STYLE: Record<EyebrowTone, CSSProperties> = {
  dimmed: {},
  onTint: { opacity: 0.75 },
  onScrim: { color: onBrandScrim.mutedText },
}

/**
 * The uppercase micro-label. One spec — 10px / 700 / 0.12em — for every place a value
 * needs naming: the file reference, a docket line, the ETA, the tracking kicker. Six
 * hand-tuned variants had drifted across the pages before this existed.
 */
export function Eyebrow({
  children,
  tone = 'dimmed',
  mb = 4,
}: {
  children: ReactNode
  tone?: EyebrowTone
  mb?: number
}) {
  return (
    <Text
      c={tone === 'dimmed' ? 'dimmed' : undefined}
      mb={mb}
      style={{ ...tokens.type.eyebrow, ...TONE_STYLE[tone] }}
    >
      {children}
    </Text>
  )
}

/**
 * A printed fact: an {@link Eyebrow} over its value at 2px corners. `tint` promotes it
 * to the brand fill for the one line with real consequences (the chosen window);
 * `plain` drops the box entirely for docket rows that only need the label.
 */
export function DocketTile({
  label,
  children,
  variant = 'outline',
}: {
  label: ReactNode
  children: ReactNode
  variant?: 'outline' | 'tint' | 'plain'
}) {
  if (variant === 'plain') {
    return (
      <Box>
        <Eyebrow>{label}</Eyebrow>
        {children}
      </Box>
    )
  }

  const tinted = variant === 'tint'
  return (
    <Box
      px="md"
      py={12}
      style={{
        borderRadius: tokens.radius.tile,
        backgroundColor: tinted ? 'var(--mantine-color-brand-light)' : 'var(--dd-surface)',
        color: tinted ? 'var(--dd-on-brand-tint)' : undefined,
        border: tinted ? undefined : '1px solid var(--dd-outline-variant)',
      }}
    >
      <Eyebrow tone={tinted ? 'onTint' : 'dimmed'}>{label}</Eyebrow>
      {children}
    </Box>
  )
}

const TAG_STYLE: CSSProperties = {
  display: 'inline-flex',
  alignItems: 'stretch',
  borderRadius: tokens.radius.tile,
  overflow: 'hidden',
  maxWidth: '100%',
}
const TAG_DIVIDER_STYLE: CSSProperties = { width: 1, flexShrink: 0 }
const TAG_VALUE_STYLE: CSSProperties = {
  fontSize: 20,
  fontWeight: 700,
  letterSpacing: '0.06em',
  lineHeight: 1.15,
  fontVariantNumeric: 'tabular-nums',
  wordBreak: 'break-all',
}

/**
 * The file reference set as the thing it actually is — a baggage tag. It is the only
 * token the passenger will be asked to read back, and it is the page's answer to the
 * question they arrived with, which is whether anyone has their bag.
 *
 * `accent` is the carrier's colour and is the *only* place airline identity lands on
 * this component: the punch ring and the divider. The tag's ground stays neutral so
 * the value keeps its contrast whatever the airline is.
 *
 * The label and the value are deliberately separate text nodes — the helpline asks the
 * passenger to read the value alone.
 */
export function PunchedTag({
  label,
  value,
  onScrim = false,
  accent,
}: {
  label: ReactNode
  value: ReactNode
  /** Set on the Ink-Blue hero, where the tag paints itself in white alphas. */
  onScrim?: boolean
  accent?: string
}) {
  const edge = accent ?? (onScrim ? onBrandScrim.border : 'var(--dd-outline-variant)')
  return (
    <Box
      style={{
        ...TAG_STYLE,
        backgroundColor: onScrim ? onBrandScrim.hoverFill : 'var(--dd-surface)',
        border: `1px solid ${onScrim ? onBrandScrim.border : 'var(--dd-outline-variant)'}`,
      }}
    >
      <Center px={10} style={{ flexShrink: 0 }}>
        {/* The punch, in the colour of whatever the tag is lying on, so it reads as
            a hole through the tag rather than a dot printed on it: the Ink hero on
            the confirm page, the card the docket sits in everywhere else. */}
        <Box
          style={{
            width: 11,
            height: 11,
            borderRadius: '50%',
            backgroundColor: onScrim ? onBrandScrim.heroBg : 'var(--dd-surface-container)',
            boxShadow: `inset 0 0 0 1px ${edge}`,
          }}
        />
      </Center>
      <Box style={{ ...TAG_DIVIDER_STYLE, backgroundColor: edge }} />
      <Box px={14} py={8} style={{ minWidth: 0 }}>
        <Eyebrow tone={onScrim ? 'onScrim' : 'dimmed'} mb={3}>
          {label}
        </Eyebrow>
        <Text style={{ ...TAG_VALUE_STYLE, color: onScrim ? onBrandScrim.text : undefined }}>
          {value}
        </Text>
      </Box>
    </Box>
  )
}
