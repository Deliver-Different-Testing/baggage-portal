/**
 * The delivery record, read back.
 *
 * The passenger sees this twice: once in the dialog that asks them to approve it,
 * and once on the confirmed screen as the receipt they keep. Those two were built
 * from two near-identical copies of the same markup, which is how the record you
 * approve and the record you are given drift apart. They are one component now, so
 * they cannot.
 */
import { Box, Divider, Text } from '@mantine/core'
import { Eyebrow } from './Docket'
import type { AddressDto, AtlOption } from '../api/client'
import { addressLines } from '../utils/address'

/**
 * The second address line, named. It carries the buzzer number and the gate code —
 * what the driver needs and the street address has nowhere to put — so it is read
 * back with the address on every screen rather than living only in the editor.
 * Nothing renders when the booking has none.
 */
export function ExtraDeliveryInfo({ value }: { value?: string | null }) {
  const text = (value ?? '').trim()
  if (!text) return null
  return (
    <Box mt={8}>
      <Eyebrow mb={2}>Extra delivery information</Eyebrow>
      <Text size="sm">{text}</Text>
    </Box>
  )
}

export interface DeliveryDocketProps {
  address: AddressDto
  passengerName: string
  passengerPhone: string
  passengerEmail: string
  atlOption: AtlOption | undefined
  accessNotes: string
  /** Rendered last, after the fields the passenger can act on. */
  fileReference?: string
}

/**
 * Address, contact and authority-to-leave, separated by keylines. Callers supply
 * their own heading and the delivery-window tile above it — those differ between the
 * dialog and the receipt; everything below does not.
 */
export function DeliveryDocket({
  address,
  passengerName,
  passengerPhone,
  passengerEmail,
  atlOption,
  accessNotes,
  fileReference,
}: DeliveryDocketProps) {
  return (
    <>
      <Box>
        <Eyebrow>Deliver to</Eyebrow>
        {addressLines(address).map((line, i) => (
          <Text key={`${i}-${line}`} size="sm">
            {line}
          </Text>
        ))}
        <ExtraDeliveryInfo value={address.line2} />
      </Box>

      <Divider />

      <Box>
        <Eyebrow>Contact</Eyebrow>
        <Text size="sm">{passengerName}</Text>
        <Text size="sm" style={{ fontVariantNumeric: 'tabular-nums' }}>
          {passengerPhone}
        </Text>
        <Text size="sm" style={{ wordBreak: 'break-word' }}>
          {passengerEmail}
        </Text>
      </Box>

      <Divider />

      <Box>
        <Eyebrow>Authority to leave</Eyebrow>
        <Text size="sm">{atlOption ? atlOption.name : 'Not authorised'}</Text>
        {/* The consequence of the off state, on the receipt as well as in the dialog.
            It used to appear only before confirming, so the record the passenger kept
            never mentioned that someone has to be home to take the bag. */}
        {atlOption ? (
          accessNotes.trim() && (
            <Text size="sm" c="dimmed" mt={2}>
              “{accessNotes.trim()}”
            </Text>
          )
        ) : (
          <Text size="sm" c="dimmed" mt={2}>
            Someone will need to be there to take the bag.
          </Text>
        )}
      </Box>

      {fileReference && (
        <>
          <Divider />

          <Box>
            <Eyebrow>File reference</Eyebrow>
            <Text size="sm" fw={600} style={{ fontVariantNumeric: 'tabular-nums' }}>
              {fileReference}
            </Text>
          </Box>
        </>
      )}
    </>
  )
}
