import { memo, useCallback, useMemo, useState, type ReactNode } from 'react'
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
  Checkbox,
  Collapse,
  Container,
  Divider,
  Group,
  Loader,
  MantineProvider,
  Modal,
  Radio,
  Stack,
  Switch,
  Text,
  Textarea,
  TextInput,
  Title,
} from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { notifications } from '@mantine/notifications'
import {
  ArrowRightIcon,
  CheckCircleIcon,
  ClockIcon,
  LockIcon,
  MapPinIcon,
  UserIcon,
} from '../components/Icon'
import { ConfirmHero } from '../components/ConfirmHero'
import { formatCountdown, useSlotHoldCountdown } from '../hooks/useSlotHoldCountdown'
import { confirmBooking, getBooking, getTimeslots } from '../api/pax'
import type { AddressDto, AtlOption, BookingSummary, TimeSlot } from '../api/client'
import type { AddressDetail } from '../types/address'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import { useRedirectOnNotFound } from '../hooks/useRedirectOnNotFound'
import { useColorMode } from '../hooks/colorModeContext'
import { AddressAutocomplete } from '../components/AddressAutocomplete'
import { PoweredByFooter } from '../components/PoweredByFooter'
import { dfrntCssVariablesResolver, tokens } from '../styles/mantineTheme'
import { airlineThemeOverride } from '../styles/airlineMantineTheme'
import { getAirlineBrand } from '../styles/airlineBranding'

// White alphas (not brand hex), so they read on the Ink-Blue and green heroes
// regardless of the primary colour.
const SCRIM_FILL = 'rgba(255,255,255,0.18)'
const SCRIM_BODY = 'rgba(255,255,255,0.85)'

// Widths of the tucJob columns these fields are written to, mirrored from
// ConfirmBookingRequest. Longer input is rejected by the API, not truncated.
const ACCESS_NOTES_MAX = 120
const PASSENGER_EMAIL_MAX = 100

function showError(message: string) {
  notifications.show({ color: 'red', message, autoClose: 4000 })
}

