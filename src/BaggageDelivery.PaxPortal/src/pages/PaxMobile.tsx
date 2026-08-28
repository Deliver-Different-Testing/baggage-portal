import {
    memo,
    useCallback,
    useMemo,
    useRef,
    useState,
    type KeyboardEvent,
    type ReactNode,
} from 'react'
import {Link as RouterLink, useParams} from 'react-router-dom'
import {useMutation, useQuery, type UseQueryResult} from '@tanstack/react-query'
import {
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
    Paper,
    Radio,
    Skeleton,
    Stack,
    Switch,
    Text,
    Textarea,
    TextInput,
    Title,
} from '@mantine/core'
import {useDisclosure} from '@mantine/hooks'
import {notifications} from '@mantine/notifications'
import {
    ArrowRightIcon,
    CheckCircleIcon,
    CheckIcon,
    ClockIcon,
    DocketIcon,
    AlertIcon,
} from '../components/Icon'
import {ConfirmHero, ConfirmHeroSkeleton} from '../components/ConfirmHero'
import {FullPageMessage} from '../components/FullPageMessage'
import {DocketTile, PunchedTag} from '../components/Docket'
import {
    DialogFooter,
    DialogHeader,
    DialogShell,
    dialogContentBg,
    sectionPaperProps,
} from '../components/dialog'
import {DeliveryDocket, ExtraDeliveryInfo} from '../components/DeliveryDocket'
import {addressLines} from '../utils/address'
import {FlightPathBackdrop} from '../components/FlightPathBackdrop'
import {formatCountdown, useRunStartCountdown} from '../hooks/useRunStartCountdown'
import {confirmBooking, getBooking, getTimeslots} from '../api/pax'
import type {AddressDto, AtlOption, BookingSummary, TimeSlot} from '../api/client'
import type {AddressDetail} from '../types/address'
import {useOnlineStatus} from '../hooks/useOnlineStatus'
import {useRedirectOnNotFound} from '../hooks/useRedirectOnNotFound'
import {AddressAutocomplete} from '../components/AddressAutocomplete'
import {PoweredByFooter} from '../components/PoweredByFooter'
import {onBrandScrim, tokens} from '../styles/mantineTheme'
import {airlineAccent} from '../styles/airlineAccent'
import {getAirlineBrand} from '../styles/airlineBranding'

const ACCESS_NOTES_MAX = 120
const PASSENGER_EMAIL_MAX = 100
const ADDRESS_LINE2_MAX = 200
const ADDRESS_FIELD_KEYS = ['line1', 'suburb', 'city', 'postCode', 'country']

const SECTION_FIELDS = {
    details: ['passengerName', 'passengerPhone', 'passengerEmail'],
    address: [...ADDRESS_FIELD_KEYS, 'addressConfirmed'],
    window: ['slot'],
    atl: ['accessNotes'],
} as const

const ERROR_COLOR = 'var(--mantine-color-red-6)'

function ConfirmSkeleton() {
    return (
        <Box mih="100vh" pb={{base: 112, sm: 128}}>
            <ConfirmHeroSkeleton/>
            <Container
                size={tokens.hero.measure}
                px={0}
                mt={tokens.hero.overlap}
                style={{position: 'relative', zIndex: 1}}
            >
                <Box px={{base: 12, sm: 0}}>
                    <Card p="lg" aria-busy="true" aria-label="Loading your booking">
                        <Stack gap="lg">
                            {[0, 1, 2, 3].map((row) => (
                                <Group key={row} gap="sm" align="center" wrap="nowrap">
                                    <Skeleton height={32} width={32} radius="sm"/>
                                    <Skeleton height={14} width={row % 2 ? 190 : 140} radius="sm"/>
                                </Group>
                            ))}
                        </Stack>
                    </Card>
                </Box>
            </Container>
        </Box>
    )
}

function showError(message: string) {
    notifications.show({color: 'red', message, autoClose: 4000})
}

const RUN_URGENT_MS = 10 * 60_000

