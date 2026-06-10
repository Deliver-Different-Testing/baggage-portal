import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Container,
  Divider,
  FormControlLabel,
  Radio,
  RadioGroup,
  Snackbar,
  Stack,
  Switch,
  TextField,
  Typography,
} from '@mui/material'
import { alpha } from '@mui/material/styles'
import {
  confirmBooking,
  getBooking,
  getTimeslots,
} from '../api/pax'
import type { AddressDto, BookingSummary, TimeSlot } from '../api/client'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import { AddressAutocomplete } from '../components/AddressAutocomplete'

const ATL_OFF = 'None'

export function PaxMobile() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const online = useOnlineStatus()

  const booking = useQuery({
    queryKey: ['pax', 'booking', id],
    queryFn: () => getBooking(id ?? ''),
    enabled: !!id,
    retry: false,
  })

  useEffect(() => {
    if (booking.error && (booking.error as { normalisedKind?: string })?.normalisedKind === 'not_found') {
      navigate('/expired', { replace: true })
    }
  }, [booking.error, navigate])

  if (!id) return null

  return booking.data ? (
    <ConfirmForm bookingId={id} summary={booking.data} online={online} />
  ) : booking.isLoading ? (
    <Container sx={{ pt: 8, textAlign: 'center' }}>
      <Typography color="text.secondary">Loading your booking…</Typography>
    </Container>
  ) : (
    <Container sx={{ pt: 8 }}>
      <Alert severity="error">We could not load this booking. Please contact support.</Alert>
    </Container>
  )
}

