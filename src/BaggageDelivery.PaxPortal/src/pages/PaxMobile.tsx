import { memo, useCallback, useMemo, useState, type CSSProperties, type ReactNode } from 'react'
import { Link as RouterLink, useParams } from 'react-router-dom'
import { useMutation, useQuery, type UseQueryResult } from '@tanstack/react-query'
import {
  alpha,
  Alert,
  Badge,
  Box,
  Button,
  Card,
  Center,
  Collapse,
  Container,
  Group,
  Loader,
  MantineProvider,
  Radio,
  Stack,
  Switch,
  Text,
  Textarea,
  TextInput,
  Title,
  Tooltip,
  ActionIcon,
} from '@mantine/core'
import { notifications } from '@mantine/notifications'
import {
  ArrowRightIcon,
  CheckCircleIcon,
  CheckIcon,
  ClockIcon,
  EditIcon,
  LockIcon,
  LuggageIcon,
  MapPinIcon,
  UserIcon,
} from '../components/Icon'
import { confirmBooking, getBooking, getTimeslots } from '../api/pax'
import type { AddressDto, BookingSummary, TimeSlot } from '../api/client'
import type { AddressDetail } from '../types/address'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import { useRedirectOnNotFound } from '../hooks/useRedirectOnNotFound'
import { useColorMode } from '../hooks/colorModeContext'
import { AddressAutocomplete } from '../components/AddressAutocomplete'
import { PoweredByFooter } from '../components/PoweredByFooter'
import { dfrntCssVariablesResolver } from '../styles/mantineTheme'
import { airlineThemeOverride } from '../styles/airlineMantineTheme'
import { getAirlineBrand } from '../styles/airlineBranding'

// White-on-Ink scrim overlays for the brand hero. These are white alphas (not brand
// hex), so they read on the fixed Ink-Blue hero regardless of the primary colour.
const SCRIM_HERO = 'var(--mantine-color-ink-9)'
const SCRIM_FILL = 'rgba(255,255,255,0.18)'
const SCRIM_FILL_FAINT = 'rgba(255,255,255,0.22)'
const SCRIM_BODY = 'rgba(255,255,255,0.85)'

// Static hero styles hoisted out of ConfirmForm's render — it re-renders on every
// keystroke, so keeping these as module constants avoids reallocating them each time.
const HERO_BOX_STYLE: CSSProperties = {
  backgroundColor: SCRIM_HERO,
  color: '#fff',
  position: 'relative',
  overflow: 'hidden',
}
const HERO_EYEBROW_STYLE: CSSProperties = {
  opacity: 0.8,
  letterSpacing: '0.08em',
  textTransform: 'uppercase',
  fontWeight: 600,
  fontSize: 10.5,
  lineHeight: 1.2,
}
const HERO_TITLE_STYLE: CSSProperties = {
  fontSize: 'clamp(32px, 8vw, 40px)',
  lineHeight: 1.05,
  color: 'inherit',
  fontWeight: 700,
  letterSpacing: '-0.03em',
}

function showError(message: string) {
  notifications.show({ color: 'red', message, autoClose: 4000 })
}

export function PaxMobile() {
  const { id } = useParams<{ id: string }>()
  const online = useOnlineStatus()

  const booking = useQuery({
    queryKey: ['pax', 'booking', id],
    queryFn: () => getBooking(id ?? ''),
    enabled: !!id,
    retry: false,
  })

  // Timeslots depend only on the URL id, not the booking response — fetch them
  // in parallel with the booking instead of waiting for ConfirmForm to mount.
  const slots = useQuery({
    queryKey: ['pax', 'timeslots', id],
    queryFn: () => getTimeslots(id ?? ''),
    enabled: !!id,
  })

  useRedirectOnNotFound(booking.error)

  if (!id) return null

  if (booking.isLoading) {
    return (
      <Center mih="100vh">
        <Stack gap="md" align="center">
          <Loader size="lg" />
          <Text size="sm" c="dimmed">
            Loading your booking…
          </Text>
        </Stack>
      </Center>
    )
  }

  if (!booking.data) {
    return (
      <Container pt={64}>
        <Alert color="red" variant="light">
          We could not load this booking. Please contact support.
        </Alert>
      </Container>
    )
  }

  return <BrandedConfirmForm bookingId={id} summary={booking.data} online={online} slots={slots} />
}

