import { useEffect, useMemo } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import Avatar from '@mui/material/Avatar'
import Box from '@mui/material/Box'
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Chip from '@mui/material/Chip'
import CircularProgress from '@mui/material/CircularProgress'
import Container from '@mui/material/Container'
import Grid from '@mui/material/Grid'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { alpha, keyframes } from '@mui/material/styles'
import Timeline from '@mui/lab/Timeline'
import TimelineConnector from '@mui/lab/TimelineConnector'
import TimelineContent from '@mui/lab/TimelineContent'
import TimelineDot from '@mui/lab/TimelineDot'
import TimelineItem from '@mui/lab/TimelineItem'
import TimelineOppositeContent from '@mui/lab/TimelineOppositeContent'
import TimelineSeparator from '@mui/lab/TimelineSeparator'
import LocalShippingRoundedIcon from '@mui/icons-material/LocalShippingRounded'
import CheckCircleRoundedIcon from '@mui/icons-material/CheckCircleRounded'
import HourglassEmptyRoundedIcon from '@mui/icons-material/HourglassEmptyRounded'
import AccessTimeRoundedIcon from '@mui/icons-material/AccessTimeRounded'
import LuggageRoundedIcon from '@mui/icons-material/LuggageRounded'
import { getTracking } from '../api/pax'
import type { TrackingTimeline } from '../api/client'
import { PoweredByFooter } from '../components/PoweredByFooter'

const pulse = keyframes`
  0% { box-shadow: 0 0 0 0 currentColor; opacity: 0.6; }
  70% { box-shadow: 0 0 0 8px transparent; opacity: 0; }
  100% { box-shadow: 0 0 0 0 transparent; opacity: 0; }
`

const STATUS_INFO = {
  Delivered: {
    icon: <CheckCircleRoundedIcon sx={{ fontSize: 16 }} />,
    label: 'Delivered',
    headline: 'Your bag has arrived',
    description: "Thanks for using our delivery service. We hope it arrived safely.",
  },
  OutForDelivery: {
    icon: <LocalShippingRoundedIcon sx={{ fontSize: 16 }} />,
    label: 'On the way',
    headline: 'Your bag is on the way',
    description: "Our driver is heading to your delivery address now.",
  },
} as const

const PENDING_STATUS = {
  icon: <HourglassEmptyRoundedIcon sx={{ fontSize: 16 }} />,
  headline: 'Preparing your delivery',
  description: "We'll keep this page updated as your bag moves through our network.",
}

