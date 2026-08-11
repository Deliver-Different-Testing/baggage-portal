import { Box, Card, Container, Grid, Group, Stack, Text, Title } from '@mantine/core'

type NodeKind = 'action' | 'system' | 'api' | 'decision'

// Categorical lane/node colours drawn from the DFRNT accent ramps (no literal hex;
// no purple/teal — reflex-blue and stone stand in for the old WorldTracer teal and
// Passenger/decision purple).
const NODE_COLOURS: Record<NodeKind, string> = {
  action: 'var(--mantine-color-orange-6)',
  system: 'var(--mantine-color-green-6)',
  api: 'var(--mantine-color-reflex-6)',
  decision: 'var(--mantine-color-ink-6)',
}

const LANE_COLOURS: Record<string, string> = {
  Airport: 'var(--mantine-color-orange-6)',
  WorldTracer: 'var(--mantine-color-reflex-6)',
  Despatch: 'var(--mantine-color-green-6)',
  Passenger: 'var(--mantine-color-brand-6)',
  Post: 'var(--mantine-color-green-6)',
}

const LANES: Array<{
  title: string
  subtitle: string
  key: keyof typeof LANE_COLOURS
  nodes: Array<{ title: string; subtitle: string; kind: NodeKind }>
}> = [
  {
    title: 'Airport Operations',
    subtitle: 'Menzies Aviation',
    key: 'Airport',
    nodes: [
      { title: 'Bag arrives', subtitle: 'Lost baggage received', kind: 'action' },
      { title: 'Bag cleared', subtitle: 'Menzies processes', kind: 'action' },
      { title: 'Trigger BDO', subtitle: 'Request delivery order', kind: 'action' },
    ],
  },
  {
    title: 'World Tracer',
    subtitle: 'SITA',
    key: 'WorldTracer',
    nodes: [
      { title: 'Create BDO', subtitle: 'Baggage Delivery Order generated', kind: 'system' },
      { title: 'API call', subtitle: 'Send BDO to Urgent', kind: 'api' },
      { title: 'File reference', subtitle: 'WT tracking number', kind: 'system' },
    ],
  },
  {
    title: 'Despatch / IntegrationManager',
    subtitle: 'Urgent Couriers',
    key: 'Despatch',
    nodes: [
      { title: 'Receive BDO', subtitle: 'Via WorldTracer poller', kind: 'api' },
      { title: 'Create job', subtitle: 'Status: ON HOLD', kind: 'system' },
      { title: 'Mint magic link', subtitle: 'BaggageDelivery API', kind: 'api' },
      { title: 'Send notification', subtitle: 'SMS / Email to passenger', kind: 'system' },
    ],
  },
  {
    title: 'Passenger PWA',
    subtitle: 'BaggageDelivery',
    key: 'Passenger',
    nodes: [
      { title: 'Open link', subtitle: 'Magic-link session', kind: 'action' },
      { title: 'Address OK?', subtitle: 'Update if needed', kind: 'decision' },
      { title: 'Pick time slot', subtitle: 'Next 3-6 delivery slots', kind: 'action' },
      { title: 'ATL options', subtitle: 'Authority to leave', kind: 'action' },
      { title: 'Confirm', subtitle: 'Submit booking', kind: 'action' },
    ],
  },
  {
    title: 'Post-confirmation',
    subtitle: 'System updates',
    key: 'Post',
    nodes: [
      { title: 'Update job', subtitle: 'READY FOR DISPATCH', kind: 'system' },
      { title: 'Update WT', subtitle: 'IntegrationManager outbox', kind: 'api' },
      { title: 'Courier assigned', subtitle: 'Run scheduled', kind: 'system' },
      { title: 'Live tracking', subtitle: 'Real-time updates', kind: 'system' },
    ],
  },
]

export function ProcessMap() {
  return (
    <Container size="lg" py={48}>
      <Card>
        <Title order={1} ta="center">
          Lost baggage delivery process
        </Title>
        <Text size="sm" ta="center" c="dimmed" mt={4} mb="xl">
          Integration between SITA WorldTracer, Despatch, and the BaggageDelivery PWA
        </Text>

        <Stack gap="md">
          {LANES.map((lane) => (
            <Grid
              key={lane.key}
              align="center"
              gap="md"
              p="md"
              style={{
                background: 'var(--dd-surface)',
                borderRadius: 'var(--mantine-radius-md)',
              }}
            >
              <Grid.Col span={{ base: 12, md: 3 }}>
                <Box
                  style={{
                    borderLeft: `4px solid ${LANE_COLOURS[lane.key]}`,
                    paddingLeft: 12,
                  }}
                >
                  <Text fw={700}>{lane.title}</Text>
                  <Text size="xs" c="dimmed">
                    {lane.subtitle}
                  </Text>
                </Box>
              </Grid.Col>
              <Grid.Col span={{ base: 12, md: 9 }}>
                <Group gap="sm" wrap="wrap">
                  {lane.nodes.map((node, idx) => (
                    <Group key={node.title} gap={8} wrap="nowrap">
                      <Box
                        style={{
                          minWidth: 140,
                          padding: '8px 12px',
                          borderRadius: 'var(--mantine-radius-md)',
                          borderLeft: `4px solid ${NODE_COLOURS[node.kind]}`,
                          background: 'var(--dd-surface-container)',
                          boxShadow: 'var(--mantine-shadow-xs)',
                        }}
                      >
                        <Text size="sm" fw={700}>
                          {node.title}
                        </Text>
                        <Text size="xs" c="dimmed">
                          {node.subtitle}
                        </Text>
                      </Box>
                      {idx < lane.nodes.length - 1 && (
                        <Text c="brand" visibleFrom="md">
                          →
                        </Text>
                      )}
                    </Group>
                  ))}
                </Group>
              </Grid.Col>
            </Grid>
          ))}
        </Stack>

        <Group gap="xl" justify="center" wrap="wrap" mt="xl">
          {(['action', 'system', 'api', 'decision'] as const).map((kind) => (
            <Group key={kind} gap={8} align="center">
              <Box
                style={{
                  width: 14,
                  height: 14,
                  borderRadius: '50%',
                  background: NODE_COLOURS[kind],
                }}
              />
              <Text size="xs" tt="capitalize">
                {kind}
              </Text>
            </Group>
          ))}
        </Group>
      </Card>
    </Container>
  )
}
