import { Box, Divider, Text } from '@mantine/core'
import { Eyebrow } from './Docket'
import type { AddressDto, AtlOption } from '../api/client'
import { addressLines } from '../utils/address'

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
  fileReference?: string
}

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