// Re-theme the passenger flow from the airline code on the booking. The root
// MantineProvider (main.tsx) stays the default cyan for loading/expired states; this
// nested provider only applies once booking data is available.
function BrandedConfirmForm({
  bookingId,
  summary,
  online,
  slots,
}: {
  bookingId: string
  summary: BookingSummary
  online: boolean
  slots: UseQueryResult<TimeSlot[]>
}) {
  const { mode } = useColorMode()
  const airlineTheme = useMemo(
    () => airlineThemeOverride(getAirlineBrand(summary.airlineCode)),
    [summary.airlineCode],
  )

  return (
    <MantineProvider
      theme={airlineTheme}
      forceColorScheme={mode}
      cssVariablesResolver={dfrntCssVariablesResolver}
      withGlobalClasses={false}
    >
      <ConfirmForm bookingId={bookingId} summary={summary} online={online} slots={slots} />
    </MantineProvider>
  )
}

function ConfirmForm({
  bookingId,
  summary,
  online,
  slots,
}: {
  bookingId: string
  summary: BookingSummary
  online: boolean
  slots: UseQueryResult<TimeSlot[]>
}) {
  const [address, setAddress] = useState<AddressDto>(summary.deliveryAddress)
  const [editingAddress, setEditingAddress] = useState(false)
  const [editingDetails, setEditingDetails] = useState(false)
  const [passengerName, setPassengerName] = useState(summary.passengerName ?? '')
  const [passengerPhone, setPassengerPhone] = useState(summary.passengerPhone ?? '')
  const [passengerEmail, setPassengerEmail] = useState(summary.passengerEmail ?? '')
  const [selectedSlotId, setSelectedSlotId] = useState<string | null>(null)
  const [atlOptionId, setAtlOptionId] = useState<number | null>(null)
  const [accessNotes, setAccessNotes] = useState('')
  const [confirmed, setConfirmed] = useState(false)
  const [submitAttempted, setSubmitAttempted] = useState(false)

  const defaultSlotId = useMemo(
    () =>
      slots.data && slots.data.length
        ? (slots.data.find((s) => s.firstAvailable) ?? slots.data[0]).id
        : null,
    [slots.data],
  )
  const effectiveSlotId = selectedSlotId ?? defaultSlotId

  const selectedSlot: TimeSlot | undefined = useMemo(
    () => slots.data?.find((s) => s.id === effectiveSlotId),
    [slots.data, effectiveSlotId],
  )

  // Stable identity so the memoized SlotOption rows aren't invalidated each render.
  const handleSelectSlot = useCallback((id: string) => setSelectedSlotId(id), [])

  // Stable identity so the memoized AddressAutocomplete isn't re-rendered on every keystroke.
  const handleAddressSelect = useCallback(
    (detail: AddressDetail) =>
      setAddress((a) => ({
        ...a,
        line1: detail.street,
        suburb: detail.suburb || null,
        city: detail.city,
        postCode: detail.postalCode || null,
        country: detail.countryCode || a.country,
      })),
    [],
  )

  // Preserve the backend ordering (Sequence descending, then Name).
  const atlOptions = summary.atlOptions

  const fieldErrors: { [key: string]: string } = {}
  if (!passengerName.trim()) fieldErrors.passengerName = 'Please enter your full name.'
  if (!passengerPhone.trim()) {
    fieldErrors.passengerPhone = 'Please enter your phone number.'
  } else if (passengerPhone.replace(/\D/g, '').length < 7) {
    fieldErrors.passengerPhone = 'Please enter a valid phone number.'
  }
  if (!passengerEmail.trim()) {
    fieldErrors.passengerEmail = 'Please enter your email address.'
  } else if (!passengerEmail.includes('@')) {
    fieldErrors.passengerEmail = 'Please enter a valid email address.'
  }
  if (!address.line1.trim()) fieldErrors.line1 = 'Please enter your street address.'
  if (!(address.suburb ?? '').trim()) fieldErrors.suburb = 'Please enter your suburb.'
  if (!address.city.trim()) fieldErrors.city = 'Please enter your city.'
  if (!(address.postCode ?? '').trim()) fieldErrors.postCode = 'Please enter your postcode.'
  if (!selectedSlot) fieldErrors.slot = 'Please pick a delivery time slot.'

  const selectedAtlOption = atlOptions.find((o) => o.id === atlOptionId)
  const atlNotesRequired = selectedAtlOption?.name.trim().toLowerCase() === 'safe place'
  if (atlNotesRequired && !accessNotes.trim()) {
    fieldErrors.accessNotes = 'Please describe the safe place to leave your baggage.'
  }

  const showFieldError = (key: string) =>
    submitAttempted ? fieldErrors[key] : undefined

  const confirm = useMutation({
    mutationFn: (body: Parameters<typeof confirmBooking>[1]) => confirmBooking(bookingId, body),
    onSuccess: () => {
      setConfirmed(true)
      // The success screen links straight to /t/:id — warm that route's lazy chunk now.
      void import('./Tracking')
    },
    onError: (err) => {
      const responseStatus = (err as { response?: { status?: number } })?.response?.status
      if (responseStatus === 409) showError('This booking has already been confirmed.')
      else showError('Could not submit your confirmation. Please try again.')
    },
  })

  function submit() {
    setSubmitAttempted(true)
    if (Object.keys(fieldErrors).length > 0) {
      showError('Please complete all required fields before confirming.')
      if (!editingDetails && (fieldErrors.passengerName || fieldErrors.passengerPhone || fieldErrors.passengerEmail)) {
        setEditingDetails(true)
      }
      if (!editingAddress && (fieldErrors.line1 || fieldErrors.suburb || fieldErrors.city || fieldErrors.postCode)) {
        setEditingAddress(true)
      }
      return
    }
    confirm.mutate({
      address,
      timeSlotStartUtc: selectedSlot!.startUtc,
      timeSlotEndUtc: selectedSlot!.endUtc,
      atlOptionId,
      accessNotes: accessNotes || null,
      passengerName: passengerName.trim(),
      passengerPhone: passengerPhone.trim(),
      passengerEmail: passengerEmail.trim(),
    })
  }

  if (confirmed) return <ConfirmedScreen summary={summary} slot={selectedSlot} bookingId={bookingId} />

  return (
    <Box mih="100vh" pb={{ base: 112, sm: 128 }}>
      <Box
        px={{ base: 20, sm: 32 }}
        pt={{ base: 32, sm: 40 }}
        pb={{ base: 48, sm: 56 }}
        style={HERO_BOX_STYLE}
      >
        <Container size={520} px={0} style={{ position: 'relative' }}>
          <Group gap="sm" align="center" mb="lg" wrap="nowrap">
            <Center
              w={36}
              h={36}
              style={{ borderRadius: 12, backgroundColor: SCRIM_FILL, flexShrink: 0 }}
            >
              <LuggageIcon size={20} color="#fff" />
            </Center>
            <Box style={{ flex: 1, minWidth: 0 }}>
              <Text style={HERO_EYEBROW_STYLE}>{summary.airlineLabel}</Text>
              <Text style={{ fontWeight: 700, letterSpacing: '0.04em', fontSize: 13 }}>
                REF · {summary.jobId}
              </Text>
            </Box>
          </Group>

          <Title order={1} mb="sm" style={HERO_TITLE_STYLE}>
            Confirm your
            <br />
            baggage delivery
          </Title>
          <Text style={{ color: SCRIM_BODY, maxWidth: 400, fontSize: 15 }}>
            Review your details below and pick a delivery window. We'll text you when our
            driver is on the way.
          </Text>

          <StepProgress current={1} steps={['Confirm', 'In transit', 'Delivered']} />
        </Container>
      </Box>

      <Container size={520} px={0} mt={{ base: -28, sm: -32 }} style={{ position: 'relative', zIndex: 1 }}>
        <Stack gap="md" px={{ base: 12, sm: 0 }}>
          <SectionCard
            icon={<UserIcon size={18} />}
            title="Your details"
            onToggleEdit={() => setEditingDetails((s) => !s)}
            editing={editingDetails}
          >
            <Stack gap="sm">
              <TextInput
                label="Full name"
                value={passengerName}
                onChange={(e) => setPassengerName(e.currentTarget.value)}
                autoComplete="name"
                required
                size="sm"
                disabled={!editingDetails}
                error={showFieldError('passengerName')}
              />
              <TextInput
                label="Phone number"
                value={passengerPhone}
                onChange={(e) => setPassengerPhone(e.currentTarget.value)}
                autoComplete="tel"
                inputMode="tel"
                required
                size="sm"
                disabled={!editingDetails}
                error={showFieldError('passengerPhone')}
              />
              <TextInput
                label="Email address"
                value={passengerEmail}
                onChange={(e) => setPassengerEmail(e.currentTarget.value)}
                autoComplete="email"
                type="email"
                required
                size="sm"
                disabled={!editingDetails}
                error={showFieldError('passengerEmail')}
              />
            </Stack>
          </SectionCard>

          <SectionCard
            icon={<MapPinIcon size={18} />}
            title="Delivery address"
            onToggleEdit={() => setEditingAddress((s) => !s)}
            editing={editingAddress}
          >
            <Stack gap="sm">
              {editingAddress && (
                <AddressAutocomplete bookingId={bookingId} onAddressSelect={handleAddressSelect} />
              )}
              <TextInput
                label="Street address"
                value={address.line1}
                onChange={(e) => setAddress((a) => ({ ...a, line1: e.currentTarget.value }))}
                required
                size="sm"
                disabled={!editingAddress}
                error={showFieldError('line1')}
              />
              <TextInput
                label="Suburb"
                value={address.suburb ?? ''}
                onChange={(e) => setAddress((a) => ({ ...a, suburb: e.currentTarget.value }))}
                required
                size="sm"
                disabled={!editingAddress}
                error={showFieldError('suburb')}
              />
              <Group gap="sm" align="flex-start" grow wrap="nowrap">
                <TextInput
                  label="City"
                  value={address.city}
                  onChange={(e) => setAddress((a) => ({ ...a, city: e.currentTarget.value }))}
                  required
                  size="sm"
                  disabled={!editingAddress}
                  error={showFieldError('city')}
                />
                <TextInput
                  label="Postcode"
                  value={address.postCode ?? ''}
                  onChange={(e) => setAddress((a) => ({ ...a, postCode: e.currentTarget.value }))}
                  required
                  size="sm"
                  maw={132}
                  disabled={!editingAddress}
                  error={showFieldError('postCode')}
                />
              </Group>
            </Stack>
          </SectionCard>

          <SectionCard icon={<ClockIcon size={18} />} title="Delivery time">
            {slots.isLoading && (
              <Group gap="sm" align="center" py="xs">
                <Loader size={18} />
                <Text size="sm" c="dimmed">
                  Loading available windows…
                </Text>
              </Group>
            )}
            {showFieldError('slot') && (
              <Text size="sm" c="red" mb="xs">
                {showFieldError('slot')}
              </Text>
            )}
            {slots.data && (
              <Stack gap="xs">
                {slots.data.map((slot) => (
                  <SlotOption
                    key={slot.id}
                    slot={slot}
                    selected={slot.id === effectiveSlotId}
                    onSelect={handleSelectSlot}
                  />
                ))}
              </Stack>
            )}
          </SectionCard>

          <SectionCard
            icon={<LockIcon size={18} />}
            title="Authority to Leave"
            subtitle="Leave baggage unattended if you're not home"
            action={
              <Switch
                checked={atlOptionId !== null}
                disabled={atlOptions.length === 0}
                onChange={(e) =>
                  setAtlOptionId(e.currentTarget.checked ? atlOptions[0]?.id ?? null : null)
                }
              />
            }
          >
            <Collapse in={atlOptionId !== null}>
              <Box pt="xs">
                <Radio.Group
                  value={atlOptionId === null ? '' : String(atlOptionId)}
                  onChange={(v) => setAtlOptionId(Number(v))}
                >
                  <Stack
                    gap={6}
                    style={
                      atlOptions.length > 6
                        ? { maxHeight: 260, overflowY: 'auto', paddingRight: 8 }
                        : undefined
                    }
                  >
                    {atlOptions.map((opt) => (
                      <Radio key={opt.id} value={String(opt.id)} label={opt.name} size="sm" />
                    ))}
                  </Stack>
                </Radio.Group>
                <Textarea
                  label={atlNotesRequired ? 'Additional details' : 'Additional details (optional)'}
                  required={atlNotesRequired}
                  value={accessNotes}
                  onChange={(e) => setAccessNotes(e.currentTarget.value)}
                  error={showFieldError('accessNotes')}
                  autosize
                  minRows={2}
                  size="sm"
                  mt="sm"
                />
              </Box>
            </Collapse>
          </SectionCard>

          {!online && (
            <Alert color="orange" variant="light" radius="md">
              You appear to be offline. Connect to the internet to submit your confirmation.
            </Alert>
          )}
        </Stack>

        <PoweredByFooter />
      </Container>

      <Box
        px="md"
        style={{
          position: 'fixed',
          left: 0,
          right: 0,
          bottom: 0,
          backgroundColor: 'var(--dd-surface-container)',
          borderTop: '1px solid var(--mantine-color-default-border)',
          paddingTop: 14,
          paddingBottom: 'calc(env(safe-area-inset-bottom, 0px) + 14px)',
          zIndex: 200,
          boxShadow: '0 -4px 16px -4px rgba(0,0,0,0.06)',
        }}
      >
        <Container size={520} px={0}>
          <Button
            size="lg"
            fullWidth
            disabled={!online}
            loading={confirm.isPending}
            onClick={submit}
            rightSection={!confirm.isPending ? <ArrowRightIcon size={18} /> : undefined}
            style={{ minHeight: 56, fontSize: 16, fontWeight: 700, letterSpacing: '0.01em' }}
          >
            {confirm.isPending ? 'Submitting…' : 'Confirm delivery'}
          </Button>
        </Container>
      </Box>
    </Box>
  )
}

