import { useMemo, type ReactElement } from 'react'
import { useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import {
  alpha,
  Avatar,
  Badge,
  Box,
  Card,
  Center,
  Container,
  Grid,
  Group,
  Loader,
  Stack,
  Text,
  Timeline,
  Title,
} from '@mantine/core'
import {
  CheckCircleIcon,
  ClockIcon,
  HourglassIcon,
  LuggageIcon,
  TruckIcon,
} from '../components/Icon'
import { getTracking } from '../api/pax'
import type { TrackingTimeline } from '../api/client'
import { PoweredByFooter } from '../components/PoweredByFooter'
import { useRedirectOnNotFound } from '../hooks/useRedirectOnNotFound'

// White-on-Ink hero scrim (white alphas, not brand hex) — reads on the Ink-Blue hero.
const SCRIM_HERO = 'var(--mantine-color-ink-9)'
const SCRIM_FILL = 'rgba(255,255,255,0.18)'
const SCRIM_FILL_STRONG = 'rgba(255,255,255,0.2)'
const SCRIM_BODY = 'rgba(255,255,255,0.85)'

const STATUS_INFO = {
  Delivered: {
    icon: <CheckCircleIcon size={16} color="#fff" />,
    label: 'Delivered',
    headline: 'Your bag has arrived',
    description: 'Thanks for using our delivery service. We hope it arrived safely.',
  },
  OutForDelivery: {
    icon: <TruckIcon size={16} color="#fff" />,
    label: 'On the way',
    headline: 'Your bag is on the way',
    description: 'Our driver is heading to your delivery address now.',
  },
} as const

// Constructing an Intl.DateTimeFormat is the expensive half of the formatting, and
// toLocale*String builds a fresh one per call — this page re-renders every 30s and
// formats once per timeline event. Same for the status regexes, which sit in a map.
const DAY_MONTH = new Intl.DateTimeFormat(undefined, { day: 'numeric', month: 'short' })
const DAY_MONTH_YEAR = new Intl.DateTimeFormat(undefined, {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
})
const TIME_OF_DAY = new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' })
const LONG_DATE = new Intl.DateTimeFormat(undefined, {
  weekday: 'long',
  day: 'numeric',
  month: 'long',
})
const DAY_AND_TIME = new Intl.DateTimeFormat(undefined, {
  weekday: 'short',
  day: 'numeric',
  month: 'short',
  hour: 'numeric',
  minute: '2-digit',
})
const CAPITALS = /([A-Z])/g
const FIRST_CHAR = /^./

// Splitting the PascalCase status is a decent fallback but a poor label: it title-
// cases every word, so the passenger read "Out For Delivery" and "Collected From
// Airport". Statuses we know about get written English; anything new still falls
// back to the split rather than showing a raw enum name.
const STATUS_LABELS: Record<string, string> = {
  BookingConfirmed: 'Booking confirmed',
  CollectedFromAirport: 'Collected from the airport',
  AtDepot: 'At our depot',
  OutForDelivery: 'Out for delivery',
  Delivered: 'Delivered',
  Pending: 'Pending',
  Cancelled: 'Cancelled',
}

const PENDING_STATUS = {
  icon: <HourglassIcon size={16} color="#fff" />,
  headline: 'Preparing your delivery',
  description: "We'll keep this page updated as your bag moves through our network.",
}

export function Tracking() {
  const { id } = useParams<{ id: string }>()

  const tracking = useQuery({
    queryKey: ['pax', 'tracking', id],
    queryFn: () => getTracking(id ?? ''),
    enabled: !!id,
    refetchInterval: (query) =>
      query.state.data?.currentStatus === 'Delivered' ? false : 30_000,
    retry: false,
  })

  useRedirectOnNotFound(tracking.error)

  if (!id) return null

  if (tracking.isLoading) {
    return (
      <Center mih="100vh">
        <Stack gap="md" align="center">
          <Loader size="lg" />
          <Text size="sm" c="dimmed">
            Loading tracking…
          </Text>
        </Stack>
      </Center>
    )
  }

  const status = tracking.data?.currentStatus ?? 'Pending'
  const statusInfo = getStatusInfo(status)

  return (
    // Flex column so the footer sits on the bottom edge. A short timeline used to
    // leave the sign-off floating mid-page with a few hundred pixels of blank
    // surface under it, which reads as a page that failed to finish loading.
    <Box mih="100vh" style={{ display: 'flex', flexDirection: 'column' }}>
      <Box
        px={{ base: 20, sm: 32 }}
        pt={{ base: 32, sm: 40 }}
        pb={{ base: 48, sm: 56 }}
        style={{ backgroundColor: SCRIM_HERO, color: '#fff' }}
      >
        <Container size="lg" px={0} style={{ position: 'relative' }}>
          <Group gap="sm" align="center" mb="lg" wrap="nowrap">
            <Center
              w={36}
              h={36}
              style={{ borderRadius: 12, backgroundColor: SCRIM_FILL, flexShrink: 0 }}
            >
              <LuggageIcon size={20} color="#fff" />
            </Center>
            <Box style={{ flex: 1, minWidth: 0 }}>
              <Text
                style={{
                  opacity: 0.8,
                  letterSpacing: '0.08em',
                  textTransform: 'uppercase',
                  fontWeight: 600,
                  fontSize: 10.5,
                  lineHeight: 1.2,
                }}
              >
                Baggage tracking
              </Text>
              <Group gap={6} align="center" mt={2} wrap="nowrap">
                <Box
                  className="pax-pulse"
                  style={{
                    width: 6,
                    height: 6,
                    borderRadius: '50%',
                    backgroundColor: 'var(--mantine-color-green-4)',
                    color: 'var(--mantine-color-green-4)',
                  }}
                />
                <Text style={{ opacity: 0.85, fontWeight: 600, fontSize: 12 }}>
                  Live · updates every 30s
                </Text>
              </Group>
            </Box>
          </Group>

          <Badge
            variant="transparent"
            mb="sm"
            leftSection={statusInfo.icon}
            style={{
              backgroundColor: SCRIM_FILL_STRONG,
              color: '#fff',
              fontWeight: 700,
              letterSpacing: '0.04em',
            }}
          >
            {statusInfo.label}
          </Badge>

          <Title
            order={1}
            mb="xs"
            style={{
              fontSize: 'clamp(32px, 8vw, 42px)',
              lineHeight: 1.05,
              color: 'inherit',
              fontWeight: 700,
              letterSpacing: '-0.03em',
            }}
          >
            {statusInfo.headline}
          </Title>
          <Text style={{ color: SCRIM_BODY, maxWidth: 480 }}>{statusInfo.description}</Text>
        </Container>
      </Box>

      <Container
        size="lg"
        px={{ base: 12, sm: 16 }}
        mt={{ base: -28, sm: -32 }}
        pb={48}
        // width:100% because Container centres itself with auto inline margins,
        // and an auto cross-axis margin opts a flex item out of stretching — it
        // would otherwise shrink to its content width inside the column above.
        style={{ position: 'relative', flex: 1, width: '100%' }}
      >
        <Grid gap={{ base: 'md', md: 'lg' }}>
          <Grid.Col span={{ base: 12, md: 4 }}>
            <Stack gap="lg">
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
          </Grid.Col>
          <Grid.Col span={{ base: 12, md: 8 }}>
            <Card p="lg">
              <Title order={3} mb={4}>
                Delivery timeline
              </Title>
              <Text size="sm" c="dimmed" mb="lg">
                Most recent updates first
              </Text>
              {tracking.data ? (
                <TimelineList data={tracking.data} nowMs={tracking.dataUpdatedAt} />
              ) : (
                <Text c="dimmed">No updates yet.</Text>
              )}
            </Card>
          </Grid.Col>
        </Grid>
      </Container>

      <PoweredByFooter />
    </Box>
  )
}

function getStatusInfo(status: string): {
  icon: ReactElement
  label: string
  headline: string
  description: string
} {
  if (status in STATUS_INFO) {
    return STATUS_INFO[status as keyof typeof STATUS_INFO]
  }
  return { ...PENDING_STATUS, label: formatStatus(status) }
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
    <Card p="lg">
      <Group gap="sm" align="center" mb="md" wrap="nowrap">
        <Center
          w={36}
          h={36}
          style={{
            borderRadius: 'var(--mantine-radius-sm)',
            backgroundColor: 'var(--mantine-color-brand-light)',
            color: 'var(--mantine-color-brand-light-color)',
          }}
        >
          <ClockIcon size={18} />
        </Center>
        <Text tt="uppercase" size="xs" fw={600} c="dimmed" style={{ flex: 1, letterSpacing: '0.06em' }}>
          Estimated arrival
        </Text>
      </Group>
      {hasEta ? (
        <Box>
          <Text fw={700} mb={4} style={{ fontSize: 'clamp(24px, 6vw, 28px)', lineHeight: 1.15 }}>
            {formatTimeRange(startUtc!, endUtc!)}
          </Text>
          <Text size="sm" c="dimmed">
            {formatDateOnly(startUtc!)}
          </Text>
        </Box>
      ) : (
        <Text c="dimmed" size="sm">
          Pending — we'll update this as soon as a driver is assigned.
        </Text>
      )}
    </Card>
  )
}

