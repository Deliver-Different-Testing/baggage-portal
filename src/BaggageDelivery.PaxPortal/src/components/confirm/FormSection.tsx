import { Box, Group, Text, Title, type BoxProps } from '@mantine/core'
import { useId, type ReactNode } from 'react'
import { tokens } from '../../styles/mantineTheme'

export function FormSection({
  title,
  subtitle,
  action,
  emphasis = 'default',
  children,
  ...boxProps
}: {
  title: string
  subtitle?: string
  action?: ReactNode
  emphasis?: 'default' | 'primary'
  children: ReactNode
} & BoxProps) {
  const headingId = useId()

  return (
    <Box
      component="section"
      aria-labelledby={headingId}
      p="lg"
      style={
        emphasis === 'primary'
          ? { boxShadow: 'inset 3px 0 0 0 var(--mantine-color-brand-filled)' }
          : undefined
      }
      {...boxProps}
    >
      <Group gap="sm" align="center" mb="md" wrap="nowrap">
        <Box style={{ flex: 1, minWidth: 0 }}>
          <Title order={2} id={headingId} style={tokens.type.sectionTitle}>
            {title}
          </Title>
          {subtitle && (
            <Text size="sm" c="dimmed" fw={400}>
              {subtitle}
            </Text>
          )}
        </Box>
        {action}
      </Group>
      {children}
    </Box>
  )
}