const RunStartNotice = memo(({
                                 targetUtc,
                                 onExpire,
                             }: {
    targetUtc: string | undefined
    onExpire: () => void
}) => {
    const remainingMs = useRunStartCountdown(targetUtc, onExpire)
    const urgent = remainingMs <= RUN_URGENT_MS

    return (
        <Group
            gap={8}
            align="center"
            wrap="nowrap"
            mt="sm"
            px="sm"
            py={8}
            style={{
                borderRadius: 'var(--mantine-radius-sm)',
                backgroundColor: urgent
                    ? 'var(--mantine-color-orange-light)'
                    : 'var(--dd-surface-container-high)',
                border: `1px solid ${
                    urgent ? 'var(--mantine-color-orange-filled)' : 'var(--mantine-color-default-border)'
                }`,
            }}
        >
            <ClockIcon size={14}/>
            <Text
                size="xs"
                c={urgent ? undefined : 'dimmed'}
                fw={urgent ? 600 : undefined}
                style={{flex: 1, minWidth: 0}}
            >
                Time left to keep this window
            </Text>
            <Text
                size="xs"
                fw={700}
                c={urgent ? 'orange' : 'brand'}
                style={{fontVariantNumeric: 'tabular-nums'}}
            >
                {formatCountdown(remainingMs)}
            </Text>
        </Group>
    )
})

