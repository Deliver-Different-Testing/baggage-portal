import { Box, Button, Container, Text } from '@mantine/core'
import { ArrowRightIcon } from '../Icon'
import { tokens } from '../../styles/mantineTheme'

export function ConfirmActionBar({
  summary,
  disabled,
  onReview,
  label = 'Review delivery',
  onCancel,
  cancelLabel,
}: {
  summary: string | null
  disabled: boolean
  onReview: () => void
  label?: string
  onCancel?: () => void
  cancelLabel?: string
}) {
  return (
    <Box className="pax-action-bar">
      <Container size={tokens.hero.measure} px="md">
        {summary && (
          <Text size="sm" c="dimmed" mb={8} ta="center" lineClamp={1}>
            {summary}
          </Text>
        )}
        <Button
          size="lg"
          fullWidth
          disabled={disabled}
          onClick={onReview}
          rightSection={<ArrowRightIcon size={18} />}
          style={tokens.button.primary}
        >
          {label}
        </Button>
        {onCancel && cancelLabel && (
          <Button variant="subtle" size="sm" fullWidth mt={6} onClick={onCancel}>
            {cancelLabel}
          </Button>
        )}
      </Container>
    </Box>
  )
}
