import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, type UseQueryResult } from '@tanstack/react-query'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Chip from '@mui/material/Chip'
import CircularProgress from '@mui/material/CircularProgress'
import Collapse from '@mui/material/Collapse'
import Container from '@mui/material/Container'
import IconButton from '@mui/material/IconButton'
import FormControlLabel from '@mui/material/FormControlLabel'
import Radio from '@mui/material/Radio'
import RadioGroup from '@mui/material/RadioGroup'
import Snackbar from '@mui/material/Snackbar'
import Stack from '@mui/material/Stack'
import Switch from '@mui/material/Switch'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import { alpha, ThemeProvider } from '@mui/material/styles'
import PersonOutlineRoundedIcon from '@mui/icons-material/PersonOutlineRounded'
import PlaceOutlinedIcon from '@mui/icons-material/PlaceOutlined'
import ScheduleRoundedIcon from '@mui/icons-material/ScheduleRounded'
import LockOutlinedIcon from '@mui/icons-material/LockOutlined'
import EditOutlinedIcon from '@mui/icons-material/EditOutlined'
import CheckRoundedIcon from '@mui/icons-material/CheckRounded'
import CheckCircleRoundedIcon from '@mui/icons-material/CheckCircleRounded'
import LuggageRoundedIcon from '@mui/icons-material/LuggageRounded'
import ArrowForwardRoundedIcon from '@mui/icons-material/ArrowForwardRounded'
import {
  confirmBooking,
  getBooking,
  getTimeslots,
} from '../api/pax'
import type { AddressDto, BookingSummary, TimeSlot } from '../api/client'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import { AddressAutocomplete } from '../components/AddressAutocomplete'
import { PoweredByFooter } from '../components/PoweredByFooter'
import { createAppTheme } from '../styles/theme'
import { getAirlineBrand } from '../styles/airlineBranding'

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

  // Timeslots depend only on the URL id, not the booking response — fetch them
  // in parallel with the booking instead of waiting for ConfirmForm to mount.
  const slots = useQuery({
    queryKey: ['pax', 'timeslots', id],
    queryFn: () => getTimeslots(id ?? ''),
    enabled: !!id,
  })

  useEffect(() => {
    if (booking.error && (booking.error as { normalisedKind?: string })?.normalisedKind === 'not_found') {
      navigate('/expired', { replace: true })
    }
  }, [booking.error, navigate])

  if (!id) return null

  if (booking.isLoading) {
    return (
      <Box
        sx={{
          minHeight: '100vh',
          display: 'grid',
          placeItems: 'center',
        }}
      >
        <Stack spacing={2} sx={{ alignItems: 'center' }}>
          <CircularProgress size={32} />
          <Typography variant="body2" color="text.secondary">
            Loading your booking…
          </Typography>
        </Stack>
      </Box>
    )
  }

  if (!booking.data) {
    return (
      <Container sx={{ pt: 8 }}>
        <Alert severity="error" variant="outlined">
          We could not load this booking. Please contact support.
        </Alert>
      </Container>
    )
  }

  return <BrandedConfirmForm bookingId={id} summary={booking.data} online={online} slots={slots} />
}