export function PaxMobile() {
    const {id} = useParams<{ id: string }>()
    const online = useOnlineStatus()

    const booking = useQuery({
        queryKey: ['pax', 'booking', id],
        queryFn: () => getBooking(id ?? ''),
        enabled: !!id,
        retry: false,
    })

    const slots = useQuery({
        queryKey: ['pax', 'timeslots', id],
        queryFn: () => getTimeslots(id ?? ''),
        enabled: !!id,
    })

    useRedirectOnNotFound(booking.error)

    if (!id) return null

    if (booking.isLoading) {
        return <ConfirmSkeleton/>
    }

    if (!booking.data) {
        return (
            <FullPageMessage
                icon={<AlertIcon size={36} color={ERROR_COLOR}/>}
                iconColor={ERROR_COLOR}
                title="We couldn't load your booking"
                description="Something went wrong on our side. Check your connection and try again — your booking has not been changed."
                actionLabel="Try again"
                onAction={() => void booking.refetch()}
            />
        )
    }

    return <ConfirmForm bookingId={id} summary={booking.data} online={online} slots={slots}/>
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
    const [addressConfirmed, setAddressConfirmed] = useState(false)
    const [editingAddress, setEditingAddress] = useState(false)
    const [passengerName, setPassengerName] = useState(summary.passengerName ?? '')
    const [passengerPhone, setPassengerPhone] = useState(summary.passengerPhone ?? '')
    const [passengerEmail, setPassengerEmail] = useState(summary.passengerEmail ?? '')
    const [selectedSlotId, setSelectedSlotId] = useState<string | null>(null)
    const defaultAtlOptionId = summary.defaultAtlOptionId ?? null
    const [atlOptionId, setAtlOptionId] = useState<number | null>(null)
    const [accessNotes, setAccessNotes] = useState('')
    const [confirmed, setConfirmed] = useState(false)
    const [submitAttempted, setSubmitAttempted] = useState(false)
    const [serverCountryError, setServerCountryError] = useState<string | null>(null)
    const [reviewOpen, {open: openReview, close: closeReview}] = useDisclosure(false)

    const accent = useMemo(
        () => airlineAccent(getAirlineBrand(summary.airlineCode)),
        [summary.airlineCode],
    )

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

    const handleSelectSlot = useCallback((id: string) => setSelectedSlotId(id), [])

    const {refetch: refetchSlots} = slots
    const handleRunStartPassed = useCallback(() => {
        setSelectedSlotId(null)
        closeReview()
        void refetchSlots()
    }, [closeReview, refetchSlots])

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
    if (!selectedSlot?.runUtc) fieldErrors.slot = 'Please pick a delivery window.'

    const selectedAtlOption = atlOptions.find((o) => o.id === atlOptionId)
    const atlNotesRequired = selectedAtlOption?.name.trim().toLowerCase() === 'safe place'
    if (atlNotesRequired && !accessNotes.trim()) {
        fieldErrors.accessNotes = 'Please describe the safe place to leave your baggage.'
    }

    const showFieldError = (key: string) =>
        submitAttempted ? fieldErrors[key] : undefined

    const addressFieldInvalid = ADDRESS_FIELD_KEYS.some((key) => fieldErrors[key])

    const sectionDone = (section: keyof typeof SECTION_FIELDS): SectionStatus =>
        SECTION_FIELDS[section].every((key) => !fieldErrors[key]) ? 'complete' : 'incomplete'

    const confirm = useMutation({
        mutationFn: (body: Parameters<typeof confirmBooking>[1]) => confirmBooking(bookingId, body),
        onSuccess: () => {
            setConfirmed(true)
            void import('./Tracking')
        },
        onError: (err) => {
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
                setAddressConfirmed(false)
                setEditingAddress(true)
                showError(countryError)
                return
            }

            const firstError = errors && Object.values(errors).flat().find(Boolean)
            showError(firstError ?? 'Could not submit your confirmation. Please try again.')
        },
    })

    function review() {
        setSubmitAttempted(true)
        if (Object.keys(fieldErrors).length > 0) {
            if (addressFieldInvalid) setEditingAddress(true)
            showError('Please complete all required fields before confirming.')
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

    if (confirmed) {
        return (
            <ConfirmedScreen
                summary={summary}
                slot={selectedSlot}
                bookingId={bookingId}
                address={address}
                passengerName={passengerName}
                passengerPhone={passengerPhone}
                passengerEmail={passengerEmail}
                atlOption={selectedAtlOption}
                accessNotes={accessNotes}
            />
        )
    }

    return (
        <Box mih="100vh" pb={{base: 112, sm: 128}}>
            <ConfirmHero summary={summary} accent={accent}/>

            <Container
                size={tokens.hero.measure}
                px={0}
                mt={tokens.hero.overlap}
                style={{position: 'relative', zIndex: 1}}
            >
                <Stack gap="md" px={{base: 12, sm: 0}}>
                    <Card p={0} style={{overflow: 'hidden'}}>
                        <Box className="pax-stagger">
                            <FormSection title="Your details" status={sectionDone('details')}>
                                <Stack gap="xs">
                                    <TextInput
                                        label="Full name"
                                        value={passengerName}
                                        onChange={(e) => setPassengerName(e.currentTarget.value)}
                                        autoComplete="name"
                                        required
                                        error={showFieldError('passengerName')}
                                    />
                                    <TextInput
                                        label="Phone number"
                                        value={passengerPhone}
                                        onChange={(e) => setPassengerPhone(e.currentTarget.value)}
                                        autoComplete="tel"
                                        inputMode="tel"
                                        required
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
                                        error={showFieldError('passengerEmail')}
                                    />
                                </Stack>
                            </FormSection>

                            <Divider/>

                            <FormSection title="Delivery address" status={sectionDone('address')}>
                                <Stack gap="sm">
                                    <AddressGate
                                        address={address}
                                        confirmed={addressConfirmed}
                                        editing={editingAddress}
                                        onChange={handleAddressConfirmedChange}
                                        onToggleEdit={handleToggleAddressEdit}
                                        error={showFieldError('addressConfirmed')}
                                    />
                                    <Collapse expanded={editingAddress}>
                                        <Stack gap="sm" pt="xs">
                                            <AddressAutocomplete bookingId={bookingId}
                                                                 onAddressSelect={handleAddressSelect}/>
                                            <TextInput
                                                label="Street address"
                                                value={address.line1}
                                                onChange={(e) => {
                                                    const value = e.currentTarget.value
                                                    setAddress((a) => ({...a, line1: value}))
                                                }}
                                                required
                                                error={showFieldError('line1')}
                                            />
                                            <TextInput
                                                label="Extra delivery information"
                                                placeholder="Apartment number, gate code, where to find the door"
                                                value={address.line2 ?? ''}
                                                onChange={(e) => {
                                                    const value = e.currentTarget.value
                                                    setAddress((a) => ({...a, line2: value}))
                                                }}
                                                maxLength={ADDRESS_LINE2_MAX}
                                            />
                                            <TextInput
                                                label="Suburb"
                                                value={address.suburb ?? ''}
                                                onChange={(e) => {
                                                    const value = e.currentTarget.value
                                                    setAddress((a) => ({...a, suburb: value}))
                                                }}
                                                required
                                                error={showFieldError('suburb')}
                                            />
                                            <Group gap="sm" align="flex-start" grow wrap="nowrap">
                                                <TextInput
                                                    label="City"
                                                    value={address.city}
                                                    onChange={(e) => {
                                                        const value = e.currentTarget.value
                                                        setAddress((a) => ({...a, city: value}))
                                                    }}
                                                    required
                                                    error={showFieldError('city')}
                                                />
                                                <TextInput
                                                    label="Postcode"
                                                    value={address.postCode ?? ''}
                                                    onChange={(e) => {
                                                        const value = e.currentTarget.value
                                                        setAddress((a) => ({...a, postCode: value}))
                                                    }}
                                                    required
                                                    maw={132}
                                                    error={showFieldError('postCode')}
                                                />
                                            </Group>
                                            <TextInput
                                                label="Country"
                                                value={address.country}
                                                onChange={(e) => {
                                                    const country = e.currentTarget.value
                                                    setServerCountryError(null)
                                                    setAddress((a) => ({...a, country}))
                                                }}
                                                required
                                                error={serverCountryError ?? showFieldError('country')}
                                            />
                                        </Stack>
                                    </Collapse>
                                </Stack>
                            </FormSection>

                            <Divider/>

                            <FormSection title="Delivery window" status={sectionDone('window')} emphasis="primary">
                                {slots.isLoading && (
                                    <Group gap="sm" align="center" py="xs">
                                        <Loader size={18}/>
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
                                    <Stack gap={4}>
                                        <Text size="sm">
                                            There are no delivery windows available for this booking yet.
                                        </Text>
                                        {summary.supportPhone && (
                                            <Text size="sm">
                                                Call {summary.supportPhone} and we&apos;ll arrange one with you.
                                            </Text>
                                        )}
                                    </Stack>
                                )}
                                {slots.data && (
                                    <SlotList
                                        slots={slots.data}
                                        selectedId={effectiveSlotId}
                                        onSelect={handleSelectSlot}
                                    />
                                )}
                                {selectedSlot?.runUtc && (
                                    <RunStartNotice
                                        targetUtc={selectedSlot.runUtc}
                                        onExpire={handleRunStartPassed}
                                    />
                                )}
                            </FormSection>

                            <Divider/>

                            <FormSection
                                title="Authority to leave"
                                subtitle="Leave baggage unattended if you're not home"
                                status={atlOptionId === null ? 'optional' : sectionDone('atl')}
                                action={
                                    <Switch
                                        checked={atlOptionId !== null}
                                        disabled={atlOptions.length === 0}
                                        onChange={(e) =>
                                            setAtlOptionId(e.currentTarget.checked ? defaultAtlOptionId : null)
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
                                            maxLength={ACCESS_NOTES_MAX}
                                            description={`${accessNotes.length}/${ACCESS_NOTES_MAX}`}
                                            autosize
                                            minRows={2}
                                            mt="sm"
                                        />
                                    </Box>
                                </Collapse>
                            </FormSection>
                        </Box>
                    </Card>

                    {!online && (
                        <Alert color="orange" variant="light">
                            You appear to be offline. Connect to the internet to submit your confirmation.
                        </Alert>
                    )}
                </Stack>

                <PoweredByFooter/>
            </Container>

            <Box px="md" className="pax-action-bar">
                <Container size={tokens.hero.measure} px={0}>
                    <Button
                        size="lg"
                        fullWidth
                        disabled={!online}
                        onClick={review}
                        leftSection={<DocketIcon size={18}/>}
                        rightSection={<ArrowRightIcon size={18}/>}
                        style={tokens.button.primary}
                    >
                        Review delivery
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
                fileReference={summary.fileReference}
            />
        </Box>
    )
}

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
                                       fileReference,
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
    fileReference: string
}) {
    return (
        <DialogShell
            opened={opened}
            onClose={onClose}
            label="Check your delivery details"
            closeOnClickOutside={!submitting}
            closeOnEscape={!submitting}
            transitionProps={{transition: 'pop', duration: tokens.duration.fast}}
        >
            <DialogHeader
                icon={<DocketIcon size={22}/>}
                title="Check your delivery details"
                subtitle="We'll book this as soon as you confirm."
                onClose={onClose}
                closeDisabled={submitting}
            />

            <Box p="lg" bg={dialogContentBg}>
                <Stack gap="md">
                    {slot && (
                        <DocketTile label="Delivery window" variant="tint">
                            <Text
                                fw={700}
                                style={{fontSize: 20, lineHeight: 1.2, fontVariantNumeric: 'tabular-nums'}}
                            >
                                {slot.label}
                            </Text>
                            <Text size="sm">{slot.dayLabel}</Text>
                        </DocketTile>
                    )}

                    <Paper {...sectionPaperProps}>
                        <Stack gap="md">
                            <DeliveryDocket
                                address={address}
                                passengerName={passengerName}
                                passengerPhone={passengerPhone}
                                passengerEmail={passengerEmail}
                                atlOption={atlOption}
                                accessNotes={accessNotes}
                                fileReference={fileReference}
                            />
                        </Stack>
                    </Paper>
                </Stack>
            </Box>

            <DialogFooter
                onCancel={onClose}
                onConfirm={onConfirm}
                confirmLabel="Confirm delivery"
                cancelLabel="Edit details"
                confirmDisabled={!online}
                submitting={submitting}
            />
        </DialogShell>
    )
}

const AtlOptionList = memo(({
                                options,
                                selectedId,
                                onSelect,
                            }: {
    options: AtlOption[]
    selectedId: number | null
    onSelect: (id: number) => void
}) => (
    <Radio.Group
        value={selectedId === null ? '' : String(selectedId)}
        onChange={(v) => onSelect(Number(v))}
    >
        <Stack
            gap={6}
            style={
                options.length > 6 ? {maxHeight: 260, overflowY: 'auto', paddingRight: 8} : undefined
            }
        >
            {options.map((opt) => (
                <Radio key={opt.id} value={String(opt.id)} label={opt.name}/>
            ))}
        </Stack>
    </Radio.Group>
))

function AddressGate({
                         address,
                         confirmed,
                         editing,
                         onChange,
                         onToggleEdit,
                         error,
                     }: {
    address: AddressDto
    confirmed: boolean
    editing: boolean
    onChange: (value: boolean) => void
    onToggleEdit: () => void
    error?: string
}) {
    return (
        <Box
            px="sm"
            py={10}
            style={{
                borderRadius: 'var(--mantine-radius-sm)',
                backgroundColor: 'var(--dd-surface-container-high)',
                border: `1px solid ${
                    error
                        ? 'var(--mantine-color-error)'
                        : confirmed
                            ? 'var(--mantine-color-brand-filled)'
                            : 'var(--mantine-color-default-border)'
                }`,
                borderLeft: `3px solid ${
                    error
                        ? 'var(--mantine-color-error)'
                        : confirmed
                            ? 'var(--mantine-color-brand-filled)'
                            : 'var(--mantine-color-default-border)'
                }`,
            }}
        >
            <Group gap="sm" align="flex-start" wrap="nowrap">
                <Box style={{flex: 1, minWidth: 0}}>
                    {!editing && (
                        <>
                            {addressLines(address).map((line, i) => (
                                <Text key={`${i}-${line}`} size="sm" fw={500} style={{lineHeight: 1.35}}>
                                    {line}
                                </Text>
                            ))}
                            <ExtraDeliveryInfo value={address.line2}/>
                        </>
                    )}
                    <Checkbox
                        mt={editing ? 0 : 8}
                        checked={confirmed}
                        onChange={(e) => onChange(e.currentTarget.checked)}
                        label="This address is correct"
                        description="We'll deliver your bag here."
                        error={error}
                    />
                </Box>
                <Button variant="subtle" size="compact-sm" onClick={onToggleEdit} style={{flexShrink: 0}}>
                    {editing ? 'Done' : 'Edit'}
                </Button>
            </Group>
        </Box>
    )
}

type SectionStatus = 'complete' | 'incomplete' | 'optional'

const STATUS_LABEL: Record<SectionStatus, string> = {
    complete: 'complete',
    incomplete: 'not filled in yet',
    optional: 'off',
}

function SectionStatusChip({title, status}: { title: string; status: SectionStatus }) {
    const complete = status === 'complete'
    return (
        <Center
            w={32}
            h={32}
            aria-label={`${title} — ${STATUS_LABEL[status]}`}
            style={{
                borderRadius: 'var(--mantine-radius-sm)',
                flexShrink: 0,
                backgroundColor: complete ? 'var(--mantine-color-brand-filled)' : 'transparent',
                color: complete ? 'var(--dd-on-brand-fill)' : 'var(--mantine-color-dimmed)',
                border: complete ? undefined : '1px solid var(--dd-outline-variant)',
                transition: 'background-color 150ms, border-color 150ms',
            }}
        >
            {complete ? (
                <CheckIcon size={18}/>
            ) : (
                status === 'optional' && (
                    <Box w={10} h={2} style={{backgroundColor: 'var(--mantine-color-dimmed)'}}/>
                )
            )}
        </Center>
    )
}

function FormSection({
                         title,
                         subtitle,
                         action,
                         status,
                         emphasis = 'default',
                         children,
                     }: {
    title: string
    subtitle?: string
    action?: ReactNode
    status: SectionStatus
    emphasis?: 'default' | 'primary'
    children: ReactNode
}) {
    return (
        <Box
            p="lg"
            style={
                emphasis === 'primary'
                    ?
                    {boxShadow: 'inset 3px 0 0 0 var(--mantine-color-brand-filled)'}
                    : undefined
            }
        >
            <Group gap="sm" align="center" mb="md" wrap="nowrap">
                <SectionStatusChip title={title} status={status}/>
                <Box style={{flex: 1, minWidth: 0}}>
                    <Title order={2} style={tokens.type.sectionTitle}>
                        {title}
                    </Title>
                    {subtitle && (
                        <Text size="sm" c="dimmed" fw={400}>
                            {subtitle}
                        </Text>
                    )}
                </Box>
                {action}
            </Group>
            {children}
        </Box>
    )
}

const SlotList = memo(({
                           slots,
                           selectedId,
                           onSelect,
                       }: {
    slots: TimeSlot[]
    selectedId: string | null
    onSelect: (id: string) => void
}) => {
    const ref = useRef<HTMLDivElement>(null)

    const moveTo = (index: number) => {
        const target = slots[index]
        if (!target) return
        onSelect(target.id)
        ref.current?.querySelectorAll<HTMLElement>('[role="radio"]')[index]?.focus()
    }

    const handleKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
        const current = slots.findIndex((s) => s.id === selectedId)
        if (current === -1) return
        switch (e.key) {
            case 'ArrowDown':
            case 'ArrowRight':
                e.preventDefault()
                moveTo((current + 1) % slots.length)
                break
            case 'ArrowUp':
            case 'ArrowLeft':
                e.preventDefault()
                moveTo((current - 1 + slots.length) % slots.length)
                break
            case 'Home':
                e.preventDefault()
                moveTo(0)
                break
            case 'End':
                e.preventDefault()
                moveTo(slots.length - 1)
                break
        }
    }

    return (
        <Stack
            ref={ref}
            gap="xs"
            role="radiogroup"
            aria-label="Delivery window"
            onKeyDown={handleKeyDown}
        >
            {slots.map((slot) => (
                <SlotOption
                    key={slot.id}
                    slot={slot}
                    selected={slot.id === selectedId}
                    onSelect={onSelect}
                />
            ))}
        </Stack>
    )
})