function StepProgress({ current, steps }: { current: number; steps: string[] }) {
  return (
    <Group gap={6} align="center" mt="xl" wrap="nowrap">
      {steps.map((step, idx) => {
        const isActive = idx === current - 1
        const isDone = idx < current - 1
        const isCurrent = isActive || isDone
        return (
          <Group
            key={step}
            gap={6}
            align="center"
            wrap="nowrap"
            style={{ flex: idx === steps.length - 1 ? 'unset' : 1 }}
          >
            <Center
              w={22}
              h={22}
              style={{
                borderRadius: '50%',
                backgroundColor: isCurrent ? 'rgba(255,255,255,0.95)' : SCRIM_FILL_FAINT,
                color: isCurrent ? 'var(--mantine-color-brand-filled)' : 'inherit',
                fontSize: 11,
                fontWeight: 700,
                flexShrink: 0,
              }}
            >
              {isDone ? <CheckIcon size={14} /> : idx + 1}
            </Center>
            <Text
              style={{
                fontWeight: isActive ? 700 : 500,
                opacity: isCurrent ? 1 : 0.65,
                fontSize: 12,
                whiteSpace: 'nowrap',
              }}
            >
              {step}
            </Text>
            {idx < steps.length - 1 && (
              <Box
                style={{
                  flex: 1,
                  height: 2,
                  backgroundColor: SCRIM_FILL_FAINT,
                  borderRadius: 999,
                  marginLeft: 6,
                }}
              />
            )}
          </Group>
        )
      })}
    </Group>
  )
}

