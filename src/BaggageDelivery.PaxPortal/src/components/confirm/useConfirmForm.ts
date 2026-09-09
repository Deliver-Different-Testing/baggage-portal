import { useCallback, useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import { useDisclosure } from '@mantine/hooks'
import { notifications } from '@mantine/notifications'
import { addressLabels, isUnitedStates, sameDeliveryAddress } from '../../utils/address'
import {
  amendBooking,
  confirmBooking,
  getServices,
  getTimeslots,
  requestAddressHelp,
} from '../../api/pax'
import { airlineAccent } from '../../styles/airlineAccent'
import { getAirlineBrand } from '../../styles/airlineBranding'
import {
  ADDRESS_FIELD_KEYS,
  atlNotesAreRequired,
  computeFieldErrors,
  type AddressErrors,
  type AtlErrors,
  type ContactErrors,
} from './confirmValidation'
import type {
  AddressDto,
  AvailableService,
  BookingConfirmation,
  BookingSummary,
  TimeSlot,
} from '../../api/client'
import type { AddressDetail } from '../../types/address'

const DEFAULT_LEAD_TIME_MINUTES = 30

export const CHANGE_WINDOW_CLOSED_TYPE = 'urn:baggage:change-window-closed'
export const CHANGE_WINDOW_CLOSED_MESSAGE =
  'This booking can no longer be changed online. Please call us.'

function bookable(slot: TimeSlot, leadTimeMs: number): boolean {
  if (!slot.runUtc) return true
  const runsAt = Date.parse(slot.runUtc)
  return Number.isNaN(runsAt) || runsAt > Date.now() + leadTimeMs
}

const SERVICES_DEBOUNCE_MS = 400
const NO_SERVICES: AvailableService[] = []
const NO_ERRORS: Record<string, string> = {}

export function showError(message: string) {
  notifications.show({ color: 'red', message, autoClose: 4000 })
}

export function useConfirmForm(
  bookingId: string,
  summary: BookingSummary,
  slots: UseQueryResult<TimeSlot[]>,
  editing?: BookingConfirmation | null,
  onAmended?: () => void,
) {
  const isEditing = !!editing
  const [address, setAddress] = useState<AddressDto>(summary.deliveryAddress)
  const [passengerName, setPassengerName] = useState(summary.passengerName ?? '')
  const [passengerPhone, setPassengerPhone] = useState(summary.passengerPhone ?? '')
  const [passengerEmail, setPassengerEmail] = useState(summary.passengerEmail ?? '')
  const [addressConfirmed, setAddressConfirmed] = useState(isEditing)
  const [editingAddress, setEditingAddress] = useState(false)
  const [selectedSlotId, setSelectedSlotId] = useState<string | null>(null)
  const [selectedServiceId, setSelectedServiceId] = useState<number | null>(null)
  const [helpRequested, setHelpRequested] = useState(false)
  const [atlOptionId, setAtlOptionId] = useState<number | null>(editing?.atlOptionId ?? null)
  const [accessNotes, setAccessNotes] = useState(editing?.accessNotes ?? '')
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

  const originalAddress = summary.deliveryAddress
  const addressChanged = !sameDeliveryAddress(originalAddress, address)

  const [debouncedAddress, setDebouncedAddress] = useState(address)
  useEffect(() => {
    const handle = setTimeout(() => setDebouncedAddress(address), SERVICES_DEBOUNCE_MS)
    return () => clearTimeout(handle)
  }, [address])

  const debouncedAddressChanged = !sameDeliveryAddress(originalAddress, debouncedAddress)
  const addressSettled = sameDeliveryAddress(debouncedAddress, address)

  const services = useQuery({
    queryKey: ['pax', 'services', bookingId, debouncedAddress],
    queryFn: () => getServices(bookingId, debouncedAddress),
    enabled: debouncedAddressChanged,
    staleTime: 30_000,
  })

  const availableServices = addressChanged ? (services.data?.services ?? NO_SERVICES) : NO_SERVICES
  const noServiceAvailable =
    addressChanged && addressSettled && services.isSuccess && availableServices.length === 0
  const servicesLoading = addressChanged && (!addressSettled || services.isLoading)

  const selectedService: AvailableService | undefined = useMemo(
    () => availableServices.find((s) => s.jobTypeId === selectedServiceId),
    [availableServices, selectedServiceId],
  )

  const handleSelectService = useCallback((jobTypeId: number) => {
    setSelectedSlotId(null)
    setSelectedServiceId(jobTypeId)
  }, [])

  const serviceSlots = useQuery({
    queryKey: [
      'pax',
      'timeslots',
      bookingId,
      selectedService?.jobTypeId ?? null,
      selectedService?.scheduleId ?? null,
    ],
    queryFn: () =>
      getTimeslots(bookingId, undefined, {
        jobTypeId: selectedService!.jobTypeId,
        scheduleId: selectedService!.scheduleId,
      }),
    enabled: !!selectedService,
  })

  const effectiveSlots = selectedService ? serviceSlots : slots

  const leadTimeMs =
    (summary.bookingLeadTimeMinutes ?? DEFAULT_LEAD_TIME_MINUTES) * 60_000

  const availableSlots = useMemo(
    () => (effectiveSlots.data ?? []).filter((s) => bookable(s, leadTimeMs)),
    [effectiveSlots.data, leadTimeMs],
  )

  const bookedRunUtc = editing?.deliveryTimeUtc ?? null
  const bookedSlotId = useMemo(
    () => (bookedRunUtc ? (availableSlots.find((s) => s.runUtc === bookedRunUtc)?.id ?? null) : null),
    [availableSlots, bookedRunUtc],
  )

  const defaultSlotId = useMemo(
    () =>
      bookedSlotId ??
      (availableSlots.length
        ? (availableSlots.find((s) => s.firstAvailable) ?? availableSlots[0]).id
        : null),
    [availableSlots, bookedSlotId],
  )
  const effectiveSlotId = selectedSlotId ?? defaultSlotId

  const selectedSlot: TimeSlot | undefined = useMemo(
    () => availableSlots.find((s) => s.id === effectiveSlotId),
    [availableSlots, effectiveSlotId],
  )

  const handleSelectSlot = useCallback((id: string) => setSelectedSlotId(id), [])

  const { refetch: refetchSlots } = effectiveSlots
  const handleRunStartPassed = useCallback(() => {
    setSelectedSlotId(null)
    closeReview()
    void refetchSlots()
  }, [closeReview, refetchSlots])

  const clearServiceChoice = useCallback(() => {
    setSelectedServiceId(null)
    setSelectedSlotId(null)
    setHelpRequested(false)
  }, [])

  const setLocation = useCallback(
    (patch: Partial<AddressDto>) => {
      clearServiceChoice()
      setAddress((a) => ({ ...a, ...patch, latitude: null, longitude: null }))
    },
    [clearServiceChoice],
  )

  const patchAddress = useCallback(
    (patch: Partial<AddressDto>) => {
      clearServiceChoice()
      setAddress((a) => ({ ...a, ...patch }))
    },
    [clearServiceChoice],
  )

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

  const handleAddressSelect = useCallback(
    (detail: AddressDetail) => {
      setServerCountryError(null)
      setAddressConfirmed(false)
      clearServiceChoice()
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
    },
    [clearServiceChoice],
  )

  const handleAtlToggle = useCallback(
    (enabled: boolean) => setAtlOptionId(enabled ? defaultAtlOptionId : null),
    [defaultAtlOptionId],
  )

  const atlOptions = summary.atlOptions
  const labels = useMemo(() => addressLabels(address.country), [address.country])
  const selectedAtlOption = atlOptions.find((o) => o.id === atlOptionId)
  const atlOptionName = selectedAtlOption?.name
  const atlNotesRequired = atlNotesAreRequired(atlOptionName)
  const hasDeliveryTime = !!selectedSlot?.runUtc
  const hasService = !!selectedService

  const fieldErrors = useMemo(
    () =>
      computeFieldErrors(
        {
          passengerName,
          passengerPhone,
          passengerEmail,
          address,
          addressConfirmed,
          hasDeliveryTime,
          atlOptionName,
          accessNotes,
          addressChanged,
          hasService,
          noServiceAvailable,
        },
        labels,
      ),
    [
      passengerName,
      passengerPhone,
      passengerEmail,
      address,
      addressConfirmed,
      hasDeliveryTime,
      atlOptionName,
      accessNotes,
      addressChanged,
      hasService,
      noServiceAvailable,
      labels,
    ],
  )

  const shown = submitCount > 0 ? fieldErrors : NO_ERRORS

  const contactErrors = useMemo<ContactErrors>(
    () => ({
      passengerName: shown.passengerName,
      passengerPhone: shown.passengerPhone,
      passengerEmail: shown.passengerEmail,
    }),
    [shown.passengerName, shown.passengerPhone, shown.passengerEmail],
  )

  const addressErrors = useMemo<AddressErrors>(
    () => ({
      addressConfirmed: shown.addressConfirmed,
      line3: shown.line3,
      line4: shown.line4,
      line5: shown.line5,
      line6: shown.line6,
      line7: shown.line7,
      country: shown.country,
    }),
    [
      shown.addressConfirmed,
      shown.line3,
      shown.line4,
      shown.line5,
      shown.line6,
      shown.line7,
      shown.country,
    ],
  )

  const atlErrors = useMemo<AtlErrors>(
    () => ({ accessNotes: shown.accessNotes }),
    [shown.accessNotes],
  )

  const { refetch: refetchServices } = services
  const retryServices = useCallback(() => void refetchServices(), [refetchServices])

  const confirm = useMutation({
    mutationFn: (body: Parameters<typeof confirmBooking>[1]) => confirmBooking(bookingId, body),
    onSuccess: () => {
      setConfirmed(true)
    },
    onError: (err) => {
      closeReview()

      const response = (
        err as {
          response?: {
            status?: number
            data?: { errors?: Record<string, string[]> }
          }
        }
      )?.response

      if (response?.status === 409) {
        showError('This booking has already been confirmed.')
        void queryClient.invalidateQueries({
          queryKey: ['pax', 'booking', bookingId],
        })
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

  const amend = useMutation({
    mutationFn: (body: Parameters<typeof amendBooking>[1]) => amendBooking(bookingId, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['pax', 'booking', bookingId] })
      if (onAmended) onAmended()
      else setConfirmed(true)
    },
    onError: (err) => {
      closeReview()

      const response = (
        err as {
          response?: {
            status?: number
            data?: { type?: string; errors?: Record<string, string[]> }
          }
        }
      )?.response

      if (response?.status === 409) {
        showError(
          response.data?.type === CHANGE_WINDOW_CLOSED_TYPE
            ? CHANGE_WINDOW_CLOSED_MESSAGE
            : 'This booking could not be changed. Please reload and try again.',
        )
        void queryClient.invalidateQueries({ queryKey: ['pax', 'booking', bookingId] })
        return
      }

      const errors = response?.data?.errors
      const firstError = errors && Object.values(errors).flat().find(Boolean)
      showError(firstError ?? 'Could not save your changes. Please try again.')
    },
  })

  const addressHelp = useMutation({
    mutationFn: () =>
      requestAddressHelp(bookingId, {
        address,
        passengerName: passengerName.trim(),
        passengerPhone: passengerPhone.trim() || null,
        passengerEmail: passengerEmail.trim() || null,
      }),
    onSuccess: () => setHelpRequested(true),
    onError: () =>
      showError('We could not send that request just now. Please try again, or call us.'),
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

    if (isEditing) {
      amend.mutate({
        deliveryTimeUtc: selectedSlot.runUtc,
        atlOptionId,
        accessNotes: accessNotes || null,
        passengerName: passengerName.trim(),
        passengerPhone: passengerPhone.trim(),
        passengerEmail: passengerEmail.trim(),
      })
      return
    }

    confirm.mutate({
      address,
      deliveryTimeUtc: selectedSlot.runUtc,
      serviceJobTypeId: selectedService?.jobTypeId ?? null,
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
    contactErrors,
    addressErrors,
    atlErrors,
    serviceError: shown.service,
    slotError: shown.slot,
    submitCount,
    passengerName,
    passengerPhone,
    passengerEmail,
    accessNotes,
    atlOptions,
    atlOptionId,
    atlNotesRequired,
    selectedAtlOption,
    slots: effectiveSlots,
    availableSlots,
    effectiveSlotId,
    selectedSlot,
    addressChanged,
    availableServices,
    selectedService,
    servicesLoading,
    servicesError: services.isError,
    noServiceAvailable,
    helpRequested,
    requestingHelp: addressHelp.isPending,
    serverCountryError,
    confirmed,
    isEditing,
    reviewOpen,
    submitting: confirm.isPending || amend.isPending,
    refetchSlots,
    retryServices,
    handleSelectService,
    requestAddressHelp: () => addressHelp.mutate(),
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
    closeReview,
    review,
    submit,
  }
}
