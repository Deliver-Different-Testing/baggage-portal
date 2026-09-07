import { Box, Collapse, List, Switch, Text, Textarea } from '@mantine/core'
import { AtlOptionList } from './AtlOptionList'
import { FormSection } from './FormSection'
import { fieldTargetId } from './confirmValidation'
import type { AtlOption } from '../../api/client'

const ACCESS_NOTES_MAX = 120

const CONSENT =
  'Leave my bag in a safe place if I am not home. I accept the risk of loss or damage once it has been left.'

const CONDITIONS = [
  'Weatherproof, so your bag stays dry',
  'Out of view of the street',
  'Safe and easy for the driver to reach',
]

export function AtlSection({
  options,
  selectedId,
  accessNotes,
  notesRequired,
  onToggle,
  onSelect,
  onNotesChange,
  showFieldError,
}: {
  options: AtlOption[]
  selectedId: number | null
  accessNotes: string
  notesRequired: boolean
  onToggle: (enabled: boolean) => void
  onSelect: (id: number) => void
  onNotesChange: (value: string) => void
  showFieldError: (key: string) => string | undefined
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
              <Text size="sm" c="dimmed" mt={6}>
                Pick a spot that is:
              </Text>
              <List size="sm" c="dimmed" spacing={2} mt={2} withPadding>
                {CONDITIONS.map((condition) => (
                  <List.Item key={condition}>{condition}</List.Item>
                ))}
              </List>
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
            error={showFieldError('accessNotes')}
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
}