function SectionCard({
  icon,
  title,
  subtitle,
  action,
  onToggleEdit,
  editing,
  children,
}: {
  icon: ReactNode
  title: string
  subtitle?: string
  action?: ReactNode
  onToggleEdit?: () => void
  editing?: boolean
  children: ReactNode
}) {
  const trailing =
    action ??
    (onToggleEdit ? (
      <Tooltip label={editing ? `Save ${title}` : `Edit ${title}`}>
        <ActionIcon
          variant="subtle"
          color="gray"
          onClick={onToggleEdit}
          aria-label={editing ? `Save ${title}` : `Edit ${title}`}
          style={{ color: editing ? 'var(--mantine-color-brand-filled)' : undefined }}
        >
          {editing ? <CheckIcon size={18} /> : <EditIcon size={18} />}
        </ActionIcon>
      </Tooltip>
    ) : null)

  return (
    <Card p="lg">
      <Group gap="sm" align="center" mb="md" wrap="nowrap">
        <Center
          w={32}
          h={32}
          style={{
            borderRadius: 'var(--mantine-radius-sm)',
            backgroundColor: 'var(--mantine-color-brand-light)',
            color: 'var(--mantine-color-brand-light-color)',
            flexShrink: 0,
          }}
        >
          {icon}
        </Center>
        <Box style={{ flex: 1, minWidth: 0 }}>
          <Text fw={600} style={{ lineHeight: 1.3 }}>
            {title}
          </Text>
          {subtitle && (
            <Text size="xs" c="dimmed">
              {subtitle}
            </Text>
          )}
        </Box>
        {trailing}
      </Group>
      {children}
    </Card>
  )
}