// Re-theme the passenger flow from the airline code on the booking. The root
// ThemeProvider (main.tsx) stays the default for loading/expired states; this
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
  const airlineTheme = useMemo(
    () => createAppTheme(true, getAirlineBrand(summary.airlineCode)),
    [summary.airlineCode],
  )

  return (
    <ThemeProvider theme={airlineTheme}>
      <ConfirmForm bookingId={bookingId} summary={summary} online={online} slots={slots} />
    </ThemeProvider>
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
  const [error, setError] = useState<string | null>(null)
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
  const atlNotesRequired =
    selectedAtlOption?.name.trim().toLowerCase() === 'safe place'
  if (atlNotesRequired && !accessNotes.trim()) {
    fieldErrors.accessNotes = 'Please describe the safe place to leave your baggage.'
  }

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
      atlOptionId,
      accessNotes: accessNotes || null,
      passengerName: passengerName.trim(),
      passengerPhone: passengerPhone.trim(),
      passengerEmail: passengerEmail.trim(),
    })
  }

  if (confirmed) return <ConfirmedScreen summary={summary} slot={selectedSlot} bookingId={bookingId} />

  return (
    <Box sx={{ minHeight: '100vh', pb: { xs: 14, sm: 16 } }}>
      <Box
        sx={(theme) => ({
          backgroundColor: theme.palette.primary.main,
          color: theme.palette.primary.contrastText,
          px: { xs: 2.5, sm: 4 },
          pt: { xs: 4, sm: 5 },
          pb: { xs: 6, sm: 7 },
          position: 'relative',
          overflow: 'hidden',
        })}
      >
        <Container
          maxWidth="sm"
          sx={{ maxWidth: 520, mx: 'auto', px: '0 !important', position: 'relative' }}
        >
          <Stack
            direction="row"
            spacing={1.5}
            sx={{ alignItems: 'center', mb: 3 }}
          >
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
                {summary.airlineLabel}
              </Typography>
              <Typography
                variant="body2"
                sx={{ fontWeight: 700, letterSpacing: '0.04em', fontSize: 13 }}
              >
                REF · {summary.jobId}
              </Typography>
            </Box>
          </Stack>

          <Typography
            variant="h1"
            sx={{
              fontSize: { xs: 32, sm: 40 },
              lineHeight: 1.05,
              mb: 1.5,
              color: 'inherit',
              fontWeight: 700,
              letterSpacing: '-0.03em',
            }}
          >
            Confirm your
            <br />
            baggage delivery
          </Typography>
          <Typography
            variant="body1"
            sx={{ opacity: 0.85, maxWidth: 400, fontSize: { xs: 14.5, sm: 16 } }}
          >
            Review your details below and pick a delivery window. We'll text
            you when our driver is on the way.
          </Typography>

          <StepProgress current={1} steps={['Confirm', 'In transit', 'Delivered']} />
        </Container>
      </Box>

      <Container
        maxWidth="sm"
        sx={{ maxWidth: 520, mx: 'auto', mt: { xs: -3.5, sm: -4 }, position: 'relative', zIndex: 1 }}
      >
        <Stack spacing={2}>

          <SectionCard
            icon={<PersonOutlineRoundedIcon fontSize="small" />}
            title="Your details"
            onToggleEdit={() => setEditingDetails((s) => !s)}
            editing={editingDetails}
          >
            <Stack spacing={1.5}>
              <TextField
                label="Full name"
                value={passengerName}
                onChange={(e) => setPassengerName(e.target.value)}
                autoComplete="name"
                required
                fullWidth
                size="small"
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
                size="small"
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
                size="small"
                disabled={!editingDetails}
                error={!!showFieldError('passengerEmail')}
                helperText={showFieldError('passengerEmail')}
              />
            </Stack>
          </SectionCard>

          <SectionCard
            icon={<PlaceOutlinedIcon fontSize="small" />}
            title="Delivery address"
            onToggleEdit={() => setEditingAddress((s) => !s)}
            editing={editingAddress}
          >
            <Stack spacing={1.5}>
              {editingAddress && (
                <AddressAutocomplete
                  bookingId={bookingId}
                  onAddressSelect={(detail) =>
                    setAddress((a) => ({
                      ...a,
                      line1: detail.street,
                      suburb: detail.suburb || null,
                      city: detail.city,
                      postCode: detail.postalCode || null,
                      country: detail.countryCode || a.country,
                    }))
                  }
                />
              )}
              <TextField
                label="Street address"
                value={address.line1}
                onChange={(e) => setAddress((a) => ({ ...a, line1: e.target.value }))}
                required
                fullWidth
                size="small"
                disabled={!editingAddress}
                error={!!showFieldError('line1')}
                helperText={showFieldError('line1')}
              />
              <TextField
                label="Suburb"
                value={address.suburb ?? ''}
                onChange={(e) => setAddress((a) => ({ ...a, suburb: e.target.value }))}
                required
                fullWidth
                size="small"
                disabled={!editingAddress}
                error={!!showFieldError('suburb')}
                helperText={showFieldError('suburb')}
              />
              <Stack direction="row" spacing={1}>
                <TextField
                  label="City"
                  value={address.city}
                  onChange={(e) => setAddress((a) => ({ ...a, city: e.target.value }))}
                  required
                  fullWidth
                  size="small"
                  disabled={!editingAddress}
                  error={!!showFieldError('city')}
                  helperText={showFieldError('city')}
                />
                <TextField
                  label="Postcode"
                  value={address.postCode ?? ''}
                  onChange={(e) => setAddress((a) => ({ ...a, postCode: e.target.value }))}
                  required
                  size="small"
                  sx={{ width: 132 }}
                  disabled={!editingAddress}
                  error={!!showFieldError('postCode')}
                  helperText={showFieldError('postCode')}
                />
              </Stack>
            </Stack>
          </SectionCard>

          <SectionCard
            icon={<ScheduleRoundedIcon fontSize="small" />}
            title="Delivery time"
          >
            {slots.isLoading && (
              <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', py: 1 }}>
                <CircularProgress size={18} />
                <Typography variant="body2" color="text.secondary">
                  Loading available windows…
                </Typography>
              </Stack>
            )}
            {showFieldError('slot') && (
              <Typography variant="body2" color="error" sx={{ mb: 1 }}>
                {showFieldError('slot')}
              </Typography>
            )}
            {slots.data && (
              <Stack spacing={1}>
                {slots.data.map((slot) => (
                  <SlotOption
                    key={slot.id}
                    slot={slot}
                    selected={slot.id === effectiveSlotId}
                    onSelect={() => setSelectedSlotId(slot.id)}
                  />
                ))}
              </Stack>
            )}
          </SectionCard>

          <SectionCard
            icon={<LockOutlinedIcon fontSize="small" />}
            title="Authority to Leave"
            subtitle="Leave baggage unattended if you're not home"
            action={
              <Switch
                checked={atlOptionId !== null}
                disabled={atlOptions.length === 0}
                onChange={(_, on) =>
                  setAtlOptionId(on ? atlOptions[0]?.id ?? null : null)
                }
              />
            }
          >
            <Collapse in={atlOptionId !== null} unmountOnExit>
              <Box sx={{ pt: 1 }}>
                <RadioGroup
                  value={atlOptionId === null ? '' : String(atlOptionId)}
                  onChange={(e) => setAtlOptionId(Number(e.target.value))}
                  sx={
                    atlOptions.length > 6
                      ? { maxHeight: 260, overflowY: 'auto', pr: 1 }
                      : undefined
                  }
                >
                  {atlOptions.map((opt) => (
                    <FormControlLabel
                      key={opt.id}
                      value={String(opt.id)}
                      control={<Radio size="small" />}
                      label={
                        <Typography variant="body2">{opt.name}</Typography>
                      }
                      sx={{ ml: -0.5 }}
                    />
                  ))}
                </RadioGroup>
                <TextField
                  label={
                    atlNotesRequired
                      ? 'Additional details'
                      : 'Additional details (optional)'
                  }
                  required={atlNotesRequired}
                  value={accessNotes}
                  onChange={(e) => setAccessNotes(e.target.value)}
                  error={!!showFieldError('accessNotes')}
                  helperText={showFieldError('accessNotes')}
                  fullWidth
                  multiline
                  minRows={2}
                  size="small"
                  sx={{ mt: 1.5 }}
                />
              </Box>
            </Collapse>
          </SectionCard>

          {!online && (
            <Alert severity="warning" variant="outlined" sx={{ borderRadius: 2 }}>
              You appear to be offline. Connect to the internet to submit your confirmation.
            </Alert>
          )}
        </Stack>

        <PoweredByFooter />
      </Container>

      <Box
        sx={(theme) => ({
          position: 'fixed',
          left: 0,
          right: 0,
          bottom: 0,
          backgroundColor: theme.palette.background.paper,
          borderTop: `1px solid ${theme.palette.divider}`,
          px: 2,
          py: 1.75,
          pb: 'calc(env(safe-area-inset-bottom, 0px) + 14px)',
          zIndex: theme.zIndex.appBar,
          boxShadow: '0 -4px 16px -4px rgba(0,0,0,0.06)',
        })}
      >
        <Container maxWidth="sm" sx={{ maxWidth: 520, mx: 'auto', px: 0 }}>
          <Button
            variant="contained"
            color="primary"
            size="large"
            fullWidth
            disabled={!online || confirm.isPending}
            onClick={submit}
            endIcon={
              confirm.isPending ? (
                <CircularProgress size={18} color="inherit" />
              ) : (
                <ArrowForwardRoundedIcon />
              )
            }
            sx={{
              minHeight: 56,
              fontSize: 16,
              fontWeight: 700,
              letterSpacing: '0.01em',
            }}
          >
            {confirm.isPending ? 'Submitting…' : 'Confirm delivery'}
          </Button>
        </Container>
      </Box>

      <Snackbar
        open={!!error}
        autoHideDuration={4000}
        onClose={() => setError(null)}
        anchorOrigin={{ vertical: 'top', horizontal: 'center' }}
      >
        <Alert
          severity="error"
          variant="filled"
          onClose={() => setError(null)}
          sx={{ width: '100%' }}
        >
          {error}
        </Alert>
      </Snackbar>
    </Box>
  )
}