function ConfirmForm({
  bookingId,
  summary,
  online,
}: {
  bookingId: string
  summary: BookingSummary
  online: boolean
}) {
  const [address, setAddress] = useState<AddressDto>(summary.deliveryAddress)
  const [editingAddress, setEditingAddress] = useState(false)
  const [editingDetails, setEditingDetails] = useState(false)
  const [passengerName, setPassengerName] = useState(summary.passengerName ?? '')
  const [passengerPhone, setPassengerPhone] = useState(summary.passengerPhone ?? '')
  const [passengerEmail, setPassengerEmail] = useState(summary.passengerEmail ?? '')
  const [selectedSlotId, setSelectedSlotId] = useState<string | null>(null)
  const [atlOptionName, setAtlOptionName] = useState<string>(ATL_OFF)
  const [accessNotes, setAccessNotes] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [confirmed, setConfirmed] = useState(false)
  const [submitAttempted, setSubmitAttempted] = useState(false)

  const slots = useQuery({
    queryKey: ['pax', 'timeslots', bookingId],
    queryFn: () => getTimeslots(bookingId),
  })

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

  const sortedAtlOptions = useMemo(
    () => [...summary.atlOptions].sort((a, b) => a.id - b.id),
    [summary.atlOptions],
  )

  const fieldErrors = useMemo(() => {
    const errors: { [key: string]: string } = {}
    if (!passengerName.trim()) errors.passengerName = 'Please enter your full name.'
    if (!passengerPhone.trim()) {
      errors.passengerPhone = 'Please enter your phone number.'
    } else if (passengerPhone.replace(/\D/g, '').length < 7) {
      errors.passengerPhone = 'Please enter a valid phone number.'
    }
    if (!passengerEmail.trim()) {
      errors.passengerEmail = 'Please enter your email address.'
    } else if (!passengerEmail.includes('@')) {
      errors.passengerEmail = 'Please enter a valid email address.'
    }
    if (!address.line1.trim()) errors.line1 = 'Please enter your street address.'
    if (!(address.suburb ?? '').trim()) errors.suburb = 'Please enter your suburb.'
    if (!address.city.trim()) errors.city = 'Please enter your city.'
    if (!(address.postCode ?? '').trim()) errors.postCode = 'Please enter your postcode.'
    if (!selectedSlot) errors.slot = 'Please pick a delivery time slot.'
    return errors
  }, [passengerName, passengerPhone, passengerEmail, address, selectedSlot])

  const showFieldError = (key: string) =>
    submitAttempted ? fieldErrors[key] : undefined

  const confirm = useMutation({
    mutationFn: (body: Parameters<typeof confirmBooking>[1]) => confirmBooking(bookingId, body),
    onSuccess: () => setConfirmed(true),
    onError: (err) => {
      const responseStatus = (err as { response?: { status?: number } })?.response?.status
      if (responseStatus === 409) setError('This booking has already been confirmed.')
      else setError('Could not submit your confirmation. Please try again.')
    },
  })

  function submit() {
    setSubmitAttempted(true)
    setError(null)
    if (Object.keys(fieldErrors).length > 0) {
      setError('Please complete all required fields before confirming.')
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
      atlOption: atlOptionName,
      accessNotes: accessNotes || null,
      passengerName: passengerName.trim(),
      passengerPhone: passengerPhone.trim(),
      passengerEmail: passengerEmail.trim(),
    })
  }

  if (confirmed) return <ConfirmedScreen summary={summary} slot={selectedSlot} />

  return (
    <Container
      maxWidth="sm"
      disableGutters
      sx={{ maxWidth: 428, mx: 'auto', pb: 12, pt: 2 }}
    >
      <Stack spacing={2} sx={{ p: 2 }}>
        <Card sx={{ overflow: 'hidden' }}>
          <Box sx={{ bgcolor: 'primary.main', color: 'primary.contrastText', p: 3 }}>
            <Typography variant="h2" color="inherit" component="div" sx={{ fontSize: 18 }}>
              {summary.airlineLabel}
            </Typography>
            <Typography variant="body2" color="inherit" sx={{ opacity: 0.8 }}>
              Confirm your baggage delivery
            </Typography>
            <Typography
              variant="caption"
              color="inherit"
              sx={{
                mt: 1, display: 'inline-block', bgcolor: 'rgba(255,255,255,0.12)',
                px: 1.5, py: 0.5, borderRadius: 999, fontFamily: 'monospace',
              }}
            >
              Ref: {summary.jobId}
            </Typography>
          </Box>
          <CardContent>
            <Stack spacing={2.5} divider={<Divider flexItem />}>
              <Section
                title="Your details"
                action={
                  <Button size="small" onClick={() => setEditingDetails((s) => !s)}>
                    {editingDetails ? 'Save' : 'Edit'}
                  </Button>
                }
              >
                <Stack spacing={1.5}>
                  <TextField
                    label="Full name"
                    value={passengerName}
                    onChange={(e) => setPassengerName(e.target.value)}
                    autoComplete="name"
                    required
                    fullWidth
                    disabled={!editingDetails}
                    error={!!showFieldError('passengerName')}
                    helperText={showFieldError('passengerName')}
                  />
                  <TextField
                    label="Phone number"
                    value={passengerPhone}
                    onChange={(e) => setPassengerPhone(e.target.value)}
                    autoComplete="tel"
                    inputMode="tel"
                    required
                    fullWidth
                    disabled={!editingDetails}
                    error={!!showFieldError('passengerPhone')}
                    helperText={showFieldError('passengerPhone')}
                  />
                  <TextField
                    label="Email address"
                    value={passengerEmail}
                    onChange={(e) => setPassengerEmail(e.target.value)}
                    autoComplete="email"
                    type="email"
                    required
                    fullWidth
                    disabled={!editingDetails}
                    error={!!showFieldError('passengerEmail')}
                    helperText={showFieldError('passengerEmail')}
                  />
                </Stack>
              </Section>

              <Section
                title="Delivery address"
                action={
                  <Button size="small" onClick={() => setEditingAddress((s) => !s)}>
                    {editingAddress ? 'Save' : 'Edit'}
                  </Button>
                }
              >
                <Stack spacing={1.5}>
                  {editingAddress && (
                    <AddressAutocomplete
                      bookingId={bookingId}
                      onAddressSelect={(detail) =>
                        setAddress({
                          ...address,
                          line1: detail.street,
                          suburb: detail.suburb || null,
                          city: detail.city,
                          postCode: detail.postalCode || null,
                          country: detail.countryCode || address.country,
                        })
                      }
                    />
                  )}
                  <TextField
                    label="Street address"
                    value={address.line1}
                    onChange={(e) => setAddress({ ...address, line1: e.target.value })}
                    required
                    fullWidth
                    disabled={!editingAddress}
                    error={!!showFieldError('line1')}
                    helperText={showFieldError('line1')}
                  />
                  <TextField
                    label="Suburb"
                    value={address.suburb ?? ''}
                    onChange={(e) => setAddress({ ...address, suburb: e.target.value })}
                    required
                    fullWidth
                    disabled={!editingAddress}
                    error={!!showFieldError('suburb')}
                    helperText={showFieldError('suburb')}
                  />
                  <Stack direction="row" spacing={1}>
                    <TextField
                      label="City"
                      value={address.city}
                      onChange={(e) => setAddress({ ...address, city: e.target.value })}
                      required
                      fullWidth
                      disabled={!editingAddress}
                      error={!!showFieldError('city')}
                      helperText={showFieldError('city')}
                    />
                    <TextField
                      label="Postcode"
                      value={address.postCode ?? ''}
                      onChange={(e) => setAddress({ ...address, postCode: e.target.value })}
                      required
                      sx={{ width: 120 }}
                      disabled={!editingAddress}
                      error={!!showFieldError('postCode')}
                      helperText={showFieldError('postCode')}
                    />
                  </Stack>
                </Stack>
              </Section>

              <Section title="Select delivery time">
                {slots.isLoading && <Typography color="text.secondary">Loading slots…</Typography>}
                {showFieldError('slot') && (
                  <Typography variant="body2" color="error" sx={{ mb: 1 }}>
                    {showFieldError('slot')}
                  </Typography>
                )}
                {slots.data && (
                  <RadioGroup
                    value={effectiveSlotId ?? ''}
                    onChange={(e) => setSelectedSlotId(e.target.value)}
                  >
                    {slots.data.map((slot) => (
                      <FormControlLabel
                        key={slot.id}
                        value={slot.id}
                        control={<Radio />}
                        sx={(theme) => ({
                          m: 0,
                          mb: 1,
                          p: 1,
                          border: 1,
                          borderColor: 'divider',
                          borderRadius: 1.5,
                          ...(slot.id === effectiveSlotId && {
                            borderColor: theme.palette.primary.main,
                            bgcolor: alpha(theme.palette.primary.main, 0.08),
                          }),
                        })}
                        label={
                          <Stack>
                            <Typography variant="body2" sx={{ fontWeight: 700 }}>{slot.label}</Typography>
                            {slot.firstAvailable && (
                              <Typography variant="caption" color="success.main">
                                FIRST AVAILABLE
                              </Typography>
                            )}
                          </Stack>
                        }
                      />
                    ))}
                  </RadioGroup>
                )}
              </Section>

              <Section
                title="Authority to Leave"
                subtitle="Leave baggage unattended if not home"
                action={
                  <Switch
                    checked={atlOptionName !== ATL_OFF}
                    disabled={sortedAtlOptions.length === 0}
                    onChange={(_, on) =>
                      setAtlOptionName(on ? sortedAtlOptions[0]?.name ?? ATL_OFF : ATL_OFF)
                    }
                  />
                }
              >
                {atlOptionName !== ATL_OFF && (
                  <>
                    <RadioGroup
                      value={atlOptionName}
                      onChange={(e) => setAtlOptionName(e.target.value)}
                      sx={
                        sortedAtlOptions.length > 8
                          ? { maxHeight: 304, overflowY: 'auto', pr: 1 }
                          : undefined
                      }
                    >
                      {sortedAtlOptions.map((opt) => (
                        <FormControlLabel
                          key={opt.id}
                          value={opt.name}
                          control={<Radio />}
                          label={opt.name}
                        />
                      ))}
                    </RadioGroup>
                    <TextField
                      label="Additional details (optional)"
                      value={accessNotes}
                      onChange={(e) => setAccessNotes(e.target.value)}
                      fullWidth
                      multiline
                      minRows={2}
                      sx={{ mt: 1.5 }}
                    />
                  </>
                )}
              </Section>
            </Stack>
          </CardContent>
        </Card>

        {error && <Alert severity="error">{error}</Alert>}
        {!online && (
          <Alert severity="warning">
            You appear to be offline. Connect to the internet to submit your confirmation.
          </Alert>
        )}

        <Button
          variant="contained"
          color="primary"
          size="large"
          fullWidth
          disabled={!online || confirm.isPending}
          onClick={submit}
        >
          {confirm.isPending ? 'Submitting…' : 'Confirm delivery'}
        </Button>
      </Stack>

      <Snackbar
        open={!!error}
        autoHideDuration={4000}
        onClose={() => setError(null)}
        message={error ?? ''}
      />
    </Container>
  )
}

