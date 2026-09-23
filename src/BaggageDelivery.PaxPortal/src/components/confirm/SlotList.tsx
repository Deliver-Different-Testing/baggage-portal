import { memo, useRef, type KeyboardEvent } from 'react'
import { Box, Center, Stack, Text } from '@mantine/core'
import { tokens } from '../../styles/mantineTheme'
import type { TimeSlot } from '../../api/client'

export const SlotOption = memo(function SlotOption({
  slot,
  selected,
  onSelect,
}: {
  slot: TimeSlot
  selected: boolean
  onSelect: (id: string) => void
}) {
  return (
    <Box
      role="radio"
      aria-checked={selected}
      tabIndex={selected ? 0 : -1}
      className="pax-slot"
      onClick={() => onSelect(slot.id)}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault()
          onSelect(slot.id)
        }
      }}
      style={{
        borderColor: selected
          ? 'var(--mantine-color-brand-filled)'
          : 'var(--mantine-color-default-border)',
        backgroundColor: selected
          ? 'var(--mantine-color-brand-light)'
          : 'var(--dd-surface-container)',
      }}
    >
      <Center
        w={20}
        h={20}
        style={{
          borderRadius: '50%',
          border: `2px solid ${selected ? 'var(--mantine-color-brand-filled)' : 'var(--mantine-color-default-border)'}`,
          flexShrink: 0,
        }}
      >
        {selected && (
          <Box
            w={10}
            h={10}
            style={{ borderRadius: '50%', backgroundColor: 'var(--mantine-color-brand-filled)' }}
          />
        )}
      </Center>
      <Box style={{ flex: 1, minWidth: 0 }}>
        <Text size="xs" c="dimmed">
          {slot.dayLabel}
        </Text>
        <Text size="md" fw={700} style={{ fontVariantNumeric: 'tabular-nums' }}>
          {slot.label}
        </Text>
        {slot.firstAvailable && (
          <Text style={{ ...tokens.type.eyebrow, color: 'var(--dd-on-brand-tint)' }}>
            First available
          </Text>
        )}
      </Box>
    </Box>
  )
})

export const SlotList = memo(function SlotList({
  slots,
  selectedId,
  onSelect,
}: {
  slots: TimeSlot[]
  selectedId: string | null
  onSelect: (id: string) => void
}) {
  const ref = useRef<HTMLDivElement>(null)

  const moveTo = (index: number) => {
    const target = slots[index]
    if (!target) return
    onSelect(target.id)
    ref.current?.querySelectorAll<HTMLElement>('[role="radio"]')[index]?.focus()
  }

  const handleKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    const current = slots.findIndex((s) => s.id === selectedId)
    if (current === -1) return
    switch (e.key) {
      case 'ArrowDown':
      case 'ArrowRight':
        e.preventDefault()
        moveTo((current + 1) % slots.length)
        break
      case 'ArrowUp':
      case 'ArrowLeft':
        e.preventDefault()
        moveTo((current - 1 + slots.length) % slots.length)
        break
      case 'Home':
        e.preventDefault()
        moveTo(0)
        break
      case 'End':
        e.preventDefault()
        moveTo(slots.length - 1)
        break
    }
  }

  return (
    <Stack
      ref={ref}
      id="pax-delivery-window"
      tabIndex={-1}
      gap="xs"
      role="radiogroup"
      aria-label="Delivery window"
      onKeyDown={handleKeyDown}
    >
      {slots.map((slot) => (
        <SlotOption key={slot.id} slot={slot} selected={slot.id === selectedId} onSelect={onSelect} />
      ))}
    </Stack>
  )
})
