import type { ReactNode } from 'react'
import { alpha, Box, Button, Container, Divider, Paper, Text, Title } from '@mantine/core'
import { PoweredByFooter } from './PoweredByFooter'

interface FullPageMessageProps {
  icon: ReactNode
  iconColor: string
  title: string
  description: string
  actionLabel?: string
  onAction?: () => void
  actionIcon?: ReactNode
}

export function FullPageMessage({
  icon,
  iconColor,
  title,
  description,
  actionLabel,
  onAction,
  actionIcon,
}: FullPageMessageProps) {
  const hasAction = !!actionLabel && !!onAction

  return (
    <Box
      style={{
        display: 'flex',
        flexDirection: 'column',
        minHeight: '100vh',
        background: 'var(--mantine-color-body)',
      }}
    >
      <Box
        style={{
          flex: 1,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          padding: 'var(--mantine-spacing-lg)',
        }}
      >
        <Container size="xs" w="100%">
          <Paper
            radius="lg"
            shadow="md"
            p={40}
            style={{
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              textAlign: 'center',
            }}
          >
            <Box
              style={{
                width: 72,
                height: 72,
                borderRadius: '50%',
                background: alpha(iconColor, 0.08),
                border: `1px solid ${alpha(iconColor, 0.16)}`,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                marginBottom: 'var(--mantine-spacing-lg)',
              }}
            >
              {icon}
            </Box>
            <Title order={3} fw={600} mb="xs">
              {title}
            </Title>
            <Text size="sm" c="dimmed" mb="xs" maw={320}>
              {description}
            </Text>
            {hasAction && (
              <>
                <Divider w="100%" my="lg" />
                <Button leftSection={actionIcon} onClick={onAction} size="md" px={32}>
                  {actionLabel}
                </Button>
              </>
            )}
          </Paper>
        </Container>
      </Box>

      <PoweredByFooter />
    </Box>
  )
}
