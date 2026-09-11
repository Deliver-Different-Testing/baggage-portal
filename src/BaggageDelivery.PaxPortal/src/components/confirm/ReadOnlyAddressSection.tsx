import { Stack, Text } from '@mantine/core'
import { DeliveryNotesPanel } from './DeliveryNotesPanel'
import { FormSection } from './FormSection'
import { addressLines } from '../../utils/address'
import type { AddressDto } from '../../api/client'

export function ReadOnlyAddressSection({
  address,
  deliveryNotes,
  supportPhone,
}: {
  address: AddressDto
  deliveryNotes: string[]
  supportPhone: string
}) {
  return (
    <FormSection title="Deliver to" subtitle="Call us if the address itself needs to change">
      <Stack gap={2}>
        {addressLines(address).map((line) => (
          <Text key={line} fw={600}>
            {line}
          </Text>
        ))}
        {supportPhone && (
          <Text size="sm" c="dimmed" mt={6}>
            To send your bag somewhere else, call {supportPhone}.
          </Text>
        )}
        <DeliveryNotesPanel notes={deliveryNotes} />
      </Stack>
    </FormSection>
  )
}