export function Tracking() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()

  const tracking = useQuery({
    queryKey: ['pax', 'tracking', id],
    queryFn: () => getTracking(id ?? ''),
    enabled: !!id,
    refetchInterval: (query) =>
      query.state.data?.currentStatus === 'Delivered' ? false : 30_000,
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
      <Box sx={{ minHeight: '100vh', display: 'grid', placeItems: 'center' }}>
        <Stack spacing={2} sx={{ alignItems: 'center' }}>
          <CircularProgress size={32} />
          <Typography variant="body2" color="text.secondary">
            Loading tracking…
          </Typography>
        </Stack>
      </Box>
    )
  }

  const status = tracking.data?.currentStatus ?? 'Pending'
  const statusInfo = getStatusInfo(status)

  return (
    <Box sx={{ minHeight: '100vh' }}>
      <Box
        sx={(theme) => ({
          backgroundColor: theme.palette.primary.main,
          color: theme.palette.primary.contrastText,
          px: { xs: 2.5, sm: 4 },
          pt: { xs: 4, sm: 5 },
          pb: { xs: 6, sm: 7 },
        })}
      >
        <Container maxWidth="lg" sx={{ mx: 'auto', px: '0 !important', position: 'relative' }}>
          <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', mb: 3 }}>
            <Box
              sx={{
                width: 36,
                height: 36,
                borderRadius: '12px',
                bgcolor: 'rgba(255,255,255,0.18)',
                display: 'grid',
                placeItems: 'center',
                flexShrink: 0,
              }}
            >
              <LuggageRoundedIcon sx={{ fontSize: 20 }} />
            </Box>
            <Box sx={{ flex: 1, minWidth: 0 }}>
              <Typography
                variant="caption"
                sx={{
                  opacity: 0.8,
                  letterSpacing: '0.08em',
                  textTransform: 'uppercase',
                  fontWeight: 600,
                  fontSize: 10.5,
                  display: 'block',
                  lineHeight: 1.2,
                }}
              >
                Baggage tracking
              </Typography>
              <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center', mt: 0.25 }}>
                <Box
                  sx={(theme) => ({
                    width: 6,
                    height: 6,
                    borderRadius: '50%',
                    bgcolor: theme.palette.success.light,
                    animation: `${pulse} 2s ease-out infinite`,
                  })}
                />
                <Typography variant="caption" sx={{ opacity: 0.85, fontWeight: 600, fontSize: 12 }}>
                  Live · updates every 30s
                </Typography>
              </Stack>
            </Box>
          </Stack>

          <Chip
            icon={statusInfo.icon}
            label={statusInfo.label}
            sx={{
              bgcolor: 'rgba(255,255,255,0.2)',
              color: 'inherit',
              fontWeight: 700,
              letterSpacing: '0.04em',
              borderRadius: 999,
              mb: 2,
              '& .MuiChip-icon': { color: 'inherit' },
            }}
          />

          <Typography
            variant="h1"
            sx={{
              fontSize: { xs: 32, sm: 42 },
              lineHeight: 1.05,
              color: 'inherit',
              fontWeight: 700,
              letterSpacing: '-0.03em',
              mb: 1,
            }}
          >
            {statusInfo.headline}
          </Typography>
          <Typography variant="body1" sx={{ opacity: 0.85, maxWidth: 480 }}>
            {statusInfo.description}
          </Typography>
        </Container>
      </Box>

      <Container
        maxWidth="lg"
        sx={{ mx: 'auto', mt: { xs: -3.5, sm: -4 }, position: 'relative', pb: 6 }}
      >
        <Grid container spacing={{ xs: 2, md: 3 }}>
          <Grid size={{ xs: 12, md: 4 }}>
            <Stack spacing={{ xs: 2, md: 3 }}>
              <EtaCard
                startUtc={tracking.data?.etaWindowStartUtc}
                endUtc={tracking.data?.etaWindowEndUtc}
              />
              {tracking.data?.courierFirstName && (
                <DriverCard
                  name={tracking.data.courierFirstName}
                  vehicle={tracking.data.vehicleLabel}
                />
              )}
            </Stack>
          </Grid>
          <Grid size={{ xs: 12, md: 8 }}>
            <Card>
              <CardContent>
                <Typography variant="h3" sx={{ mb: 0.5 }}>
                  Delivery timeline
                </Typography>
                <Typography variant="body2" color="text.secondary" sx={{ mb: 2.5 }}>
                  Most recent updates first
                </Typography>
                {tracking.data ? (
                  <TimelineList data={tracking.data} nowMs={tracking.dataUpdatedAt} />
                ) : (
                  <Typography color="text.secondary">No updates yet.</Typography>
                )}
              </CardContent>
            </Card>
          </Grid>
        </Grid>
      </Container>

      <PoweredByFooter />
    </Box>
  )
}

function getStatusInfo(status: string): {
  icon: React.ReactElement
  label: string
  headline: string
  description: string
} {
  if (status in STATUS_INFO) {
    return STATUS_INFO[status as keyof typeof STATUS_INFO]
  }
  return { ...PENDING_STATUS, label: status }
}

function EtaCard({
  startUtc,
  endUtc,
}: {
  startUtc?: string | null
  endUtc?: string | null
}) {
  const hasEta = !!startUtc && !!endUtc
  return (
    <Card>
      <CardContent>
        <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', mb: 2 }}>
          <Box
            sx={(theme) => ({
              width: 36,
              height: 36,
              borderRadius: theme.tokens.radius.sm,
              bgcolor: alpha(theme.palette.primary.main, 0.1),
              color: 'primary.main',
              display: 'grid',
              placeItems: 'center',
            })}
          >
            <AccessTimeRoundedIcon fontSize="small" />
          </Box>
          <Typography variant="h6" sx={{ flex: 1 }}>Estimated arrival</Typography>
        </Stack>
        {hasEta ? (
          <Box>
            <Typography
              variant="h2"
              sx={{ fontSize: { xs: 24, sm: 28 }, lineHeight: 1.15, mb: 0.5 }}
            >
              {formatTimeRange(startUtc!, endUtc!)}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {formatDateOnly(startUtc!)}
            </Typography>
          </Box>
        ) : (
          <Typography color="text.secondary" variant="body2">
            Pending — we'll update this as soon as a driver is assigned.
          </Typography>
        )}
      </CardContent>
    </Card>
  )
}