function StepProgress({
  current,
  steps,
}: {
  current: number
  steps: string[]
}) {
  return (
    <Stack
      direction="row"
      spacing={0}
      sx={{ alignItems: 'center', mt: 3.5, gap: 0.75 }}
    >
      {steps.map((step, idx) => {
        const isActive = idx === current - 1
        const isDone = idx < current - 1
        const isCurrent = isActive || isDone
        return (
          <Stack
            key={step}
            direction="row"
            spacing={0.75}
            sx={{ alignItems: 'center', flex: idx === steps.length - 1 ? 'unset' : 1 }}
          >
            <Box
              sx={{
                width: 22,
                height: 22,
                borderRadius: '50%',
                bgcolor: isCurrent ? 'rgba(255,255,255,0.95)' : 'rgba(255,255,255,0.22)',
                color: isCurrent ? 'primary.main' : 'inherit',
                display: 'grid',
                placeItems: 'center',
                fontSize: 11,
                fontWeight: 700,
                flexShrink: 0,
              }}
            >
              {isDone ? <CheckRoundedIcon sx={{ fontSize: 14 }} /> : idx + 1}
            </Box>
            <Typography
              variant="caption"
              sx={{
                fontWeight: isActive ? 700 : 500,
                opacity: isCurrent ? 1 : 0.65,
                fontSize: 12,
                whiteSpace: 'nowrap',
              }}
            >
              {step}
            </Typography>
            {idx < steps.length - 1 && (
              <Box
                sx={{
                  flex: 1,
                  height: 2,
                  bgcolor: 'rgba(255,255,255,0.22)',
                  borderRadius: 999,
                  ml: 0.75,
                }}
              />
            )}
          </Stack>
        )
      })}
    </Stack>
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
      <Tooltip title={editing ? `Save ${title}` : `Edit ${title}`}>
        <IconButton
          size="small"
          onClick={onToggleEdit}
          aria-label={editing ? `Save ${title}` : `Edit ${title}`}
          sx={(theme) => ({
            color: editing ? theme.palette.primary.main : theme.palette.text.secondary,
          })}
        >
          {editing ? <CheckRoundedIcon fontSize="small" /> : <EditOutlinedIcon fontSize="small" />}
        </IconButton>
      </Tooltip>
    ) : null)

  return (
    <Card>
      <CardContent>
        <Stack
          direction="row"
          spacing={1.5}
          sx={{ alignItems: 'center', mb: 2 }}
        >
          <Box
            sx={(theme) => ({
              width: 32,
              height: 32,
              borderRadius: theme.tokens.radius.sm,
              bgcolor: alpha(theme.palette.primary.main, 0.1),
              color: 'primary.main',
              display: 'grid',
              placeItems: 'center',
              flexShrink: 0,
            })}
          >
            {icon}
          </Box>
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Typography variant="h4" sx={{ lineHeight: 1.3 }}>
              {title}
            </Typography>
            {subtitle && (
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                {subtitle}
              </Typography>
            )}
          </Box>
          {trailing}
        </Stack>
        {children}
      </CardContent>
    </Card>
  )
}

