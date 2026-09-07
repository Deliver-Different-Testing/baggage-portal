import { useCallback, useMemo, useState } from 'react'
import { useMutation, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import { useDisclosure } from '@mantine/hooks'
import { notifications } from '@mantine/notifications'
import { addressLabels, isUnitedStates } from '../../utils/address'
import { confirmBooking } from '../../api/pax'
import { airlineAccent } from '../../styles/airlineAccent'
import { getAirlineBrand } from '../../styles/airlineBranding'
import { ADDRESS_FIELD_KEYS, atlNotesAreRequired, computeFieldErrors } from './confirmValidation'
import type { AddressDto, BookingSummary, TimeSlot } from '../../api/client'
import type { AddressDetail } from '../../types/address'

function hasNotStarted(slot: TimeSlot): boolean {
  if (!slot.runUtc) return true
  const runsAt = Date.parse(slot.runUtc)
  return Number.isNaN(runsAt) || runsAt > Date.now()
}

export function showError(message: string) {
  notifications.show({ color: 'red', message, autoClose: 4000 })
}

export function useConfirmForm(
  bookingId: string,
  summary: BookingSummary,
  slots: UseQueryResult<TimeSlot[]>,
) {
  const [address, setAddress] = useState<AddressDto>(summary.deliveryAddress)
  const [passengerName, setPassengerName] = useState(summary.passengerName ?? '')
  const [passengerPhone, setPassengerPhone] = useState(summary.passengerPhone ?? '')
  const [passengerEmail, setPassengerEmail] = useState(summary.passengerEmail ?? '')
  const [addressConfirmed, setAddressConfirmed] = useState(false)
  const [editingAddress, setEditingAddress] = useState(false)
  const [selectedSlotId, setSelectedSlotId] = useState<string | null>(null)
  const [atlOptionId, setAtlOptionId] = useState<number | null>(null)
  const [accessNotes, setAccessNotes] = useState('')
  const [confirmed, setConfirmed] = useState(false)
  const [submitCount, setSubmitCount] = useState(0)
  const [serverCountryError, setServerCountryError] = useState<string | null>(null)
  const [reviewOpen, { open: openReview, close: closeReview }] = useDisclosure(false)
  const queryClient = useQueryClient()

  const defaultAtlOptionId = summary.defaultAtlOptionId ?? null

  const accent = useMemo(
    () => airlineAccent(getAirlineBrand(summary.airlineCode)),
    [summary.airlineCode],
  )

  const availableSlots = useMemo(
    () => (slots.data ?? []).filter(hasNotStarted),
    [slots.data],
  )

  const defaultSlotId = useMemo(
    () =>
      availableSlots.length
        ? (availableSlots.find((s) => s.firstAvailable) ?? availableSlots[0]).id
        : null,
    [availableSlots],
  )
  const effectiveSlotId = selectedSlotId ?? defaultSlotId

  const selectedSlot: TimeSlot | undefined = useMemo(
    () => availableSlots.find((s) => s.id === effectiveSlotId),
    [availableSlots, effectiveSlotId],
  )

  const handleSelectSlot = useCallback((id: string) => setSelectedSlotId(id), [])

  const { refetch: refetchSlots } = slots
  const handleRunStartPassed = useCallback(() => {
    setSelectedSlotId(null)
    closeReview()
    void refetchSlots()
  }, [closeReview, refetchSlots])

  const setLocation = useCallback((patch: Partial<AddressDto>) => {
    setAddress((a) => ({ ...a, ...patch, latitude: null, longitude: null }))
  }, [])

  const patchAddress = useCallback((patch: Partial<AddressDto>) => {
    setAddress((a) => ({ ...a, ...patch }))
  }, [])

  const handleAddressConfirmedChange = useCallback((value: boolean) => {
    setAddressConfirmed(value)
    if (value) setEditingAddress(false)
  }, [])

  const handleToggleAddressEdit = useCallback(() => {
    setEditingAddress((editing) => {
      if (!editing) setAddressConfirmed(false)
      return !editing
    })
  }, [])

  const handleCountryChange = useCallback(
    (country: string) => {
      setServerCountryError(null)
      setLocation({ country })
    },
    [setLocation],
  )

  const handleAddressSelect = useCallback((detail: AddressDetail) => {
    setServerCountryError(null)
    setAddressConfirmed(false)
    const us = isUnitedStates(detail.countryCode)
    setAddress((a) => ({
      ...a,
      line3: detail.streetNumber || null,
      line4: detail.street,
      line5: us ? detail.city : detail.suburb,
      line6: us ? detail.stateCode || detail.state : detail.city,
      line7: detail.postalCode || null,
      country: detail.countryCode || a.country,
      latitude: detail.latitude ?? null,
      longitude: detail.longitude ?? null,
    }))
  }, [])

  const handleAtlToggle = useCallback(
    (enabled: boolean) => setAtlOptionId(enabled ? defaultAtlOptionId : null),
    [defaultAtlOptionId],
  )

  const atlOptions = summary.atlOptions
  const labels = addressLabels(address.country)
  const selectedAtlOption = atlOptions.find((o) => o.id === atlOptionId)
  const atlNotesRequired = atlNotesAreRequired(selectedAtlOption?.name)

  const fieldErrors = computeFieldErrors(
    {
      passengerName,
      passengerPhone,
      passengerEmail,
      address,
      addressConfirmed,
      hasDeliveryTime: !!selectedSlot?.runUtc,
      atlOptionName: selectedAtlOption?.name,
      accessNotes,
    },
    labels,
  )

  const showFieldError = (key: string) => (submitCount > 0 ? fieldErrors[key] : undefined)

  const confirm = useMutation({
    mutationFn: (body: Parameters<typeof confirmBooking>[1]) => confirmBooking(bookingId, body),
    onSuccess: () => {
      setConfirmed(true)
    },
    onError: (err) => {
      closeReview()

      const response = (
        err as {
          response?: { status?: number; data?: { errors?: Record<string, string[]> } }
        }
      )?.response

      if (response?.status === 409) {
        showError('This booking has already been confirmed.')
        void queryClient.invalidateQueries({ queryKey: ['pax', 'booking', bookingId] })
        return
      }

      const errors = response?.data?.errors
      const countryError = errors?.['Address.Country']?.[0]
      if (countryError) {
        setServerCountryError(countryError)
        setEditingAddress(true)
        showError(countryError)
        return
      }

      const firstError = errors && Object.values(errors).flat().find(Boolean)
      showError(firstError ?? 'Could not submit your confirmation. Please try again.')
    },
  })

  function review() {
    setSubmitCount((count) => count + 1)
    if (Object.keys(fieldErrors).length > 0) {
      if (ADDRESS_FIELD_KEYS.some((key) => fieldErrors[key])) setEditingAddress(true)
      return
    }
    openReview()
  }

  function submit() {
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

  return {
    accent,
    labels,
    address,
    addressConfirmed,
    editingAddress,
    fieldErrors,
    submitCount,
    passengerName,
    passengerPhone,
    passengerEmail,
    accessNotes,
    atlOptions,
    atlOptionId,
    atlNotesRequired,
    selectedAtlOption,
    availableSlots,
    effectiveSlotId,
    selectedSlot,
    serverCountryError,
    confirmed,
    reviewOpen,
    submitting: confirm.isPending,
    refetchSlots,
    setPassengerName,
    setPassengerPhone,
    setPassengerEmail,
    setAccessNotes,
    setAtlOptionId,
    setLocation,
    patchAddress,
    handleCountryChange,
    handleAddressConfirmedChange,
    handleToggleAddressEdit,
    handleAddressSelect,
    handleSelectSlot,
    handleRunStartPassed,
    handleAtlToggle,
    showFieldError,
    closeReview,
    review,
    submit,
  }
}
