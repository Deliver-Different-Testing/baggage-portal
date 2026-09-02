import { useEffect, useRef } from 'react'
import { Anchor, Box, List, Text } from '@mantine/core'
import { AlertIcon } from '../Icon'
import { fieldTargetId, orderedErrors } from './confirmValidation'
import { tokens } from '../../styles/mantineTheme'

export function ConfirmErrorSummary({
  fieldErrors,
  submitCount,
}: {
  fieldErrors: Record<string, string>
  submitCount: number
}) {
  const ref = useRef<HTMLDivElement>(null)
  const entries = orderedErrors(fieldErrors)

  useEffect(() => {
    if (submitCount > 0 && ref.current) ref.current.focus()
  }, [submitCount])

  if (submitCount === 0 || entries.length === 0) return null

  return (
    <Box
      ref={ref}
      role="alert"
      tabIndex={-1}
      aria-labelledby="pax-error-summary-title"
      p="md"
      mb="md"
      style={{
        borderRadius: 'var(--mantine-radius-sm)',
        border: '1px solid var(--mantine-color-error)',
        backgroundColor: 'var(--mantine-color-red-light)',
        outlineOffset: 2,
      }}
    >
      <Box mb="xs" style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <AlertIcon size={18} color="var(--mantine-color-error)" />
        <Text id="pax-error-summary-title" fw={700} style={tokens.type.sectionTitle}>
          Check these before confirming
        </Text>
      </Box>
      <List spacing={4} size="sm" withPadding>
        {entries.map(({ key, message }) => (
          <List.Item key={key}>
            <Anchor
              href={`#${fieldTargetId(key)}`}
              underline="always"
              c="inherit"
              onClick={(event) => {
                event.preventDefault()
                const target = document.getElementById(fieldTargetId(key))
                target?.scrollIntoView?.({ block: 'center' })
                target?.focus()
              }}
            >
              {message}
            </Anchor>
          </List.Item>
        ))}
      </List>
    </Box>
  )
}
