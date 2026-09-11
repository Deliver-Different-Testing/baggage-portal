import { Box, Stack, Text } from '@mantine/core'

const HINT =
  'To add a delivery note, use Additional details under Authority to leave.'

export function DeliveryNotesPanel({ notes }: { notes: string[] }) {
  return (
    <Stack gap={6} mt="xs">
      <Text size="sm" fw={600}>
        Delivery notes
      </Text>
      {notes.length > 0 && (
        <Box
          px="sm"
          py={10}
          style={{
            borderRadius: 'var(--mantine-radius-sm)',
            backgroundColor: 'var(--dd-surface-container-high)',
          }}
        >
          <Stack gap={4}>
            {notes.map((note, index) => (
              <Text key={`${index}-${note}`} size="sm">
                {note}
              </Text>
            ))}
          </Stack>
        </Box>
      )}
      <Text size="xs" c="dimmed">
        {HINT}
      </Text>
    </Stack>
  )
}
