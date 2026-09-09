import { memo } from 'react'
import { Box, Collapse, List, Switch, Text, Textarea } from '@mantine/core'
import { AtlOptionList } from './AtlOptionList'
import { FormSection } from './FormSection'
import { fieldTargetId, type AtlErrors } from './confirmValidation'
import type { AtlOption } from '../../api/client'

const ACCESS_NOTES_MAX = 120

const CONSENT =
  "Leave my baggage in a safe place if I'm not available. I accept the risk of loss or damage once it has been left."

const CONDITIONS = [
  'Pick a location that is waterproof so your bag stays dry.',
  'Pick a location that is out of view of the street.',
  'Pick a location that is safe and easy for the driver to reach.',
  'If at a motel or hotel, inform reception that your bag is on its way.',
]

const DELIVERY_PROOF =
  "As soon as we've delivered your bag to this location, you will receive a message confirming where we've left it and a photograph of the bag at that location."

export const AtlSection = memo(function AtlSection({
  options,
  selectedId,
  accessNotes,
  notesRequired,
  onToggle,
  onSelect,
  onNotesChange,
  errors,
}: {
  options: AtlOption[]
  selectedId: number | null
  accessNotes: string
  notesRequired: boolean
  onToggle: (enabled: boolean) => void
  onSelect: (id: number) => void
  onNotesChange: (value: string) => void
  errors: AtlErrors
}) {
  const enabled = selectedId !== null

  return (
    <FormSection
      title="Authority to leave"
      subtitle="We can leave your bag in a safe place instead of waiting for someone"
      action={
        <Switch
          checked={enabled}
          disabled={options.length === 0}
          aria-label="Authority to leave"
          onChange={(e) => onToggle(e.currentTarget.checked)}
        />
      }
    >
      <Collapse expanded={enabled}>
        <Box pt="xs">
          <Box
            px="sm"
            py={10}
            mb="sm"
            style={{
              borderRadius: 'var(--mantine-radius-sm)',
              backgroundColor: 'var(--dd-surface-container-high)',
              borderLeft: '3px solid var(--mantine-color-brand-filled)',
            }}
          >
            <Text size="sm" fw={600}>
              {CONSENT}
            </Text>
            <Box component="details" mt={6}>
              <Text component="summary" size="sm" style={{ cursor: 'pointer' }}>
                What this means
              </Text>
              <List size="sm" c="dimmed" spacing={2} mt={6} withPadding>
                {CONDITIONS.map((condition) => (
                  <List.Item key={condition}>{condition}</List.Item>
                ))}
              </List>
              <Text size="sm" c="dimmed" mt={6}>
                {DELIVERY_PROOF}
              </Text>
            </Box>
          </Box>

          <Text size="sm" fw={500} mb={6}>
            Where should we leave it?
          </Text>
          <AtlOptionList options={options} selectedId={selectedId} onSelect={onSelect} />

          <Textarea
            id={fieldTargetId('accessNotes')}
            label={notesRequired ? 'Additional details' : 'Additional details (optional)'}
            required={notesRequired}
            value={accessNotes}
            onChange={(e) => onNotesChange(e.currentTarget.value)}
            error={errors.accessNotes}
            maxLength={ACCESS_NOTES_MAX}
            description={`${accessNotes.length}/${ACCESS_NOTES_MAX}`}
            autosize
            minRows={2}
            mt="sm"
          />
        </Box>
      </Collapse>
    </FormSection>
  )
})