function Section({
  title,
  subtitle,
  action,
  children,
}: {
  title: string
  subtitle?: string
  action?: ReactNode
  children: ReactNode
}) {
  return (
    <Box>
      <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center', mb: subtitle ? 0 : 1 }}>
        <Box>
          <Typography variant="h3">{title}</Typography>
          {subtitle && (
            <Typography variant="body2" color="text.secondary">
              {subtitle}
            </Typography>
          )}
        </Box>
        {action}
      </Stack>
      {children}
    </Box>
  )
}

function ConfirmedScreen({ summary, slot }: { summary: BookingSummary; slot: TimeSlot | undefined }) {
  return (
    <Container maxWidth="sm" sx={{ pt: 4, maxWidth: 428, mx: 'auto' }}>
      <Card>
        <CardContent>
          <Box sx={{
            width: 56, height: 56, borderRadius: '50%',
            bgcolor: 'success.main', color: 'common.white',
            display: 'grid', placeItems: 'center', fontSize: 28, mb: 2, mx: 'auto',
          }}>
            ✓
          </Box>
          <Typography variant="h2" align="center">Booking confirmed</Typography>
          <Typography variant="body2" align="center" color="text.secondary" sx={{ mt: 1 }}>
            Ref: {summary.jobId}
          </Typography>
          <Divider sx={{ my: 2 }} />
          {slot && (
            <Stack spacing={0.5}>
              <Typography variant="body2" color="text.secondary">Delivery window</Typography>
              <Typography>{slot.label}</Typography>
            </Stack>
          )}
          <Typography sx={{ mt: 3 }} color="text.secondary" variant="body2">
            We will text you when our driver is on the way. You can track your
            delivery from the link in that message.
          </Typography>
        </CardContent>
      </Card>
    </Container>
  )
}
