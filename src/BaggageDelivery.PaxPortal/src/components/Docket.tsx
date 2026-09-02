import type { CSSProperties, ReactNode } from 'react'
import { Box, Center, Text } from '@mantine/core'
import { onBrandScrim, tokens } from '../styles/mantineTheme'

export type EyebrowTone =
  | 'dimmed'
  | 'onTint'
  | 'onScrim'

const TONE_STYLE: Record<EyebrowTone, CSSProperties> = {
  dimmed: {},
  onTint: { opacity: 0.75 },
  onScrim: { color: onBrandScrim.mutedText },
}

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

export function PunchedTag({
  label,
  value,
  onScrim = false,
  accent,
}: {
  label: ReactNode
  value: ReactNode
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

const BAG_TAG_VALUE_STYLE: CSSProperties = {
  fontSize: 22,
  fontWeight: 700,
  letterSpacing: '0.14em',
  lineHeight: 1.1,
  fontVariantNumeric: 'tabular-nums',
  wordBreak: 'break-all',
}

export function BagTag({
  label,
  value,
  caption,
  accent,
}: {
  label: ReactNode
  value: ReactNode
  caption?: ReactNode
  accent: string
}) {
  return (
    <Box
      style={{
        display: 'inline-flex',
        alignItems: 'stretch',
        borderRadius: tokens.radius.tile,
        overflow: 'hidden',
        maxWidth: '100%',
        backgroundColor: onBrandScrim.hoverFill,
        border: `1px solid ${onBrandScrim.border}`,
      }}
    >
      <Center px={9} style={{ flexShrink: 0, backgroundColor: accent }}>
        <Box
          style={{
            width: 12,
            height: 12,
            borderRadius: '50%',
            backgroundColor: onBrandScrim.heroBg,
          }}
        />
      </Center>
      <Box px={14} py={10} style={{ minWidth: 0 }}>
        <Eyebrow tone="onScrim" mb={3}>
          {label}
        </Eyebrow>
        <Text style={{ ...BAG_TAG_VALUE_STYLE, color: onBrandScrim.text }}>{value}</Text>
        {caption && (
          <Text mt={2} style={{ color: onBrandScrim.mutedText, fontSize: 13, fontWeight: 500 }}>
            {caption}
          </Text>
        )}
      </Box>
    </Box>
  )
}