// Memoized so typing in the form's text fields (which re-renders ConfirmForm on every
// keystroke) doesn't re-render every slot in the list. `onSelect` takes the slot id so
// ConfirmForm can pass one stable handler to all rows instead of a per-row closure —
// without that, the changing prop identity would defeat the memo.
const SlotOption = memo(function SlotOption({
  slot,
  selected,
  onSelect,
}: {
  slot: TimeSlot
  selected: boolean
  onSelect: (id: string) => void
}) {
  return (
    <Box
      role="radio"
      aria-checked={selected}
      tabIndex={0}
      onClick={() => onSelect(slot.id)}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault()
          onSelect(slot.id)
        }
      }}
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: 12,
        padding: '12px 14px',
        borderRadius: 'var(--mantine-radius-md)',
        border: `1px solid ${selected ? 'var(--mantine-color-brand-filled)' : 'var(--mantine-color-default-border)'}`,
        backgroundColor: selected ? 'var(--mantine-color-brand-light)' : 'var(--dd-surface-container)',
        cursor: 'pointer',
        outline: 'none',
      }}
    >
      <Center
        w={20}
        h={20}
        style={{
          borderRadius: '50%',
          border: `2px solid ${selected ? 'var(--mantine-color-brand-filled)' : 'var(--mantine-color-default-border)'}`,
          flexShrink: 0,
        }}
      >
        {selected && (
          <Box
            w={10}
            h={10}
            style={{ borderRadius: '50%', backgroundColor: 'var(--mantine-color-brand-filled)' }}
          />
        )}
      </Center>
      <Box style={{ flex: 1, minWidth: 0 }}>
        <Text size="sm" fw={600}>
          {slot.label}
        </Text>
        {slot.firstAvailable && (
          <Text
            style={{
              color: 'var(--mantine-color-green-light-color)',
              fontWeight: 600,
              letterSpacing: '0.06em',
              textTransform: 'uppercase',
              fontSize: 10,
            }}
          >
            First available
          </Text>
        )}
      </Box>
    </Box>
  )
})