function DriverCard({ name, vehicle }: { name: string; vehicle?: string | null }) {
  return (
    <Card p="lg">
      <Text tt="uppercase" size="xs" fw={600} c="dimmed" mb="sm" style={{ letterSpacing: '0.06em' }}>
        Your driver
      </Text>
      <Group gap="md" align="center" wrap="nowrap">
        <Avatar variant="filled" color="brand" radius={100} size={48} style={{ fontWeight: 600 }}>
          {name.charAt(0)}
        </Avatar>
        <Box>
          <Text fw={600}>{name}</Text>
          {vehicle && (
            <Text size="sm" c="dimmed">
              {vehicle}
            </Text>
          )}
        </Box>
      </Group>
    </Card>
  )
}

function TimelineList({ data, nowMs }: { data: TrackingTimeline; nowMs: number }) {
  const events = useMemo(
    () => [...data.events].sort((a, b) => +new Date(b.atUtc) - +new Date(a.atUtc)),
    [data.events],
  )
  return (
    <Timeline active={0} bulletSize={18} lineWidth={2} color="brand">
      {events.map((event, idx) => {
        const isCurrent = idx === 0
        return (
          <Timeline.Item
            key={`${event.status}-${event.atUtc}`}
            bullet={
              <Box
                className={isCurrent ? 'pax-pulse' : undefined}
                style={{
                  width: 12,
                  height: 12,
                  borderRadius: '50%',
                  backgroundColor: isCurrent
                    ? 'var(--mantine-color-brand-filled)'
                    : alpha('var(--mantine-color-brand-6)', 0.3),
                  color: alpha('var(--mantine-color-brand-6)', 0.35),
                }}
              />
            }
            title={
              <Text fw={600} size="sm" style={{ lineHeight: 1.3 }}>
                {formatStatus(event.status)}
              </Text>
            }
          >
            {/* When it happened is the fact; how long ago is the gloss. The
                relative time led here, which reads fine at "27m ago" and badly
                at "3h ago" when the passenger wants to know the actual hour. */}
            <Group gap={6} align="baseline" wrap="nowrap">
              <Text size="sm" fw={500} style={{ fontVariantNumeric: 'tabular-nums' }}>
                {formatEventTime(event.atUtc, nowMs)}
              </Text>
              <Text size="xs" c="dimmed">
                {formatRelativeTime(event.atUtc, nowMs)}
              </Text>
            </Group>
            {event.description && (
              <Text size="sm" c="dimmed" mt={2}>
                {event.description}
              </Text>
            )}
            {event.locationLabel && (
              <Text size="xs" c="dimmed" mt={4} style={{ letterSpacing: '0.02em' }}>
                {event.locationLabel}
              </Text>
            )}
          </Timeline.Item>
        )
      })}
    </Timeline>
  )
}