function DriverCard({ name, vehicle }: { name: string; vehicle?: string | null }) {
  return (
    <Card>
      <CardContent>
        <Typography variant="h6" sx={{ mb: 1.5 }}>
          Your driver
        </Typography>
        <Stack direction="row" spacing={2} sx={{ alignItems: 'center' }}>
          <Avatar
            sx={(theme) => ({
              bgcolor: theme.palette.primary.main,
              color: theme.palette.primary.contrastText,
              width: 48,
              height: 48,
              fontWeight: 600,
            })}
          >
            {name.charAt(0)}
          </Avatar>
          <Box>
            <Typography variant="body1" sx={{ fontWeight: 600 }}>
              {name}
            </Typography>
            {vehicle && (
              <Typography variant="body2" color="text.secondary">
                {vehicle}
              </Typography>
            )}
          </Box>
        </Stack>
      </CardContent>
    </Card>
  )
}

function TimelineList({ data, nowMs }: { data: TrackingTimeline; nowMs: number }) {
  const events = useMemo(
    () => [...data.events].sort((a, b) => +new Date(b.atUtc) - +new Date(a.atUtc)),
    [data.events],
  )
  return (
    <Timeline
      sx={{
        p: 0,
        m: 0,
        '& .MuiTimelineItem-root::before': { flex: 0, padding: 0 },
      }}
    >
      {events.map((event, idx) => {
        const isCurrent = idx === 0
        return (
          <TimelineItem key={`${event.status}-${event.atUtc}`}>
            <TimelineOppositeContent
              sx={{
                flex: '0 0 110px',
                color: 'text.secondary',
                fontSize: 12,
                pt: 1.25,
              }}
            >
              {formatRelativeTime(event.atUtc, nowMs)}
            </TimelineOppositeContent>
            <TimelineSeparator>
              <TimelineDot
                sx={(theme) => ({
                  bgcolor: isCurrent ? theme.palette.primary.main : alpha(theme.palette.primary.main, 0.3),
                  boxShadow: 'none',
                  margin: 1,
                  ...(isCurrent && {
                    animation: `${pulse} 2s ease-out infinite`,
                    color: alpha(theme.palette.primary.main, 0.35),
                  }),
                })}
              />
              {idx < events.length - 1 && (
                <TimelineConnector
                  sx={(theme) => ({ bgcolor: alpha(theme.palette.divider, 0.5) })}
                />
              )}
            </TimelineSeparator>
            <TimelineContent sx={{ pt: 1, pb: 2.5 }}>
              <Typography variant="body1" sx={{ fontWeight: 600, lineHeight: 1.3 }}>
                {formatStatus(event.status)}
              </Typography>
              {event.description && (
                <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>
                  {event.description}
                </Typography>
              )}
              {event.locationLabel && (
                <Typography
                  variant="caption"
                  color="text.secondary"
                  sx={{ display: 'block', mt: 0.5, letterSpacing: '0.02em' }}
                >
                  {event.locationLabel}
                </Typography>
              )}
            </TimelineContent>
          </TimelineItem>
        )
      })}
    </Timeline>
  )
}

function formatStatus(status: string): string {
  return status.replace(/([A-Z])/g, ' $1').replace(/^./, (c) => c.toUpperCase()).trim()
}

function formatRelativeTime(utc: string, nowMs: number): string {
  const date = new Date(utc)
  const diffMin = Math.floor((nowMs - date.getTime()) / 60_000)
  if (diffMin < 1) return 'Just now'
  if (diffMin < 60) return `${diffMin}m ago`
  const diffHr = Math.floor(diffMin / 60)
  if (diffHr < 24) return `${diffHr}h ago`
  const sameYear = date.getFullYear() === new Date(nowMs).getFullYear()
  return date.toLocaleDateString(undefined, {
    day: 'numeric',
    month: 'short',
    ...(sameYear ? {} : { year: 'numeric' }),
  })
}

function formatTimeRange(startUtc: string, endUtc: string): string {
  const start = new Date(startUtc)
  const end = new Date(endUtc)
  const timeFmt: Intl.DateTimeFormatOptions = { hour: 'numeric', minute: '2-digit' }
  return `${start.toLocaleTimeString(undefined, timeFmt)} – ${end.toLocaleTimeString(undefined, timeFmt)}`
}

function formatDateOnly(utc: string): string {
  return new Date(utc).toLocaleDateString(undefined, {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
  })
}
