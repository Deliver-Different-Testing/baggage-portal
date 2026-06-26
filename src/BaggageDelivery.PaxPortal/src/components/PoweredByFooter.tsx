import Box from '@mui/material/Box'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import type { SxProps, Theme } from '@mui/material/styles'

interface PoweredByFooterProps {
  sx?: SxProps<Theme>
}

export function PoweredByFooter({ sx }: PoweredByFooterProps) {
  return (
    <Box
      component="footer"
      sx={[{ width: '100%', py: 3, px: 2 }, ...(Array.isArray(sx) ? sx : [sx])]}
    >
      <Stack
        direction="row"
        spacing={0.75}
        sx={{ alignItems: 'center', justifyContent: 'center' }}
      >
        <Typography
          variant="caption"
          sx={{ color: 'text.secondary', fontSize: 11.5, letterSpacing: '0.04em' }}
        >
          Powered by
        </Typography>
        <Box
          component="img"
          src="/dfrnt-logo.png"
          alt="Deliver DFRNT"
          sx={{ height: 22, width: 'auto', display: 'block' }}
        />
      </Stack>
    </Box>
  )
}