function SlotOption({
  slot,
  selected,
  onSelect,
}: {
  slot: TimeSlot
  selected: boolean
  onSelect: () => void
}) {
  return (
    <Box
      role="radio"
      aria-checked={selected}
      tabIndex={0}
      onClick={onSelect}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault()
          onSelect()
        }
      }}
      sx={(theme) => ({
        display: 'flex',
        alignItems: 'center',
        gap: 1.5,
        px: 1.75,
        py: 1.5,
        borderRadius: theme.tokens.radius.md,
        border: `1px solid ${selected ? theme.palette.primary.main : theme.palette.divider}`,
        backgroundColor: selected
          ? alpha(theme.palette.primary.main, 0.06)
          : theme.palette.background.paper,
        cursor: 'pointer',
        transition: `all ${theme.tokens.duration.fast}ms ease`,
        outline: 'none',
        '&:hover': {
          borderColor: theme.palette.primary.main,
          backgroundColor: alpha(theme.palette.primary.main, 0.04),
        },
        '&:focus-visible': {
          boxShadow: `0 0 0 3px ${alpha(theme.palette.primary.main, 0.24)}`,
        },
      })}
    >
      <Box
        sx={(theme) => ({
          width: 20,
          height: 20,
          borderRadius: '50%',
          border: `2px solid ${selected ? theme.palette.primary.main : theme.palette.divider}`,
          display: 'grid',
          placeItems: 'center',
          flexShrink: 0,
          transition: `all ${theme.tokens.duration.fast}ms ease`,
        })}
      >
        {selected && (
          <Box
            sx={(theme) => ({
              width: 10,
              height: 10,
              borderRadius: '50%',
              backgroundColor: theme.palette.primary.main,
            })}
          />
        )}
      </Box>
      <Box sx={{ flex: 1, minWidth: 0 }}>
        <Typography variant="body2" sx={{ fontWeight: 600 }}>
          {slot.label}
        </Typography>
        {slot.firstAvailable && (
          <Typography
            variant="caption"
            sx={(theme) => ({
              color: theme.palette.success.dark,
              fontWeight: 600,
              letterSpacing: '0.06em',
              textTransform: 'uppercase',
              fontSize: 10,
            })}
          >
            First available
          </Typography>
        )}
      </Box>
    </Box>
  )
}

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
    <Box sx={{ minHeight: '100vh' }}>
      <Box
        sx={(theme) => ({
          backgroundColor: theme.palette.success.dark,
          color: theme.palette.success.contrastText,
          px: { xs: 2.5, sm: 4 },
          pt: { xs: 6, sm: 8 },
          pb: { xs: 7, sm: 9 },
        })}
      >
        <Container maxWidth="sm" sx={{ maxWidth: 520, mx: 'auto', textAlign: 'center', px: '0 !important' }}>
          <Box
            sx={{
              width: 80,
              height: 80,
              borderRadius: '50%',
              bgcolor: 'rgba(255,255,255,0.16)',
              display: 'grid',
              placeItems: 'center',
              mx: 'auto',
              mb: 3,
            }}
          >
            <CheckCircleRoundedIcon sx={{ fontSize: 48 }} />
          </Box>
          <Chip
            label="Confirmed"
            size="small"
            sx={{
              bgcolor: 'rgba(255,255,255,0.18)',
              color: 'inherit',
              fontWeight: 700,
              letterSpacing: '0.08em',
              textTransform: 'uppercase',
              fontSize: 10.5,
              mb: 2,
              borderRadius: 999,
            }}
          />
          <Typography
            variant="h1"
            sx={{
              fontSize: { xs: 30, sm: 38 },
              color: 'inherit',
              mb: 1,
              fontWeight: 700,
              letterSpacing: '-0.03em',
            }}
          >
            You're all set
          </Typography>
          <Typography variant="body1" sx={{ opacity: 0.88 }}>
            REF · {summary.jobId}
          </Typography>
        </Container>
      </Box>

      <Container
        maxWidth="sm"
        sx={{ maxWidth: 520, mx: 'auto', mt: { xs: -4, sm: -5 }, position: 'relative', pb: 6 }}
      >
        <Stack spacing={2}>
          {slot && (
            <Card>
              <CardContent>
                <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', mb: 1.5 }}>
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
                    <ScheduleRoundedIcon fontSize="small" />
                  </Box>
                  <Typography variant="h6" sx={{ flex: 1 }}>Delivery window</Typography>
                </Stack>
                <Typography variant="h2" sx={{ fontSize: { xs: 24, sm: 28 } }}>
                  {slot.label}
                </Typography>
              </CardContent>
            </Card>
          )}

          <Button
            component={RouterLink}
            to={`/t/${bookingId}`}
            variant="contained"
            size="large"
            fullWidth
            endIcon={<ArrowForwardRoundedIcon />}
            sx={{ py: 1.5, fontWeight: 600 }}
          >
            Track your delivery
          </Button>

          <Card sx={(theme) => ({ bgcolor: alpha(theme.palette.primary.main, 0.06), border: 'none' })}>
            <CardContent>
              <Typography variant="body2" color="text.primary" sx={{ fontWeight: 500 }}>
                We'll also text you when our driver is on the way.
              </Typography>
            </CardContent>
          </Card>
        </Stack>
      </Container>

      <PoweredByFooter />
    </Box>
  )
}
