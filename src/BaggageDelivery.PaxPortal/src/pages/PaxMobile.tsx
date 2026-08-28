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

// Widths of the tucJob columns these fields are written to, mirrored from
// ConfirmBookingRequest. Longer input is rejected by the API, not truncated.
const ACCESS_NOTES_MAX = 120
const PASSENGER_EMAIL_MAX = 100
const ADDRESS_LINE2_MAX = 200
// The fields that live inside the collapsed address editor. An error on one of
// them is an error the passenger can neither see nor fix, so it opens the editor.
const ADDRESS_FIELD_KEYS = ['line1', 'suburb', 'city', 'postCode', 'country']

// Which validation keys belong to which card. The chip on each section header is
// driven straight off the `fieldErrors` map the form already builds, so completion
// state can never disagree with what submitting the form would actually say.
const SECTION_FIELDS = {
    details: ['passengerName', 'passengerPhone', 'passengerEmail'],
    address: [...ADDRESS_FIELD_KEYS, 'addressConfirmed'],
    window: ['slot'],
    atl: ['accessNotes'],
} as const

const ERROR_COLOR = 'var(--mantine-color-red-6)'

/**
 * The confirm page while the booking is loading. The Ink hero renders for real
 * (see ConfirmHeroSkeleton) and the panel below it is blocked out at the height it
 * will occupy, so the page keeps its shape instead of flashing an empty surface and
 * then snapping a full form into place.
 */
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

/** Below this, the run leaving stops being context and becomes the thing to act on. */
const RUN_URGENT_MS = 10 * 60_000

