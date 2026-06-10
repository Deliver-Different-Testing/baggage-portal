import type { ReactNode } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Container from '@mui/material/Container'
import Divider from '@mui/material/Divider'
import Paper from '@mui/material/Paper'
import Typography from '@mui/material/Typography'
import { alpha } from '@mui/material/styles'

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
      sx={(theme) => ({
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        minHeight: '100vh',
        bgcolor: alpha(theme.palette.primary.main, 0.06),
        p: 3,
      })}
    >
      <Container maxWidth="xs">
        <Paper
          elevation={6}
          sx={(theme) => ({
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            textAlign: 'center',
            p: { xs: 4, sm: 5 },
            borderRadius: 3,
            border: `1px solid ${theme.palette.divider}`,
          })}
        >
          <Box
            sx={{
              width: 72,
              height: 72,
              borderRadius: '50%',
              bgcolor: alpha(iconColor, 0.08),
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              mb: 3,
              border: `1px solid ${alpha(iconColor, 0.16)}`,
            }}
          >
            {icon}
          </Box>
          <Typography variant="h5" sx={{ fontWeight: 600 }} gutterBottom>
            {title}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1, maxWidth: 320 }}>
            {description}
          </Typography>
          {hasAction && (
            <>
              <Divider sx={{ width: '100%', my: 3 }} />
              <Button
                variant="contained"
                startIcon={actionIcon}
                onClick={onAction}
                size="large"
                sx={{ px: 4 }}
              >
                {actionLabel}
              </Button>
            </>
          )}
        </Paper>
      </Container>
    </Box>
  )
}
