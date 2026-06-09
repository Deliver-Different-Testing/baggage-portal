import { useEffect } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import {
  Avatar,
  Box,
  Card,
  CardContent,
  Chip,
  Container,
  Divider,
  Grid,
  Stack,
  Typography,
} from '@mui/material'
import {
  Timeline,
  TimelineConnector,
  TimelineContent,
  TimelineDot,
  TimelineItem,
  TimelineOppositeContent,
  TimelineSeparator,
} from '@mui/lab'
import { getTracking } from '../api/pax'
import type { TrackingTimeline } from '../api/client'

export function Tracking() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()

  const tracking = useQuery({
    queryKey: ['pax', 'tracking', id],
    queryFn: () => getTracking(id ?? ''),
    enabled: !!id,
    refetchInterval: 30_000,
    retry: false,
  })

  useEffect(() => {
    if (tracking.error && (tracking.error as { normalisedKind?: string })?.normalisedKind === 'not_found') {
      navigate('/expired', { replace: true })
    }
  }, [tracking.error, navigate])

  if (!id) return null
  if (tracking.isLoading) {
    return (
      <Container sx={{ pt: 8 }}>
        <Typography color="text.secondary">Loading…</Typography>
      </Container>
    )
  }

  return (
    <Container maxWidth="lg" sx={{ pt: 4, pb: 8 }}>
      <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h1">Baggage tracking</Typography>
        {tracking.data && <StatusChip status={tracking.data.currentStatus} />}
      </Stack>
      <Grid container spacing={3}>
        <Grid size={{ xs: 12, md: 8 }}>
          <Card>
            <CardContent>
              <Typography variant="h3">Delivery timeline</Typography>
              <Divider sx={{ my: 2 }} />
              {tracking.data ? <TimelineList data={tracking.data} /> : <Typography color="text.secondary">Loading…</Typography>}
            </CardContent>
          </Card>
        </Grid>
        <Grid size={{ xs: 12, md: 4 }}>
          {tracking.data?.courierFirstName && (
            <Card sx={{ mb: 2 }}>
              <CardContent>
                <Typography variant="h3">Driver</Typography>
                <Divider sx={{ my: 1.5 }} />
                <Stack direction="row" spacing={2} sx={{ alignItems: 'center' }}>
                  <Avatar sx={{ bgcolor: 'secondary.main', color: 'primary.main' }}>
                    {tracking.data.courierFirstName.charAt(0)}
                  </Avatar>
                  <Box>
                    <Typography sx={{ fontWeight: 700 }}>{tracking.data.courierFirstName}</Typography>
                    {tracking.data.vehicleLabel && (
                      <Typography variant="body2" color="text.secondary">
                        {tracking.data.vehicleLabel}
                      </Typography>
                    )}
                  </Box>
                </Stack>
              </CardContent>
            </Card>
          )}
          <Card>
            <CardContent>
              <Typography variant="h3">ETA</Typography>
              <Divider sx={{ my: 1.5 }} />
              {tracking.data?.etaWindowStartUtc && tracking.data.etaWindowEndUtc ? (
                <Typography>
                  {new Date(tracking.data.etaWindowStartUtc).toLocaleString()} —{' '}
                  {new Date(tracking.data.etaWindowEndUtc).toLocaleString()}
                </Typography>
              ) : (
                <Typography color="text.secondary">Pending</Typography>
              )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Container>
  )
}

function StatusChip({ status }: { status: string }) {
  const colour =
    status === 'Delivered' ? 'success' :
    status === 'OutForDelivery' ? 'warning' :
    'secondary'
  return <Chip label={status} color={colour} sx={{ fontWeight: 700 }} />
}

function TimelineList({ data }: { data: TrackingTimeline }) {
  const events = [...data.events].sort((a, b) => +new Date(a.atUtc) - +new Date(b.atUtc))
  return (
    <Timeline sx={{ p: 0, m: 0 }}>
      {events.map((event, idx) => (
        <TimelineItem key={`${event.status}-${event.atUtc}`}>
          <TimelineOppositeContent sx={{ flex: 0.3, color: 'text.secondary' }}>
            {new Date(event.atUtc).toLocaleString()}
          </TimelineOppositeContent>
          <TimelineSeparator>
            <TimelineDot color={idx === events.length - 1 ? 'secondary' : 'success'} />
            {idx < events.length - 1 && <TimelineConnector />}
          </TimelineSeparator>
          <TimelineContent>
            <Typography sx={{ fontWeight: 700 }}>{event.status}</Typography>
            {event.description && (
              <Typography variant="body2" color="text.secondary">{event.description}</Typography>
            )}
            {event.locationLabel && (
              <Typography variant="caption" color="text.secondary">{event.locationLabel}</Typography>
            )}
          </TimelineContent>
        </TimelineItem>
      ))}
    </Timeline>
  )
}