/**
 * The strip under the window list: what the deadline is for, and how long is left.
 *
 * The countdown ticks once a second, so the whole strip lives inside this memo
 * rather than only the number. Owning the tick here keeps each second to this
 * subtree — otherwise the hero, all eight inputs, the ATL list and the review
 * dialog re-render every second while the passenger is still reading the page.
 * `onExpire` must be stable (useCallback).
 *
 * The strip is neutral, not yellow, while the run is still a way off: a run that
 * has not left yet is information rather than a warning, and
 * --mantine-color-yellow-light over the charcoal ladder mixes to a muddy olive.
 * Inside the last ten minutes it genuinely is a warning — miss it and the chosen
 * window is dropped from under the passenger — so it escalates to orange, the same
 * tint the offline alert already uses.
 */
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
            {/* Names the stake rather than giving an instruction. The line read "Make sure
          you confirm your booking before the chosen run time", which is longer, tells
          the passenger what to do instead of what they lose, and was set in the
          quietest style in the card. */}
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
        return <ConfirmSkeleton/>
    }

    if (!booking.data) {
        // The same shell TokenExpired uses, rather than a bare red Alert on an otherwise
        // empty page. The old copy also told the passenger to contact support without
        // giving them any way to — and at this point there is no booking loaded, so there
        // is no support number to offer. Retrying is the action they actually have.
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
    // Almost every bag goes to the address already on the booking, so the page opens
    // on that address to be read and ticked rather than on a form to be filled in.
    // Editing is the exception path and lives behind Edit. The tick stays either
    // way: a wrong address puts the bag on a stranger's doorstep, so signing it off
    // is a deliberate act.
    const [addressConfirmed, setAddressConfirmed] = useState(false)
    const [editingAddress, setEditingAddress] = useState(false)
    const [passengerName, setPassengerName] = useState(summary.passengerName ?? '')
    const [passengerPhone, setPassengerPhone] = useState(summary.passengerPhone ?? '')
    const [passengerEmail, setPassengerEmail] = useState(summary.passengerEmail ?? '')
    const [selectedSlotId, setSelectedSlotId] = useState<string | null>(null)
    // Authority to leave starts off: leaving a suitcase unattended is a decision the
    // passenger opts into, not one they have to notice and undo. Switching it on
    // lands on the handoff point the server resolved, so the portal never has to
    // know a LeaveNotHomeId or match on a display name.
    const defaultAtlOptionId = summary.defaultAtlOptionId ?? null
    const [atlOptionId, setAtlOptionId] = useState<number | null>(null)
    const [accessNotes, setAccessNotes] = useState('')
    const [confirmed, setConfirmed] = useState(false)
    const [submitAttempted, setSubmitAttempted] = useState(false)
    // The server resolves the country to an ISO-2 code and is the only thing that
    // can tell us a spelling is unrecognisable — surface its message on the field
    // rather than the generic retry toast.
    const [serverCountryError, setServerCountryError] = useState<string | null>(null)
    const [reviewOpen, {open: openReview, close: closeReview}] = useDisclosure(false)

    // The carrier's colour, for the hero only — see `airlineAccent`. Memoized because
    // ConfirmHero is memoized and a fresh object every keystroke would defeat it.
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

    // Stable identity so the memoized SlotOption rows aren't invalidated each render.
    const handleSelectSlot = useCallback((id: string) => setSelectedSlotId(id), [])

    // A page left open long enough outlives the run it has selected. Once that run
    // has departed, pull a fresh list and drop the selection so defaultSlotId
    // re-selects whatever is genuinely first available now. A review dialog left
    // open across the boundary would be reading back a window that no longer
    // exists, so it closes with it.
    const {refetch: refetchSlots} = slots
    const handleRunStartPassed = useCallback(() => {
        setSelectedSlotId(null)
        closeReview()
        void refetchSlots()
    }, [closeReview, refetchSlots])

    // Ticking the box is the passenger saying they are done with the address, so it
    // closes the editor behind them; unticking leaves it as it was.
    const handleAddressConfirmedChange = useCallback((value: boolean) => {
        setAddressConfirmed(value)
        if (value) setEditingAddress(false)
    }, [])

    // Reaching for Edit withdraws the sign-off — the address they ticked is not the
    // one they are about to type.
    const handleToggleAddressEdit = useCallback(() => {
        setEditingAddress((editing) => {
            if (!editing) setAddressConfirmed(false)
            return !editing
        })
    }, [])

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

    const addressFieldInvalid = ADDRESS_FIELD_KEYS.some((key) => fieldErrors[key])

    // Section completeness, read straight off the same validation the submit gate uses.
    // Shown from the start rather than only after a failed submit: most of these fields
    // arrive prefilled, and a passenger who can see three ticks already knows the page
    // is asking them for one thing, not four.
    const sectionDone = (section: keyof typeof SECTION_FIELDS): SectionStatus =>
        SECTION_FIELDS[section].every((key) => !fieldErrors[key]) ? 'complete' : 'incomplete'

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
                // Reopen the gate and the editor: the passenger can't fix a field they
                // can't see.
                setAddressConfirmed(false)
                setEditingAddress(true)
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
            if (addressFieldInvalid) setEditingAddress(true)
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
                    {/* One panel with keyline dividers, not four bordered cards. Four boxes on a
              shaded page read as a settings screen — the same reason the section icons
              were dropped for status chips — and they nested three deep, since the
              address gate and every delivery-window row carry a border of their own.
              `overflow: hidden` so the primary section's keyline is clipped by the
              card radius rather than poking out of the corner. */}
                    <Card p={0} style={{overflow: 'hidden'}}>
                        {/* The stagger moves here with the sections it animates; it used to sit on
                the Stack, whose children are now the panel and the offline alert. */}
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
                                    {/* Mantine keeps a collapsed Collapse mounted, so the fields stay in
                      the DOM but out of the accessibility tree — which is what keeps the
                      compact state compact for a screen reader too. */}
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
                                            {/* The buzzer number, the gate code, the unit round the back — what
                          the driver needs and the street address has nowhere to put. Out
                          of sight until the passenger asks to edit, and never required. */}
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

                            {/* The one decision on the page, so it carries the weight: the only card
                  with the primary emphasis and the only place a brand tint means
                  "chosen" rather than "here". */}
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
                                    <Stack gap={4}>
                                        <Text size="sm">
                                            There are no delivery windows available for this booking yet.
                                        </Text>
                                        {/* Its own node, not a tail concatenated into the sentence above:
                        the support line is the passenger's only way forward here and
                        was inseparable from the first sentence for styling. */}
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
                                            // Matches tucJob.ucjbToSpecial. A hard cap with no counter reads
                                            // as the field being broken, so show the remaining room.
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

                {/* No support number here: the confirm flow has one job, and a phone number
            at the bottom of it is an invitation to stop and call instead. The
            empty-window state and the confirmed screen still carry it, where it is
            the passenger's only way forward. */}
                <PoweredByFooter/>
            </Container>

            {/* A floating button, not a docked bar — see .pax-action-bar in index.css. The
          band of chrome it used to sit on drew a hard edge across the page for a
          single control. "Review" and not "confirm": this step only opens the
          read-back, and a passenger who reads "confirm" on it expects the booking to
          be made when they tap. */}
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
            // A tap outside mid-flight would hide a request the passenger can't retry.
            closeOnClickOutside={!submitting}
            closeOnEscape={!submitting}
            transitionProps={{transition: 'pop', duration: tokens.duration.fast}}
        >
            {/* The intro line is the header's subtitle rather than the first thing in
          the body — it says what the dialog is for, which is the header's job. */}
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
                        // The one field with real consequences, stamped at 2px against the
                        // pill buttons and the 28px shell.
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

                    {/* Despatch's section Paper holds the docket. The container takes the
              12px corner of the design language; the printed lines inside keep
              their own 2px, so the motif survives the port. */}
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
                // Not "Cancel": this is the way back to the form, not a way to abandon
                // the booking, and the passenger has typed into that form.
                cancelLabel="Edit details"
                confirmDisabled={!online}
                submitting={submitting}
            />
        </DialogShell>
    )
}

// Memoized for the same reason as SlotOption: the list is fixed for the booking, so
// it shouldn't re-render behind every keystroke in the fields above it. `onSelect`
// is the raw setState, which React keeps stable.
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

