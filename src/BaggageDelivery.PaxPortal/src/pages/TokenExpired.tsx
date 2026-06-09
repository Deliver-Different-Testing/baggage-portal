import { Box, Container, Stack, Typography } from '@mui/material'

export function TokenExpired() {
  return (
    <Container maxWidth="sm" sx={{ pt: 8 }}>
      <Stack spacing={2} sx={{ alignItems: 'center', textAlign: 'center' }}>
        <Box
          sx={{
            width: 56, height: 56, borderRadius: '50%',
            bgcolor: 'warning.main', color: 'common.white',
            display: 'grid', placeItems: 'center', fontSize: 28,
          }}
        >
          !
        </Box>
        <Typography variant="h2">Link expired</Typography>
        <Typography color="text.secondary">
          This confirmation link is no longer valid. If you still need to confirm
          your baggage delivery, please contact our team — we will send you a new link.
        </Typography>
      </Stack>
    </Container>
  )
}
