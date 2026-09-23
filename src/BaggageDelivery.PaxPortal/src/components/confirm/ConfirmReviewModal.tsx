import { Box, Divider, Stack, Text } from '@mantine/core'
import { DocketIcon } from '../Icon'
import { DocketTile, Eyebrow } from '../Docket'
import { DialogFooter, DialogHeader, DialogShell, dialogContentBg } from '../dialog'
import { addressLines } from '../../utils/address'
import { tokens } from '../../styles/mantineTheme'
import type { AddressDto, TimeSlot } from '../../api/client'

export function ConfirmReviewModal({
  opened,
  onClose,
  onConfirm,
  submitting,
  online,
  slot,
  address,
  passengerPhone,
  title = 'Check your delivery details',
  subtitle = "We'll book this as soon as you confirm.",
  confirmLabel,
}: {
  opened: boolean
  onClose: () => void
  onConfirm: () => void
  submitting: boolean
  online: boolean
  slot: TimeSlot | undefined
  address: AddressDto
  passengerPhone: string
  title?: string
  subtitle?: string
  confirmLabel?: string
}) {
  return (
    <DialogShell
      opened={opened}
      onClose={onClose}
      label={title}
      closeOnClickOutside={!submitting}
      closeOnEscape={!submitting}
      transitionProps={{ transition: 'pop', duration: tokens.duration.fast }}
    >
      <DialogHeader
        icon={<DocketIcon size={22} />}
        title={title}
        subtitle={subtitle}
        onClose={onClose}
        closeDisabled={submitting}
      />

      <Box p="lg" bg={dialogContentBg}>
        <Stack gap="md">
          {slot && (
            <DocketTile label="Delivery window" variant="tint">
              <Text
                fw={700}
                style={{ fontSize: 20, lineHeight: 1.2, fontVariantNumeric: 'tabular-nums' }}
              >
                {slot.label}
              </Text>
              <Text size="sm">{slot.dayLabel}</Text>
            </DocketTile>
          )}

          <Box>
            <Eyebrow>Deliver to</Eyebrow>
            {addressLines(address).map((line, i) => (
              <Text key={`${i}-${line}`} size="sm" fw={500}>
                {line}
              </Text>
            ))}
          </Box>

          <Divider />

          <Box>
            <Eyebrow>Driver calls</Eyebrow>
            <Text size="sm" fw={500} style={{ fontVariantNumeric: 'tabular-nums' }}>
              {passengerPhone}
            </Text>
          </Box>
        </Stack>
      </Box>

      <DialogFooter
        onCancel={onClose}
        onConfirm={onConfirm}
        confirmLabel={confirmLabel ?? 'Confirm delivery'}
        cancelLabel="Edit details"
        confirmDisabled={!online}
        submitting={submitting}
      />
    </DialogShell>
  )
}
