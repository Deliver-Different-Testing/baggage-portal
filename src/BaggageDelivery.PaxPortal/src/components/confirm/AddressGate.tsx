import { Box, Button, Checkbox, Group, Text } from '@mantine/core'
import { ExtraDeliveryInfo } from '../DeliveryDocket'
import { fieldTargetId } from './confirmValidation'
import { addressLines } from '../../utils/address'
import type { AddressDto } from '../../api/client'

export function AddressGate({
  address,
  confirmed,
  editing,
  onChange,
  onToggleEdit,
  error,
}: {
  address: AddressDto
  confirmed: boolean
  editing: boolean
  onChange: (value: boolean) => void
  onToggleEdit: () => void
  error?: string
}) {
  const accent = error
    ? 'var(--mantine-color-error)'
    : confirmed
      ? 'var(--mantine-color-brand-filled)'
      : 'var(--mantine-color-default-border)'

  return (
    <Box
      px="sm"
      py={10}
      style={{
        borderRadius: 'var(--mantine-radius-sm)',
        backgroundColor: 'var(--dd-surface-container-high)',
        border: `1px solid ${accent}`,
        borderLeft: `3px solid ${accent}`,
      }}
    >
      <Group gap="sm" align="flex-start" wrap="nowrap">
        <Box style={{ flex: 1, minWidth: 0 }}>
          {!editing && (
            <>
              {addressLines(address).map((line, i) => (
                <Text key={`${i}-${line}`} size="sm" fw={500} style={{ lineHeight: 1.35 }}>
                  {line}
                </Text>
              ))}
              <ExtraDeliveryInfo value={address.line2} />
            </>
          )}
          <Checkbox
            id={fieldTargetId('addressConfirmed')}
            mt={editing ? 0 : 8}
            checked={confirmed}
            onChange={(e) => onChange(e.currentTarget.checked)}
            label="This address is correct"
            description="We'll deliver your bag here."
            error={error}
          />
        </Box>
        <Button variant="subtle" size="compact-sm" onClick={onToggleEdit} style={{ flexShrink: 0 }}>
          {editing ? 'Done' : 'Edit'}
        </Button>
      </Group>
    </Box>
  )
}
