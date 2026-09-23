import { useCallback, useState } from 'react'
import { useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { AlertIcon } from '../components/Icon'
import { FullPageMessage } from '../components/FullPageMessage'
import { ConfirmForm } from '../components/confirm/ConfirmForm'
import { ConfirmSkeleton } from '../components/confirm/ConfirmSkeleton'
import { ConfirmedScreen } from '../components/confirm/ConfirmedScreen'
import { getBooking, getTimeslots } from '../api/pax'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import { useRedirectOnNotFound } from '../hooks/useRedirectOnNotFound'

export { ConfirmedScreen } from '../components/confirm/ConfirmedScreen'

const ERROR_COLOR = 'var(--mantine-color-red-6)'

export function PaxMobile() {
  const { id } = useParams<{ id: string }>()
  const online = useOnlineStatus()
  const [editing, setEditing] = useState(false)

  const booking = useQuery({
    queryKey: ['pax', 'booking', id],
    queryFn: () => getBooking(id ?? ''),
    enabled: !!id,
    retry: false,
  })

  const confirmation = booking.data?.confirmation ?? null

  const slots = useQuery({
    queryKey: ['pax', 'timeslots', id],
    queryFn: () => getTimeslots(id ?? ''),
    enabled: !!id && (!confirmation || editing),
  })

  const startEditing = useCallback(() => setEditing(true), [])
  const stopEditing = useCallback(() => setEditing(false), [])

  useRedirectOnNotFound(booking.error)

  if (!id) return null

  if (booking.isLoading) {
    return <ConfirmSkeleton />
  }

  if (!booking.data) {
    return (
      <FullPageMessage
        icon={<AlertIcon size={36} color={ERROR_COLOR} />}
        iconColor={ERROR_COLOR}
        title="We couldn't load your booking"
        description="Something went wrong on our side. Check your connection and try again — your booking has not been changed."
        actionLabel="Try again"
        onAction={() => void booking.refetch()}
      />
    )
  }

  if (confirmation && !editing) {
    return (
      <ConfirmedScreen
        summary={booking.data}
        onEdit={confirmation.canEdit ? startEditing : undefined}
        editableUntilUtc={confirmation.editableUntilUtc}
        slot={{ dayLabel: confirmation.dayLabel, label: confirmation.windowLabel }}
        address={booking.data.deliveryAddress}
        passengerName={booking.data.passengerName ?? ''}
        passengerPhone={booking.data.passengerPhone ?? ''}
        passengerEmail={booking.data.passengerEmail ?? ''}
        atlOption={booking.data.atlOptions.find((o) => o.id === confirmation.atlOptionId)}
        accessNotes={confirmation.accessNotes ?? ''}
      />
    )
  }

  return (
    <ConfirmForm
      bookingId={id}
      summary={booking.data}
      online={online}
      slots={slots}
      editing={editing ? confirmation : null}
      onCancelEdit={stopEditing}
    />
  )
}