/**
 * The address as it stands, and one tick to sign it off. This is the whole
 * address step for the bags going where the booking already says — the fields sit
 * behind the Edit control for the rest.
 */
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
                // The panel stays neutral once ticked. Tinting it cyan put a cyan checkbox
                // on a cyan ground, and the control that carries the whole sign-off was the
                // hardest thing on the card to see. A brand rule down the edge marks it
                // signed off instead, and the box keeps a surface to stand out against.
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
                    {/* Read first, tick second. While the fields are open they are the
              address, and repeating it above them is noise. */}
                    {!editing && (
                        <>
                            {addressLines(address).map((line, i) => (
                                <Text key={`${i}-${line}`} size="sm" fw={500} style={{lineHeight: 1.35}}>
                                    {line}
                                </Text>
                            ))}
                            {/* Deane's review: the delivery instructions are what the driver
                  needs, so they are read back here rather than only in the
                  editor the passenger has no reason to open. */}
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

/**
 * The 32px slot on each section header. It used to hold a glyph that restated the
 * title in a picture — a person beside "Your details", a pin beside "Delivery
 * address" — which is decoration, and four identical tinted squares down the left
 * edge read as a settings page. It now carries state instead, so the column of chips
 * is something the passenger can scan to see what is left.
 */
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
                // A dash for a section that is switched off, an empty box for one still to
                // do — an empty box on an optional section reads as an outstanding task.
                status === 'optional' && (
                    <Box w={10} h={2} style={{backgroundColor: 'var(--mantine-color-dimmed)'}}/>
                )
            )}
        </Center>
    )
}

/**
 * One section of the confirm panel. Not a Card: the four sections share a single
 * card and are separated by keyline dividers, so this contributes padding and a
 * header, not a box.
 */
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
                    ? // The one decision on the page, marked on the section rather than on its
                      // title. `emphasis` used to change only the heading's weight and size,
                      // which is not something a passenger can see against three sibling
                      // headings. A brand rule down the edge is the same device the address
                      // gate already uses for a signed-off address. Deliberately not a tinted
                      // ground: the selected window row inside is itself brand-tinted, and a
                      // tint on a tint is the cyan-on-cyan problem the address gate documents.
                    {boxShadow: 'inset 3px 0 0 0 var(--mantine-color-brand-filled)'}
                    : undefined
            }
        >
            <Group gap="sm" align="center" mb="md" wrap="nowrap">
                <SectionStatusChip title={title} status={status}/>
                <Box style={{flex: 1, minWidth: 0}}>
                    {/* A real h2. These were plain `Text`, which left the page with one
              heading (the hero h1) for an eight-field form, and set the section
              titles at the same size as the field labels beneath them. */}
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

/**
 * The window list as a real radio group. Selection follows focus with the arrow keys,
 * per the WAI-ARIA radiogroup pattern, and only the selected row is in the tab order
 * so the list is one tab stop rather than one per window. `effectiveSlotId` always
 * resolves once the slots load, so exactly one row is always tabbable.
 */
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
        // Focus has to follow the selection or the next arrow press comes from the old
        // row and the caret appears to jump back.
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

// Memoized so typing in the form's text fields (which re-renders ConfirmForm on every
// keystroke) doesn't re-render every slot in the list. `onSelect` takes the slot id so
// ConfirmForm can pass one stable handler to all rows instead of a per-row closure —
// without that, the changing prop identity would defeat the memo.
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
        // Roving tab index: the group is one tab stop, the arrow keys move within it.
        tabIndex={selected ? 0 : -1}
        // Chrome (focus ring, transition) lives in index.css — see .pax-slot. The
        // previous inline `outline: none` left the list with no visible focus at all.
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
            {/* Date above window, as in the mock — a time on its own leaves the
            passenger guessing which day it belongs to. The window carries the
            weight though: the day is context, the hours are the decision, and
            this is the one decision on the page. */}
            <Text size="xs" c="dimmed">
                {slot.dayLabel}
            </Text>
            <Text size="md" fw={700} style={{fontVariantNumeric: 'tabular-nums'}}>
                {slot.label}
            </Text>
            {slot.firstAvailable && (
                // Brand-toned, not green: a second accent inside an already brand-tinted
                // row reads as two competing signals in sixty pixels. Deep cyan, not the
                // brand fill — cyan on a cyan tint at 10px is about 1.9:1.
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
        // Flex column so the sign-off lands on the bottom edge rather than floating
        // above a few hundred pixels of blank surface.
        <Box mih="100vh" style={{display: 'flex', flexDirection: 'column'}}>
            {/* The Ink hero, same as the confirm screen it replaces. A full-bleed green
          band introduced a fourth brand colour at the most memorable moment and
          discarded the airline theme with it; the tick and the badge carry the
          success on their own. */}
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
                // width:100% because Container centres itself with auto inline margins,
                // and an auto cross-axis margin opts a flex item out of stretching.
                style={{position: 'relative', flex: 1, width: '100%'}}
            >
                <Stack gap="md">
                    {/* The docket. This is the passenger's only record of what they just
              submitted and the screen they are most likely to screenshot, so it is
              set as the thing it is — a printed receipt, at 2px against the pill
              button below it — rather than as a tick and a headline. */}
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