export function ConfirmedScreen({
  summary,
  slot,
  bookingId,
}: {
  summary: BookingSummary
  slot: TimeSlot | undefined
  bookingId: string
}) {
  return (
    <Box mih="100vh">
      <Box
        px={{ base: 20, sm: 32 }}
        pt={{ base: 48, sm: 64 }}
        pb={{ base: 56, sm: 72 }}
        style={{ backgroundColor: 'var(--mantine-color-green-8)', color: '#fff' }}
      >
        <Container size={520} px={0} style={{ textAlign: 'center' }}>
          <Center
            w={80}
            h={80}
            mx="auto"
            mb="lg"
            style={{ borderRadius: '50%', backgroundColor: SCRIM_FILL }}
          >
            <CheckCircleIcon size={48} color="#fff" />
          </Center>
          <Badge
            variant="transparent"
            mb="sm"
            style={{
              backgroundColor: SCRIM_FILL,
              color: '#fff',
              fontWeight: 700,
              letterSpacing: '0.08em',
            }}
          >
            Confirmed
          </Badge>
          <Title
            order={1}
            mb="xs"
            style={{
              fontSize: 'clamp(30px, 7vw, 38px)',
              color: 'inherit',
              fontWeight: 700,
              letterSpacing: '-0.03em',
            }}
          >
            You're all set
          </Title>
          <Text style={{ color: SCRIM_BODY }}>REF · {summary.jobId}</Text>
        </Container>
      </Box>

      <Container size={520} px={{ base: 12, sm: 0 }} mt={{ base: -32, sm: -40 }} pb={48} style={{ position: 'relative' }}>
        <Stack gap="md">
          {slot && (
            <Card p="lg">
              <Group gap="sm" align="center" mb="sm" wrap="nowrap">
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
                  Delivery window
                </Text>
              </Group>
              <Text fw={700} style={{ fontSize: 'clamp(24px, 6vw, 28px)' }}>
                {slot.label}
              </Text>
            </Card>
          )}

          <Button
            component={RouterLink}
            to={`/t/${bookingId}`}
            size="lg"
            fullWidth
            rightSection={<ArrowRightIcon size={18} />}
            style={{ paddingTop: 12, paddingBottom: 12, fontWeight: 600 }}
          >
            Track your delivery
          </Button>

          <Card p="lg" style={{ backgroundColor: alpha('var(--mantine-color-brand-6)', 0.06) }}>
            <Text size="sm" fw={500}>
              We'll also text you when our driver is on the way.
            </Text>
          </Card>
        </Stack>
      </Container>

      <PoweredByFooter />
    </Box>
  )
}
