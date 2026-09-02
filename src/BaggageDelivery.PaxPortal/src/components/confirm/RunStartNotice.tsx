import { memo } from 'react'
import { Group, Text } from '@mantine/core'
import { ClockIcon } from '../Icon'
import { formatCountdown, useRunStartCountdown } from '../../hooks/useRunStartCountdown'

const RUN_URGENT_MS = 10 * 60_000

export const RunStartNotice = memo(function RunStartNotice({
  targetUtc,
  onExpire,
}: {
  targetUtc: string | undefined
  onExpire: () => void
}) {
  const remainingMs = useRunStartCountdown(targetUtc, onExpire)
  const urgent = remainingMs <= RUN_URGENT_MS

  return (
    <Group
      role="timer"
      data-urgent={urgent || undefined}
      aria-label="Time left to keep this window"
      gap={8}
      align="center"
      wrap="nowrap"
      mt="sm"
      px="sm"
      py={8}
      style={{
        borderRadius: 'var(--mantine-radius-sm)',
        backgroundColor: urgent
          ? 'var(--mantine-color-orange-light)'
          : 'var(--dd-surface-container-high)',
        border: `1px solid ${
          urgent ? 'var(--mantine-color-orange-filled)' : 'var(--mantine-color-default-border)'
        }`,
      }}
    >
      <ClockIcon size={14} />
      <Text
        size="xs"
        c={urgent ? undefined : 'dimmed'}
        fw={urgent ? 600 : undefined}
        style={{ flex: 1, minWidth: 0 }}
      >
        Time left to keep this window
      </Text>
      <Text
        size="xs"
        fw={700}
        c={urgent ? 'orange' : 'brand'}
        style={{ fontVariantNumeric: 'tabular-nums' }}
      >
        {formatCountdown(remainingMs)}
      </Text>
    </Group>
  )
})