const SlotOption = memo(({
                             slot,
                             selected,
                             onSelect,
                         }: {
    slot: TimeSlot
    selected: boolean
    onSelect: (id: string) => void
}) => (
    <Box
        role="radio"
        aria-checked={selected}
        tabIndex={selected ? 0 : -1}
        className="pax-slot"
        onClick={() => onSelect(slot.id)}
        onKeyDown={(e) => {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault()
                onSelect(slot.id)
            }
        }}
        style={{
            borderColor: selected
                ? 'var(--mantine-color-brand-filled)'
                : 'var(--mantine-color-default-border)',
            backgroundColor: selected
                ? 'var(--mantine-color-brand-light)'
                : 'var(--dd-surface-container)',
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
                    style={{borderRadius: '50%', backgroundColor: 'var(--mantine-color-brand-filled)'}}
                />
            )}
        </Center>
        <Box style={{flex: 1, minWidth: 0}}>
            <Text size="xs" c="dimmed">
                {slot.dayLabel}
            </Text>
            <Text size="md" fw={700} style={{fontVariantNumeric: 'tabular-nums'}}>
                {slot.label}
            </Text>
            {slot.firstAvailable && (
                <Text style={{...tokens.type.eyebrow, color: 'var(--dd-on-brand-tint)'}}>
                    First available
                </Text>
            )}
        </Box>
    </Box>
))

