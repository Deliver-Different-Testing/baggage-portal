import { Anchor, Button, Group, Stack, Text } from '@mantine/core'
import { FormSection } from './FormSection'
import { AlertIcon } from '../Icon'
import { addressLines } from '../../utils/address'
import type { AddressDto } from '../../api/client'

export function AddressUnserviceableCard({
  address,
  airlineLabel,
  supportPhone,
  requested,
  submitting,
  onRequestHelp,
}: {
  address: AddressDto
  airlineLabel: string
  supportPhone: string
  requested: boolean
  submitting: boolean
  onRequestHelp: () => void
}) {
  const lines = addressLines(address)

  return (
    <FormSection
      title="We cannot deliver there"
      subtitle="This address is outside the areas we cover"
      emphasis="primary"
    >
      <Stack
        gap="sm"
        id="pax-delivery-service"
        tabIndex={-1}
        role="status"
        p="md"
        style={{
          borderRadius: 'var(--mantine-radius-md)',
          border: '1px solid var(--mantine-color-default-border)',
          backgroundColor: 'var(--dd-surface-container)',
        }}
      >
        <Group gap="xs" align="flex-start" wrap="nowrap">
          <AlertIcon size={20} />
          <Text size="sm" style={{ flex: 1 }}>
            We don&apos;t have a delivery service that covers{' '}
            <Text span fw={700}>
              {lines.join(', ')}
            </Text>
            .
          </Text>
        </Group>

        {requested ? (
          <Text size="sm" fw={700}>
            Thanks — we&apos;ve asked {airlineLabel} to contact you about this address.
          </Text>
        ) : (
          <>
            <Text size="sm">
              You can change the address above, or ask {airlineLabel} to get in touch and sort it
              out with you directly.
            </Text>
            <Button
              variant="filled"
              radius={9999}
              loading={submitting}
              onClick={onRequestHelp}
              style={{ alignSelf: 'flex-start' }}
            >
              Ask {airlineLabel} to contact me
            </Button>
          </>
        )}

        {supportPhone && (
          <Text size="sm" c="dimmed">
            Or call us on{' '}
            <Anchor href={`tel:${supportPhone.replace(/\s/g, '')}`} size="sm">
              {supportPhone}
            </Anchor>
            .
          </Text>
        )}
      </Stack>
    </FormSection>
  )
}
