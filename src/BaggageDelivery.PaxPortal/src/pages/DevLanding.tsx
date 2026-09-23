import type { ReactNode } from 'react'
import { Link as RouterLink } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import {
  Badge,
  Box,
  Button,
  Center,
  Container,
  Group,
  Loader,
  Paper,
  Stack,
  Text,
  Title,
} from '@mantine/core'
import { ArrowRightIcon, LuggageIcon } from '../components/Icon'
import { PoweredByFooter } from '../components/PoweredByFooter'
import { getDevLinks } from '../api/pax'
import { useRedirectOnNotFound } from '../hooks/useRedirectOnNotFound'

export function DevLanding() {
  const links = useQuery({
    queryKey: ['dev', 'links'],
    queryFn: getDevLinks,
    retry: false,
  })

  useRedirectOnNotFound(links.error)

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
        <Container size="sm" w="100%">
          {links.isPending ? (
            <Center mih={240}>
              <Loader size="lg" />
            </Center>
          ) : links.data ? (
            <Paper radius="lg" shadow="md" p={40}>
              <Group justify="space-between" align="center" mb="xs">
                <Title order={3} fw={600}>
                  Baggage Delivery — dev links
                </Title>
                <Badge variant="light" radius="sm">
                  Job {links.data.jobId}
                </Badge>
              </Group>

              <Text size="sm" c="dimmed" mb="xl">
                These links are only served in Development. Passengers reach the app
                through a link like the ones below, never through this page.
              </Text>

              <Stack gap="lg">
                <DevLink
                  icon={<LuggageIcon size={20} color="var(--mantine-color-brand-6)" />}
                  label="Confirm delivery"
                  to={`/c/${links.data.token}`}
                  url={links.data.confirmUrl}
                />
              </Stack>
            </Paper>
          ) : (
            <Paper radius="lg" shadow="md" p={40}>
              <Title order={4} fw={600} mb="xs">
                Dev links unavailable
              </Title>
              <Text size="sm" c="dimmed">
                The API did not return magic links. Check that it is running and that
                BaggageDeliveryEncryptionKey and BaggageDeliveryEncryptionIV are set.
              </Text>
            </Paper>
          )}
        </Container>
      </Box>

      <PoweredByFooter />
    </Box>
  )
}

type DevLinkProps = {
  icon: ReactNode
  label: string
  to: string
  url: string
}

function DevLink({ icon, label, to, url }: DevLinkProps) {
  return (
    <Box>
      <Group gap="xs" mb={6}>
        {icon}
        <Text size="sm" fw={600}>
          {label}
        </Text>
      </Group>
      <Text
        size="xs"
        c="dimmed"
        mb="sm"
        p="xs"
        style={{
          borderRadius: 2,
          background: 'var(--dd-surface-container)',
          letterSpacing: '0.02em',
          wordBreak: 'break-all',
        }}
      >
        {url}
      </Text>
      <Button
        component={RouterLink}
        to={to}
        radius={9999}
        rightSection={<ArrowRightIcon size={16} />}
      >
        Open
      </Button>
    </Box>
  )
}