export function ConfirmedScreen({
                                    summary,
                                    slot,
                                    bookingId,
                                    address,
                                    passengerName,
                                    passengerPhone,
                                    passengerEmail,
                                    atlOption,
                                    accessNotes,
                                }: {
    summary: BookingSummary
    slot: TimeSlot | undefined
    bookingId: string
    address: AddressDto
    passengerName: string
    passengerPhone: string
    passengerEmail: string
    atlOption: AtlOption | undefined
    accessNotes: string
}) {
    return (
        <Box mih="100vh" style={{display: 'flex', flexDirection: 'column'}}>
            <Box
                px={tokens.hero.px}
                pt={tokens.hero.pt}
                pb={tokens.hero.pb}
                style={{
                    backgroundColor: onBrandScrim.heroBg,
                    color: onBrandScrim.text,
                    position: 'relative',
                    overflow: 'hidden',
                }}
            >
                <FlightPathBackdrop/>
                <Container
                    size={tokens.hero.measure}
                    px={0}
                    style={{textAlign: 'center', position: 'relative', zIndex: 1}}
                >
                    <Center
                        w={80}
                        h={80}
                        mx="auto"
                        mb="lg"
                        style={{borderRadius: '50%', backgroundColor: onBrandScrim.fill}}
                    >
                        <CheckCircleIcon size={48} color={onBrandScrim.text}/>
                    </Center>
                    <Badge
                        variant="transparent"
                        mb="sm"
                        style={{
                            backgroundColor: onBrandScrim.fill,
                            color: onBrandScrim.text,
                            fontWeight: 700,
                            letterSpacing: '0.08em',
                        }}
                    >
                        Confirmed
                    </Badge>
                    <Title order={1} mb="xs" style={{...tokens.type.heroTitle, color: 'inherit'}}>
                        You're all set
                    </Title>
                </Container>
            </Box>

            <Container
                size={tokens.hero.measure}
                px={{base: 12, sm: 0}}
                mt={tokens.hero.overlap}
                pb={48}
                style={{position: 'relative', flex: 1, width: '100%'}}
            >
                <Stack gap="md">
                    <Card p="lg">
                        <Stack gap="md">
                            {summary.fileReference && (
                                <PunchedTag label="File reference" value={summary.fileReference}/>
                            )}

                            {slot && (
                                <DocketTile label="Delivery window" variant="tint">
                                    <Text fw={700} style={{fontSize: tokens.type.figure, lineHeight: 1.15}}>
                                        {slot.dayLabel}
                                    </Text>
                                    <Text fw={600} style={{fontVariantNumeric: 'tabular-nums'}}>
                                        {slot.label}
                                    </Text>
                                </DocketTile>
                            )}

                            <Divider/>

                            <DeliveryDocket
                                address={address}
                                passengerName={passengerName}
                                passengerPhone={passengerPhone}
                                passengerEmail={passengerEmail}
                                atlOption={atlOption}
                                accessNotes={accessNotes}
                            />
                        </Stack>
                    </Card>

                    <Button
                        component={RouterLink}
                        to={`/t/${bookingId}`}
                        size="lg"
                        fullWidth
                        rightSection={<ArrowRightIcon size={18}/>}
                        style={tokens.button.primary}
                    >
                        Track your delivery
                    </Button>

                    <Text size="sm" c="dimmed" ta="center">
                        We'll also text you when our driver is on the way.
                    </Text>
                </Stack>
            </Container>

            <PoweredByFooter/>
        </Box>
    )
}