function formatStatus(status: string): string {
  return (
    STATUS_LABELS[status] ??
    status.replace(CAPITALS, ' $1').replace(FIRST_CHAR, (c) => c.toUpperCase()).trim()
  )
}

/** Clock time for today's events; day and clock time once "3:58 PM" is ambiguous. */
function formatEventTime(utc: string, nowMs: number): string {
  const date = new Date(utc)
  const sameDay = date.toDateString() === new Date(nowMs).toDateString()
  return sameDay ? TIME_OF_DAY.format(date) : DAY_AND_TIME.format(date)
}

function formatRelativeTime(utc: string, nowMs: number): string {
  const date = new Date(utc)
  const diffMin = Math.floor((nowMs - date.getTime()) / 60_000)
  if (diffMin < 1) return 'Just now'
  if (diffMin < 60) return `${diffMin}m ago`
  const diffHr = Math.floor(diffMin / 60)
  if (diffHr < 24) return `${diffHr}h ago`
  const sameYear = date.getFullYear() === new Date(nowMs).getFullYear()
  return (sameYear ? DAY_MONTH : DAY_MONTH_YEAR).format(date)
}

function formatTimeRange(startUtc: string, endUtc: string): string {
  return `${TIME_OF_DAY.format(new Date(startUtc))} – ${TIME_OF_DAY.format(new Date(endUtc))}`
}

function formatDateOnly(utc: string): string {
  return LONG_DATE.format(new Date(utc))
}
