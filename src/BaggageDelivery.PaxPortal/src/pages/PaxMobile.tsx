import { useEffect, useMemo, useState } from 'react'
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
import {
  confirmBooking,
  getBooking,
  getTimeslots,
} from '../api/pax'
import type { AddressDto, BookingSummary, TimeSlot } from '../api/client'
import { useOnlineStatus } from '../hooks/useOnlineStatus'

type AtlOption = 'None' | 'FrontDoor' | 'BackDoor' | 'Garage' | 'Reception' | 'Neighbour' | 'Other'

const ATL_LABELS: Record<AtlOption, string> = {
  None: 'No — I will be home',
  FrontDoor: 'Front door',
  BackDoor: 'Back door',
  Garage: 'Garage',
  Reception: 'Building reception',
  Neighbour: 'With a neighbour',
  Other: 'Other location',
}

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
  const [selectedSlotId, setSelectedSlotId] = useState<string | null>(null)
  const [atl, setAtl] = useState<AtlOption>('None')
  const [accessNotes, setAccessNotes] = useState('')
  const [phoneOverride, setPhoneOverride] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [confirmed, setConfirmed] = useState(false)

  const slots = useQuery({
    queryKey: ['pax', 'timeslots', bookingId],
    queryFn: () => getTimeslots(bookingId),
  })

  useEffect(() => {
    if (slots.data && slots.data.length && !selectedSlotId) {
      const first = slots.data.find((s) => s.firstAvailable) ?? slots.data[0]
      setSelectedSlotId(first.id)
    }
  }, [slots.data, selectedSlotId])

  const selectedSlot: TimeSlot | undefined = useMemo(
    () => slots.data?.find((s) => s.id === selectedSlotId),
    [slots.data, selectedSlotId],
  )

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
    setError(null)
    if (!selectedSlot) {
      setError('Please pick a delivery time slot.')
      return
    }
    if (atl === 'Other' && !accessNotes.trim()) {
      setError('Please describe where to leave the bag.')
      return
    }
    confirm.mutate({
      address,
      timeSlotStartUtc: selectedSlot.startUtc,
      timeSlotEndUtc: selectedSlot.endUtc,
      atlOption: atl,
      accessNotes: accessNotes || null,
      phoneOverride: phoneOverride || null,
    })
  }

  if (confirmed) return <ConfirmedScreen summary={summary} slot={selectedSlot} />

  return (
    <Container
      maxWidth="sm"
      disableGutters
      sx={{ maxWidth: 428, mx: 'auto', pb: 12 }}
    >
      <Box sx={{ bgcolor: 'primary.main', color: 'common.white', p: 3 }}>
        <Typography variant="h2" color="inherit" component="div" sx={{ fontSize: 18 }}>
          {summary.airlineLabel}
        </Typography>
        <Typography variant="body2" sx={{ opacity: 0.8 }}>
          Confirm your baggage delivery
        </Typography>
        <Typography
          variant="caption"
          sx={{
            mt: 1, display: 'inline-block', bgcolor: 'rgba(255,255,255,0.12)',
            px: 1.5, py: 0.5, borderRadius: 999, fontFamily: 'monospace',
          }}
        >
          Ref: {summary.reference}
        </Typography>
      </Box>

      <Stack spacing={2} sx={{ p: 2 }}>
        <Card>
          <CardContent>
            <Typography variant="h3">Your details</Typography>
            <Divider sx={{ my: 1 }} />
            <Typography variant="body1">{summary.passengerName}</Typography>
            <Typography variant="body2" color="text.secondary">{summary.passengerPhone}</Typography>
            <Typography variant="body2" color="text.secondary">{summary.passengerEmail}</Typography>
          </CardContent>
        </Card>

        <Card>
          <CardContent>
            <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center' }}>
              <Typography variant="h3">Delivery address</Typography>
              <Button
                size="small"
                onClick={() => setEditingAddress((s) => !s)}
              >
                {editingAddress ? 'Save' : 'Edit'}
              </Button>
            </Stack>
            <Divider sx={{ my: 1 }} />
            {editingAddress ? (
              <Stack spacing={1.5}>
                <TextField
                  label="Street address"
                  value={address.line1}
                  onChange={(e) => setAddress({ ...address, line1: e.target.value })}
                  fullWidth
                />
                <TextField
                  label="Suburb"
                  value={address.suburb ?? ''}
                  onChange={(e) => setAddress({ ...address, suburb: e.target.value })}
                  fullWidth
                />
                <Stack direction="row" spacing={1}>
                  <TextField
                    label="City"
                    value={address.city}
                    onChange={(e) => setAddress({ ...address, city: e.target.value })}
                    fullWidth
                  />
                  <TextField
                    label="Postcode"
                    value={address.postCode ?? ''}
                    onChange={(e) => setAddress({ ...address, postCode: e.target.value })}
                    sx={{ width: 120 }}
                  />
                </Stack>
              </Stack>
            ) : (
              <Box>
                <Typography>{address.line1}</Typography>
                {address.suburb && <Typography>{address.suburb}</Typography>}
                <Typography>
                  {[address.city, address.postCode].filter(Boolean).join(' ')}
                </Typography>
              </Box>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardContent>
            <Typography variant="h3">Select delivery time</Typography>
            <Divider sx={{ my: 1 }} />
            {slots.isLoading && <Typography color="text.secondary">Loading slots…</Typography>}
            {slots.data && (
              <RadioGroup
                value={selectedSlotId ?? ''}
                onChange={(e) => setSelectedSlotId(e.target.value)}
              >
                {slots.data.map((slot) => (
                  <FormControlLabel
                    key={slot.id}
                    value={slot.id}
                    control={<Radio />}
                    sx={{
                      m: 0,
                      mb: 1,
                      p: 1,
                      border: 1,
                      borderColor: 'divider',
                      borderRadius: 1.5,
                      ...(slot.id === selectedSlotId && {
                        borderColor: 'secondary.main',
                        bgcolor: 'secondary.light',
                      }),
                    }}
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
          </CardContent>
        </Card>

        <Card>
          <CardContent>
            <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center' }}>
              <Box>
                <Typography variant="h3">Authority to Leave</Typography>
                <Typography variant="body2" color="text.secondary">
                  Leave baggage unattended if not home
                </Typography>
              </Box>
              <Switch
                checked={atl !== 'None'}
                onChange={(_, on) => setAtl(on ? 'FrontDoor' : 'None')}
              />
            </Stack>
            {atl !== 'None' && (
              <>
                <Divider sx={{ my: 1.5 }} />
                <RadioGroup
                  value={atl}
                  onChange={(e) => setAtl(e.target.value as AtlOption)}
                >
                  {(['FrontDoor', 'BackDoor', 'Garage', 'Reception', 'Neighbour', 'Other'] as const).map((opt) => (
                    <FormControlLabel
                      key={opt}
                      value={opt}
                      control={<Radio />}
                      label={ATL_LABELS[opt]}
                    />
                  ))}
                </RadioGroup>
                {(atl === 'Neighbour' || atl === 'Other') && (
                  <TextField
                    label={atl === 'Other' ? 'Specify location (required)' : 'Neighbour details (optional)'}
                    value={accessNotes}
                    onChange={(e) => setAccessNotes(e.target.value)}
                    fullWidth
                    multiline
                    minRows={2}
                    sx={{ mt: 1.5 }}
                    required={atl === 'Other'}
                  />
                )}
              </>
            )}
          </CardContent>
        </Card>

        <TextField
          label="Override contact phone (optional)"
          value={phoneOverride}
          onChange={(e) => setPhoneOverride(e.target.value)}
          fullWidth
        />

        {error && <Alert severity="error">{error}</Alert>}
        {!online && (
          <Alert severity="warning">
            You appear to be offline. Connect to the internet to submit your confirmation.
          </Alert>
        )}

        <Button
          variant="contained"
          color="secondary"
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
            Ref: {summary.reference}
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
