import { Box, Card, Container, Group, Skeleton, Stack } from '@mantine/core'
import { ConfirmHeroSkeleton } from '../ConfirmHero'
import { tokens } from '../../styles/mantineTheme'

export function ConfirmSkeleton() {
  return (
    <Box mih="100vh" pb={{ base: 112, sm: 128 }}>
      <ConfirmHeroSkeleton />
      <Container
        size={tokens.hero.measure}
        px={0}
        mt={tokens.hero.overlap}
        style={{ position: 'relative', zIndex: 1 }}
      >
        <Box px={{ base: 12, sm: 0 }}>
          <Card p="lg" aria-busy="true" aria-label="Loading your booking">
            <Stack gap="lg">
              {[0, 1, 2, 3].map((row) => (
                <Group key={row} gap="sm" align="center" wrap="nowrap">
                  <Skeleton height={32} width={32} radius="sm" />
                  <Skeleton height={14} width={row % 2 ? 190 : 140} radius="sm" />
                </Group>
              ))}
            </Stack>
          </Card>
        </Box>
      </Container>
    </Box>
  )
}