// The hold ticks once a second for ten minutes. Owning that state here rather than
// in ConfirmForm keeps each tick to this one Text node — otherwise the hero, all
// eight inputs, the ATL list and the review dialog re-render every second while the
// passenger is still reading the page. `onExpire` must be stable (useCallback).
const SlotHoldCountdown = memo(function SlotHoldCountdown({
  onExpire,
}: {
  onExpire: () => void
}) {
  const remainingMs = useSlotHoldCountdown(onExpire)
  return (
    <Text size="xs" fw={700} c="brand" style={{ fontVariantNumeric: 'tabular-nums' }}>
      {formatCountdown(remainingMs)}
    </Text>
  )
})

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
  // The page is a review screen, so every field is editable on arrival. The one
  // deliberate act is signing off the address — a wrong one puts the bag on a
  // stranger's doorstep — so that block locks behind an explicit tick instead of
  // hiding behind an edit pencil.
  const [addressConfirmed, setAddressConfirmed] = useState(false)
  const [passengerName, setPassengerName] = useState(summary.passengerName ?? '')
  const [passengerPhone, setPassengerPhone] = useState(summary.passengerPhone ?? '')
  const [passengerEmail, setPassengerEmail] = useState(summary.passengerEmail ?? '')
  const [selectedSlotId, setSelectedSlotId] = useState<string | null>(null)
  const [atlOptionId, setAtlOptionId] = useState<number | null>(null)
  const [accessNotes, setAccessNotes] = useState('')
  const [confirmed, setConfirmed] = useState(false)
  const [submitAttempted, setSubmitAttempted] = useState(false)
  // The server resolves the country to an ISO-2 code and is the only thing that
  // can tell us a spelling is unrecognisable — surface its message on the field
  // rather than the generic retry toast.
  const [serverCountryError, setServerCountryError] = useState<string | null>(null)
  const [reviewOpen, { open: openReview, close: closeReview }] = useDisclosure(false)

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

  // A page left open long enough can hold a run that has since departed. When the
  // hold lapses, pull a fresh list and drop the selection so defaultSlotId
  // re-selects whatever is genuinely first available now. A review dialog left
  // open across the lapse would be reading back a window that no longer exists,
  // so it closes with it.
  const { refetch: refetchSlots } = slots
  const handleHoldExpired = useCallback(() => {
    setSelectedSlotId(null)
    closeReview()
    void refetchSlots()
  }, [closeReview, refetchSlots])

  // Stable identity so the memoized AddressAutocomplete isn't re-rendered on every keystroke.
  const handleAddressSelect = useCallback(
    (detail: AddressDetail) => {
      setServerCountryError(null)
      setAddress((a) => ({
        ...a,
        line1: detail.street,
        suburb: detail.suburb || null,
        city: detail.city,
        postCode: detail.postalCode || null,
        country: detail.countryCode || a.country,
      }))
    },
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
  if (!address.country.trim()) fieldErrors.country = 'Please enter your country.'
  if (!addressConfirmed) {
    fieldErrors.addressConfirmed = 'Please confirm your delivery address is correct.'
  }
  // Guard runUtc, not just the slot: a slot cached in the pre-runUtc shape still
  // matches by id, and JSON.stringify drops the undefined deliveryTimeUtc so the
  // server saw no delivery time at all.
  if (!selectedSlot?.runUtc) fieldErrors.slot = 'Please pick a delivery window.'

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
      // Field errors and the address gate both live behind the scrim — drop the
      // dialog so the passenger can see and fix what failed.
      closeReview()

      const response = (err as {
        response?: { status?: number; data?: { errors?: Record<string, string[]> } }
      })?.response

      if (response?.status === 409) {
        showError('This booking has already been confirmed.')
        return
      }

      const errors = response?.data?.errors
      const countryError = errors?.['Address.Country']?.[0]
      if (countryError) {
        setServerCountryError(countryError)
        // Reopen the gate: the passenger can't fix a locked field.
        setAddressConfirmed(false)
        showError(countryError)
        return
      }

      // Any other validation failure (a missing delivery time, an over-long
      // note) still says what went wrong rather than collapsing into "try
      // again", which the passenger can only respond to by trying again.
      const firstError = errors && Object.values(errors).flat().find(Boolean)
      showError(firstError ?? 'Could not submit your confirmation. Please try again.')
    },
  })

  // Validation gates the review, not the send, so the docket never reads a
  // half-filled booking back to the passenger.
  function review() {
    setSubmitAttempted(true)
    if (Object.keys(fieldErrors).length > 0) {
      showError('Please complete all required fields before confirming.')
      return
    }
    openReview()
  }

  function submit() {
    // Re-checked here, not just in review(): a background refetch can retire the
    // held window while the dialog is open, and the docket would be reading back
    // a time that no longer exists.
    if (!selectedSlot?.runUtc) {
      closeReview()
      showError('Please pick a delivery window.')
      return
    }
    confirm.mutate({
      address,
      deliveryTimeUtc: selectedSlot.runUtc,
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
      <ConfirmHero summary={summary} />

      <Container size={520} px={0} mt={{ base: -28, sm: -32 }} style={{ position: 'relative', zIndex: 1 }}>
        <Stack gap="md" px={{ base: 12, sm: 0 }}>
          <SectionCard icon={<UserIcon size={18} />} title="Your details">
            <Stack gap="sm">
              <TextInput
                label="Full name"
                value={passengerName}
                onChange={(e) => setPassengerName(e.currentTarget.value)}
                autoComplete="name"
                required
                size="sm"
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
                error={showFieldError('passengerPhone')}
              />
              <TextInput
                label="Email address"
                value={passengerEmail}
                onChange={(e) => setPassengerEmail(e.currentTarget.value)}
                autoComplete="email"
                type="email"
                required
                maxLength={PASSENGER_EMAIL_MAX}
                size="sm"
                error={showFieldError('passengerEmail')}
              />
            </Stack>
          </SectionCard>

          <SectionCard icon={<MapPinIcon size={18} />} title="Delivery address">
            <Stack gap="sm">
              {!addressConfirmed && (
                <AddressAutocomplete bookingId={bookingId} onAddressSelect={handleAddressSelect} />
              )}
              <AddressGate
                confirmed={addressConfirmed}
                onChange={setAddressConfirmed}
                error={showFieldError('addressConfirmed')}
              />
              <TextInput
                label="Street address"
                value={address.line1}
                onChange={(e) => setAddress((a) => ({ ...a, line1: e.currentTarget.value }))}
                required
                size="sm"
                disabled={addressConfirmed}
                error={showFieldError('line1')}
              />
              <TextInput
                label="Suburb"
                value={address.suburb ?? ''}
                onChange={(e) => setAddress((a) => ({ ...a, suburb: e.currentTarget.value }))}
                required
                size="sm"
                disabled={addressConfirmed}
                error={showFieldError('suburb')}
              />
              <Group gap="sm" align="flex-start" grow wrap="nowrap">
                <TextInput
                  label="City"
                  value={address.city}
                  onChange={(e) => setAddress((a) => ({ ...a, city: e.currentTarget.value }))}
                  required
                  size="sm"
                  disabled={addressConfirmed}
                  error={showFieldError('city')}
                />
                <TextInput
                  label="Postcode"
                  value={address.postCode ?? ''}
                  onChange={(e) => setAddress((a) => ({ ...a, postCode: e.currentTarget.value }))}
                  required
                  size="sm"
                  maw={132}
                  disabled={addressConfirmed}
                  error={showFieldError('postCode')}
                />
              </Group>
              <TextInput
                label="Country"
                value={address.country}
                onChange={(e) => {
                  setServerCountryError(null)
                  setAddress((a) => ({ ...a, country: e.currentTarget.value }))
                }}
                required
                size="sm"
                disabled={addressConfirmed}
                error={serverCountryError ?? showFieldError('country')}
              />
            </Stack>
          </SectionCard>

          <SectionCard icon={<ClockIcon size={18} />} title="Delivery window">
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
            {/* Without these two the card renders as an empty box when the
                timeslots call fails or comes back with nothing, which reads as
                the page being broken rather than as something the passenger can
                act on. */}
            {slots.isError && (
              <Stack gap="xs" align="flex-start">
                <Text size="sm">
                  We couldn't load the available delivery windows just now.
                </Text>
                <Button variant="light" size="xs" onClick={() => void refetchSlots()}>
                  Try again
                </Button>
              </Stack>
            )}
            {slots.data?.length === 0 && (
              <Text size="sm">
                There are no delivery windows available for this booking yet.
                {summary.supportPhone && ` Call ${summary.supportPhone} and we'll arrange one with you.`}
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
            {slots.data && slots.data.length > 0 && (
              <Group
                gap={8}
                align="center"
                wrap="nowrap"
                mt="sm"
                px="sm"
                py={8}
                // A neutral strip, not a yellow one: --mantine-color-yellow-light
                // over the charcoal ladder mixes to a muddy olive, and a hold that
                // has not lapsed yet is information rather than a warning.
                style={{
                  borderRadius: 'var(--mantine-radius-sm)',
                  backgroundColor: 'var(--dd-surface-container-high)',
                  border: '1px solid var(--mantine-color-default-border)',
                }}
              >
                <ClockIcon size={14} />
                <Text size="xs" c="dimmed" style={{ flex: 1, minWidth: 0 }}>
                  Please confirm within 10 minutes to secure your selected time slot
                </Text>
                <SlotHoldCountdown onExpire={handleHoldExpired} />
              </Group>
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
            <Collapse expanded={atlOptionId !== null}>
              <Box pt="xs">
                <AtlOptionList
                  options={atlOptions}
                  selectedId={atlOptionId}
                  onSelect={setAtlOptionId}
                />
                <Textarea
                  label={atlNotesRequired ? 'Additional details' : 'Additional details (optional)'}
                  required={atlNotesRequired}
                  value={accessNotes}
                  onChange={(e) => setAccessNotes(e.currentTarget.value)}
                  error={showFieldError('accessNotes')}
                  // Matches tucJob.ucjbToSpecial. A hard cap with no counter reads
                  // as the field being broken, so show the remaining room.
                  maxLength={ACCESS_NOTES_MAX}
                  description={`${accessNotes.length}/${ACCESS_NOTES_MAX}`}
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

        <PoweredByFooter clientName={summary.airlineLabel} supportPhone={summary.supportPhone} />
      </Container>

      {/* Chrome and breakpoint behaviour live in index.css — see .pax-action-bar. */}
      <Box px="md" className="pax-action-bar">
        <Container size={520} px={0}>
          <Button
            size="lg"
            fullWidth
            disabled={!online}
            onClick={review}
            rightSection={<ArrowRightIcon size={18} />}
            style={{ minHeight: 56, fontSize: 16, fontWeight: 700, letterSpacing: '0.01em' }}
          >
            Review and confirm
          </Button>
        </Container>
      </Box>

      <ConfirmReviewModal
        opened={reviewOpen}
        onClose={closeReview}
        onConfirm={submit}
        submitting={confirm.isPending}
        online={online}
        slot={selectedSlot}
        address={address}
        passengerName={passengerName}
        passengerPhone={passengerPhone}
        passengerEmail={passengerEmail}
        atlOption={selectedAtlOption}
        accessNotes={accessNotes}
      />
    </Box>
  )
}

/**
 * Docket lines for the address, dropping the parts this country doesn't use.
 * Suburb and city are separate places and get a comma between them; the postcode
 * belongs to the city, so it follows on a space as it would on an envelope.
 */
function addressLines(a: AddressDto): string[] {
  const locality = [[a.suburb, a.city].map((p) => (p ?? '').trim()).filter(Boolean).join(', '), a.postCode]
    .map((part) => (part ?? '').trim())
    .filter(Boolean)
    .join(' ')
  return [a.line1, locality, a.country]
    .map((line) => (line ?? '').trim())
    .filter(Boolean)
}

/**
 * `onTint` keeps the label inside the brand-tinted stamp: the page-level dimmed
 * grey is mixed for the body background, not the tile, and loses contrast on it.
 */
function DocketLabel({ children, onTint }: { children: ReactNode; onTint?: boolean }) {
  return (
    <Text
      c={onTint ? undefined : 'dimmed'}
      mb={4}
      style={{
        fontSize: 10,
        fontWeight: 700,
        letterSpacing: '0.09em',
        textTransform: 'uppercase',
        opacity: onTint ? 0.75 : undefined,
      }}
    >
      {children}
    </Text>
  )
}

/**
 * The read-back between tapping the bar button and the POST. Deliberately not
 * shaped like the form behind it — no inputs, no section icons — so it reads as
 * the record about to be filed rather than one more step to fill in.
 */
export function ConfirmReviewModal({
  opened,
  onClose,
  onConfirm,
  submitting,
  online,
  slot,
  address,
  passengerName,
  passengerPhone,
  passengerEmail,
  atlOption,
  accessNotes,
}: {
  opened: boolean
  onClose: () => void
  onConfirm: () => void
  submitting: boolean
  online: boolean
  slot: TimeSlot | undefined
  address: AddressDto
  passengerName: string
  passengerPhone: string
  passengerEmail: string
  atlOption: AtlOption | undefined
  accessNotes: string
}) {
  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title="Check your delivery details"
      size={480}
      // A tap outside mid-flight would hide a request the passenger can't retry.
      closeOnClickOutside={!submitting}
      closeOnEscape={!submitting}
      withCloseButton={!submitting}
      transitionProps={{ transition: 'pop', duration: tokens.duration.fast }}
      styles={{ title: { fontWeight: 700, fontSize: 18, letterSpacing: '-0.01em' } }}
    >
      <Text size="sm" c="dimmed" mb="md">
        We'll book this as soon as you confirm.
      </Text>

      <Stack gap="md">
        {slot && (
          // The one field with real consequences, stamped at 2px against the
          // pill buttons and the lg-radius shell.
          <Box
            px="md"
            py={12}
            style={{
              borderRadius: tokens.radius.tile,
              backgroundColor: 'var(--mantine-color-brand-light)',
              color: 'var(--mantine-color-brand-light-color)',
            }}
          >
            <DocketLabel onTint>Delivery window</DocketLabel>
            <Text fw={700} style={{ fontSize: 20, lineHeight: 1.2 }}>
              {slot.label}
            </Text>
            <Text size="sm">{slot.dayLabel}</Text>
          </Box>
        )}

        <Box>
          <DocketLabel>Deliver to</DocketLabel>
          {addressLines(address).map((line, i) => (
            <Text key={`${i}-${line}`} size="sm">
              {line}
            </Text>
          ))}
        </Box>

        <Divider />

        <Box>
          <DocketLabel>Contact</DocketLabel>
          <Text size="sm">{passengerName}</Text>
          <Text size="sm">{passengerPhone}</Text>
          <Text size="sm" style={{ wordBreak: 'break-word' }}>
            {passengerEmail}
          </Text>
        </Box>

        <Divider />

        <Box>
          <DocketLabel>Authority to leave</DocketLabel>
          <Text size="sm">{atlOption ? atlOption.name : 'Not authorised'}</Text>
          {atlOption ? (
            accessNotes.trim() && (
              <Text size="sm" c="dimmed" mt={2}>
                “{accessNotes.trim()}”
              </Text>
            )
          ) : (
            <Text size="sm" c="dimmed" mt={2}>
              Someone will need to be there to take the bag.
            </Text>
          )}
        </Box>
      </Stack>

      {/* Primary first, then the way back. "Edit details" sat above the confirm
          button as bare centred text, which read as a stray link rather than the
          other half of the choice. */}
      <Stack gap="xs" mt="xl">
        <Button
          size="lg"
          fullWidth
          loading={submitting}
          disabled={!online}
          onClick={onConfirm}
          style={{ minHeight: 52, fontWeight: 700 }}
        >
          Confirm delivery
        </Button>
        <Button variant="default" fullWidth disabled={submitting} onClick={onClose}>
          Edit details
        </Button>
      </Stack>
    </Modal>
  )
}

// Memoized for the same reason as SlotOption: the list is fixed for the booking, so
// it shouldn't re-render behind every keystroke in the fields above it. `onSelect`
// is the raw setState, which React keeps stable.
const AtlOptionList = memo(function AtlOptionList({
  options,
  selectedId,
  onSelect,
}: {
  options: AtlOption[]
  selectedId: number | null
  onSelect: (id: number) => void
}) {
  return (
    <Radio.Group
      value={selectedId === null ? '' : String(selectedId)}
      onChange={(v) => onSelect(Number(v))}
    >
      <Stack
        gap={6}
        style={
          options.length > 6 ? { maxHeight: 260, overflowY: 'auto', paddingRight: 8 } : undefined
        }
      >
        {options.map((opt) => (
          <Radio key={opt.id} value={String(opt.id)} label={opt.name} size="sm" />
        ))}
      </Stack>
    </Radio.Group>
  )
})

function AddressGate({
  confirmed,
  onChange,
  error,
}: {
  confirmed: boolean
  onChange: (value: boolean) => void
  error?: string
}) {
  return (
    <Box
      px="sm"
      py={10}
      style={{
        borderRadius: 'var(--mantine-radius-sm)',
        backgroundColor: confirmed
          ? 'var(--mantine-color-brand-light)'
          : 'var(--dd-surface-container-high)',
        border: `1px solid ${
          error ? 'var(--mantine-color-error)' : 'var(--mantine-color-default-border)'
        }`,
      }}
    >
      <Checkbox
        checked={confirmed}
        onChange={(e) => onChange(e.currentTarget.checked)}
        label="This address is correct"
        description={confirmed ? 'Untick to make a change.' : "We'll deliver your bag here."}
        error={error}
        size="sm"
      />
    </Box>
  )
}

function SectionCard({
  icon,
  title,
  subtitle,
  action,
  children,
}: {
  icon: ReactNode
  title: string
  subtitle?: string
  action?: ReactNode
  children: ReactNode
}) {
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
        {action}
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
        {/* Date above window, as in the mock — a time on its own leaves the
            passenger guessing which day it belongs to. The window carries the
            weight though: the day is context, the hours are the decision. */}
        <Text size="xs" c="dimmed">
          {slot.dayLabel}
        </Text>
        <Text size="sm" fw={600}>
          {slot.label}
        </Text>
        {slot.firstAvailable && (
          // Brand-toned, not green: a second accent inside an already brand-tinted
          // row reads as two competing signals in sixty pixels.
          <Text
            c="brand"
            style={{
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
    // Flex column so the sign-off lands on the bottom edge rather than floating
    // above a few hundred pixels of blank surface.
    <Box mih="100vh" style={{ display: 'flex', flexDirection: 'column' }}>
      {/* The Ink hero, same as the confirm screen it replaces. A full-bleed green
          band introduced a fourth brand colour at the most memorable moment and
          discarded the airline theme with it; the tick and the badge carry the
          success on their own. */}
      <Box
        px={{ base: 20, sm: 32 }}
        pt={{ base: 48, sm: 64 }}
        pb={{ base: 56, sm: 72 }}
        style={{ backgroundColor: 'var(--mantine-color-ink-9)', color: '#fff' }}
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
          {summary.reference && (
            <Text style={{ color: SCRIM_BODY }}>File Reference · {summary.reference}</Text>
          )}
        </Container>
      </Box>

      <Container
        size={520}
        px={{ base: 12, sm: 0 }}
        mt={{ base: -32, sm: -40 }}
        pb={48}
        // width:100% because Container centres itself with auto inline margins,
        // and an auto cross-axis margin opts a flex item out of stretching.
        style={{ position: 'relative', flex: 1, width: '100%' }}
      >
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
                {slot.dayLabel}
              </Text>
              <Text fw={600} c="dimmed" style={{ fontSize: 'clamp(16px, 4vw, 18px)' }}>
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

      <PoweredByFooter clientName={summary.airlineLabel} supportPhone={summary.supportPhone} />
    </Box>
  )
}
