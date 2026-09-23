import { memo } from 'react'
import type { UseQueryResult } from '@tanstack/react-query'
import { Button, Group, Loader, Stack, Text } from '@mantine/core'
import { FormSection } from './FormSection'
import { RunStartNotice } from './RunStartNotice'
import { SlotList } from './SlotList'
import type { TimeSlot } from '../../api/client'

export const WindowSection = memo(function WindowSection({
  slots,
  available,
  selectedId,
  selectedSlot,
  supportPhone,
  error,
  onSelect,
  onRunStartPassed,
}: {
  slots: UseQueryResult<TimeSlot[]>
  available: TimeSlot[]
  selectedId: string | null
  selectedSlot: TimeSlot | undefined
  supportPhone: string
  error?: string
  onSelect: (id: string) => void
  onRunStartPassed: () => void
}) {
  const { refetch } = slots

  return (
    <FormSection title="Delivery window" subtitle="When we should bring your bag" emphasis="primary">
      {slots.isLoading && (
        <Group gap="sm" align="center" py="xs">
          <Loader size={18} />
          <Text size="sm" c="dimmed">
            Loading available windows…
          </Text>
        </Group>
      )}
      {error && (
        <Text size="sm" c="red" mb="xs">
          {error}
        </Text>
      )}
      {slots.isError && (
        <Stack gap="xs" align="flex-start">
          <Text size="sm">We couldn&apos;t load the available delivery windows just now.</Text>
          <Button variant="light" size="xs" onClick={() => void refetch()}>
            Try again
          </Button>
        </Stack>
      )}
      {slots.data && available.length === 0 && (
        <Stack gap={4} role="status">
          <Text size="sm">There are no delivery windows available for this booking yet.</Text>
          {supportPhone && (
            <Text size="sm">Call {supportPhone} and we&apos;ll arrange one with you.</Text>
          )}
        </Stack>
      )}
      {available.length > 0 && (
        <SlotList slots={available} selectedId={selectedId} onSelect={onSelect} />
      )}
      {selectedSlot?.runUtc && (
        <RunStartNotice targetUtc={selectedSlot.runUtc} onExpire={onRunStartPassed} />
      )}
    </FormSection>
  )
})
