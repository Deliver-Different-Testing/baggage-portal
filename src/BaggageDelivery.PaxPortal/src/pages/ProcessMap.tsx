import { Box, Card, CardContent, Container, Stack, Typography } from '@mui/material'

type NodeKind = 'action' | 'system' | 'api' | 'decision'

const LANE_COLOURS: Record<string, string> = {
  Airport: '#fe811a',
  WorldTracer: '#00B0B9',
  Despatch: '#13b964',
  Passenger: '#824ae0',
  Post: '#13b964',
}

const NODE_COLOURS: Record<NodeKind, string> = {
  action: '#fe811a',
  system: '#13b964',
  api: '#00B0B9',
  decision: '#824ae0',
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
    <Container maxWidth="lg" sx={{ py: 6 }}>
      <Card>
        <CardContent>
          <Typography variant="h1" align="center">
            Lost baggage delivery process
          </Typography>
          <Typography variant="body2" align="center" color="text.secondary" sx={{ mt: 0.5, mb: 4 }}>
            Integration between SITA WorldTracer, Despatch, and the BaggageDelivery PWA
          </Typography>

          <Stack spacing={3}>
            {LANES.map((lane) => (
              <Box
                key={lane.key}
                sx={{
                  display: 'grid',
                  gridTemplateColumns: { xs: '1fr', md: '200px 1fr' },
                  gap: 2,
                  alignItems: 'center',
                  bgcolor: '#f8f7f7',
                  p: 2,
                  borderRadius: 1.5,
                }}
              >
                <Box sx={{ borderLeft: 4, borderColor: LANE_COLOURS[lane.key], pl: 1.5 }}>
                  <Typography sx={{ fontWeight: 700 }}>{lane.title}</Typography>
                  <Typography variant="caption" color="text.secondary">{lane.subtitle}</Typography>
                </Box>
                <Stack
                  direction={{ xs: 'column', md: 'row' }}
                  spacing={1.5}
                  useFlexGap
                  sx={{ flexWrap: 'wrap' }}
                >
                  {lane.nodes.map((node, idx) => (
                    <Box
                      key={node.title}
                      sx={{ display: 'flex', alignItems: 'center', gap: 1 }}
                    >
                      <Box
                        sx={{
                          minWidth: 140,
                          px: 1.5,
                          py: 1,
                          borderRadius: 1.5,
                          borderLeft: 4,
                          borderColor: NODE_COLOURS[node.kind],
                          bgcolor: 'common.white',
                          boxShadow: 1,
                        }}
                      >
                        <Typography variant="body2" sx={{ fontWeight: 700 }}>{node.title}</Typography>
                        <Typography variant="caption" color="text.secondary">{node.subtitle}</Typography>
                      </Box>
                      {idx < lane.nodes.length - 1 && (
                        <Typography color="primary" sx={{ display: { xs: 'none', md: 'block' } }}>→</Typography>
                      )}
                    </Box>
                  ))}
                </Stack>
              </Box>
            ))}
          </Stack>

          <Stack direction="row" spacing={3} useFlexGap sx={{ mt: 4, justifyContent: 'center', flexWrap: 'wrap' }}>
            {(['action', 'system', 'api', 'decision'] as const).map((kind) => (
              <Stack key={kind} direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                <Box sx={{ width: 14, height: 14, borderRadius: '50%', bgcolor: NODE_COLOURS[kind] }} />
                <Typography variant="caption" sx={{ textTransform: 'capitalize' }}>{kind}</Typography>
              </Stack>
            ))}
          </Stack>
        </CardContent>
      </Card>
    </Container>
  )
}
