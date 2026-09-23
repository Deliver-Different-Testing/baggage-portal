import { memo } from 'react'
import { Collapse, Group, Stack, TextInput } from '@mantine/core'
import { AddressAutocomplete } from '../AddressAutocomplete'
import { AddressGate } from './AddressGate'
import { DeliveryNotesPanel } from './DeliveryNotesPanel'
import { FormSection } from './FormSection'
import { fieldTargetId, type AddressErrors, type AddressLabels } from './confirmValidation'
import type { AddressDto } from '../../api/client'
import type { AddressDetail } from '../../types/address'

const ADDRESS_LINE2_MAX = 200

const optional = (label: string) => `${label} (optional)`

export const AddressSection = memo(function AddressSection({
  bookingId,
  address,
  deliveryNotes,
  labels,
  confirmed,
  editing,
  serverCountryError,
  onConfirmedChange,
  onToggleEdit,
  onAddressSelect,
  onPatch,
  onLocationPatch,
  onCountryChange,
  errors,
}: {
  bookingId: string
  address: AddressDto
  deliveryNotes: string[]
  labels: AddressLabels
  confirmed: boolean
  editing: boolean
  serverCountryError: string | null
  onConfirmedChange: (value: boolean) => void
  onToggleEdit: () => void
  onAddressSelect: (detail: AddressDetail) => void
  onPatch: (patch: Partial<AddressDto>) => void
  onLocationPatch: (patch: Partial<AddressDto>) => void
  onCountryChange: (country: string) => void
  errors: AddressErrors
}) {
  return (
    <FormSection title="Deliver to" subtitle="Check this is where you want your bag sent">
      <Stack gap="sm">
        <AddressGate
          address={address}
          confirmed={confirmed}
          editing={editing}
          onChange={onConfirmedChange}
          onToggleEdit={onToggleEdit}
          error={errors.addressConfirmed}
        />
        <Collapse expanded={editing}>
          <Stack gap="sm" pt="xs">
            <AddressAutocomplete bookingId={bookingId} onAddressSelect={onAddressSelect} />
            <TextInput
              label={optional(labels.line1)}
              value={address.line1 ?? ''}
              onChange={(e) => onPatch({ line1: e.currentTarget.value })}
              autoComplete="organization"
            />
            <Group gap="sm" align="flex-end" wrap="nowrap">
              <TextInput
                id={fieldTargetId('line3')}
                label={labels.line3}
                value={address.line3 ?? ''}
                onChange={(e) => onLocationPatch({ line3: e.currentTarget.value })}
                required
                style={{ flex: '0 1 42%' }}
                error={errors.line3}
              />
              <TextInput
                id={fieldTargetId('line4')}
                label={labels.line4}
                value={address.line4}
                onChange={(e) => onLocationPatch({ line4: e.currentTarget.value })}
                autoComplete="address-line1"
                required
                style={{ flex: 1 }}
                error={errors.line4}
              />
            </Group>
            <TextInput
              label={optional(labels.line2)}
              placeholder="Apartment number, gate code, where to find the door"
              value={address.line2 ?? ''}
              onChange={(e) => onPatch({ line2: e.currentTarget.value })}
              autoComplete="address-line2"
              maxLength={ADDRESS_LINE2_MAX}
            />
            <TextInput
              id={fieldTargetId('line5')}
              label={labels.line5}
              value={address.line5}
              onChange={(e) => onLocationPatch({ line5: e.currentTarget.value })}
              autoComplete="address-level3"
              required
              error={errors.line5}
            />
            <Group gap="sm" align="flex-end" grow wrap="nowrap">
              <TextInput
                id={fieldTargetId('line6')}
                label={labels.line6}
                value={address.line6}
                onChange={(e) => onLocationPatch({ line6: e.currentTarget.value })}
                autoComplete="address-level2"
                required
                error={errors.line6}
              />
              <TextInput
                id={fieldTargetId('line7')}
                label={labels.line7}
                value={address.line7 ?? ''}
                onChange={(e) => onLocationPatch({ line7: e.currentTarget.value })}
                autoComplete="postal-code"
                autoCapitalize="characters"
                required
                error={errors.line7}
              />
            </Group>
            <TextInput
              id={fieldTargetId('country')}
              label={labels.country}
              value={address.country}
              onChange={(e) => onCountryChange(e.currentTarget.value)}
              autoComplete="country-name"
              required
              error={serverCountryError ?? errors.country}
            />
          </Stack>
        </Collapse>
        <DeliveryNotesPanel notes={deliveryNotes} />
      </Stack>
    </FormSection>
  )
})
