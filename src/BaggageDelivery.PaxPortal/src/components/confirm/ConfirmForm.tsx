import type { UseQueryResult } from '@tanstack/react-query'
import { Alert, Anchor, Box, Card, Container, Divider, Stack, Text } from '@mantine/core'
import { ConfirmHero } from '../ConfirmHero'
import { PoweredByFooter } from '../PoweredByFooter'
import { AddressSection } from './AddressSection'
import { AtlSection } from './AtlSection'
import { ConfirmActionBar } from './ConfirmActionBar'
import { ConfirmedScreen } from './ConfirmedScreen'
import { ConfirmErrorSummary } from './ConfirmErrorSummary'
import { ConfirmReviewModal } from './ConfirmReviewModal'
import { ContactSection } from './ContactSection'
import { AddressUnserviceableCard } from './AddressUnserviceableCard'
import { ReadOnlyAddressSection } from './ReadOnlyAddressSection'
import { ServiceSection } from './ServiceSection'
import { WindowSection } from './WindowSection'
import { useConfirmForm } from './useConfirmForm'
import { addressLines } from '../../utils/address'
import { tokens } from '../../styles/mantineTheme'
import type { BookingConfirmation, BookingSummary, TimeSlot } from '../../api/client'

export function ConfirmForm({
  bookingId,
  summary,
  online,
  slots,
  editing,
  onCancelEdit,
}: {
  bookingId: string
  summary: BookingSummary
  online: boolean
  slots: UseQueryResult<TimeSlot[]>
  editing?: BookingConfirmation | null
  onCancelEdit?: () => void
}) {
  const form = useConfirmForm(bookingId, summary, slots, editing, onCancelEdit)
  const { address, isEditing } = form

  if (form.confirmed) {
    return (
      <ConfirmedScreen
        summary={summary}
        slot={form.selectedSlot}
        address={address}
        passengerName={form.passengerName}
        passengerPhone={form.passengerPhone}
        passengerEmail={form.passengerEmail}
        atlOption={form.selectedAtlOption}
        accessNotes={form.accessNotes}
      />
    )
  }

  const street = addressLines(address)[0] ?? ''
  const barSummary = form.selectedSlot
    ? [form.selectedSlot.dayLabel, form.selectedSlot.label, street].filter(Boolean).join(' · ')
    : null

  return (
    <Box mih="100vh" pb={{ base: 140, sm: 156 }}>
      <ConfirmHero
        summary={summary}
        accent={form.accent}
        title={isEditing ? 'Change your delivery' : undefined}
        body={
          isEditing
            ? 'Pick a new delivery window or update how we should leave your bag.'
            : undefined
        }
      />

      <Container
        size={tokens.hero.measure}
        px={0}
        mt={tokens.hero.overlap}
        style={{ position: 'relative', zIndex: 1 }}
      >
        <Stack gap="md" px={{ base: 12, sm: 0 }}>
          <Card
            p={0}
            style={{
              overflow: 'hidden',
              borderTop: '2px dashed var(--dd-outline-variant)',
            }}
          >
            <Box className="pax-stagger">
              <Box px="lg" pt="lg">
                <ConfirmErrorSummary
                  fieldErrors={form.fieldErrors}
                  submitCount={form.submitCount}
                />
              </Box>

              <ContactSection
                passengerName={form.passengerName}
                passengerPhone={form.passengerPhone}
                passengerEmail={form.passengerEmail}
                onNameChange={form.setPassengerName}
                onPhoneChange={form.setPassengerPhone}
                onEmailChange={form.setPassengerEmail}
                errors={form.contactErrors}
              />

              <Divider />

              {isEditing ? (
                <ReadOnlyAddressSection
                  address={address}
                  supportPhone={summary.supportPhone}
                />
              ) : (
                <AddressSection
                  bookingId={bookingId}
                  address={address}
                  labels={form.labels}
                  confirmed={form.addressConfirmed}
                  editing={form.editingAddress}
                  serverCountryError={form.serverCountryError}
                  onConfirmedChange={form.handleAddressConfirmedChange}
                  onToggleEdit={form.handleToggleAddressEdit}
                  onAddressSelect={form.handleAddressSelect}
                  onPatch={form.patchAddress}
                  onLocationPatch={form.setLocation}
                  onCountryChange={form.handleCountryChange}
                  errors={form.addressErrors}
                />
              )}

              {form.addressChanged && (
                <>
                  <Divider />

                  {form.noServiceAvailable ? (
                    <AddressUnserviceableCard
                      address={address}
                      airlineLabel={summary.airlineLabel}
                      supportPhone={summary.supportPhone}
                      requested={form.helpRequested}
                      submitting={form.requestingHelp}
                      onRequestHelp={form.requestAddressHelp}
                    />
                  ) : (
                    <ServiceSection
                      services={form.availableServices}
                      selectedId={form.selectedService?.jobTypeId ?? null}
                      loading={form.servicesLoading}
                      error={form.servicesError}
                      fieldError={form.serviceError}
                      onSelect={form.handleSelectService}
                      onRetry={form.retryServices}
                    />
                  )}
                </>
              )}

              {!form.noServiceAvailable && (
                <>
                  <Divider />

                  <WindowSection
                    slots={form.slots}
                    available={form.availableSlots}
                    selectedId={form.effectiveSlotId}
                    selectedSlot={form.selectedSlot}
                    supportPhone={summary.supportPhone}
                    error={form.slotError}
                    onSelect={form.handleSelectSlot}
                    onRunStartPassed={form.handleRunStartPassed}
                  />
                </>
              )}

              <Divider />

              <AtlSection
                options={form.atlOptions}
                selectedId={form.atlOptionId}
                accessNotes={form.accessNotes}
                notesRequired={form.atlNotesRequired}
                onToggle={form.handleAtlToggle}
                onSelect={form.setAtlOptionId}
                onNotesChange={form.setAccessNotes}
                errors={form.atlErrors}
              />
            </Box>
          </Card>

          {!online && (
            <Alert color="orange" variant="light">
              You appear to be offline. Connect to the internet to submit your confirmation.
            </Alert>
          )}

          {summary.supportPhone && (
            <Text size="sm" c="dimmed" ta="center" px="md">
              Any questions? Call{' '}
              <Anchor
                href={`tel:${summary.supportPhone.replace(/\s/g, '')}`}
                c="dimmed"
                underline="always"
              >
                {summary.supportPhone}
              </Anchor>
              {summary.jobNumber ? ` and quote tracking number ${summary.jobNumber}.` : '.'}
            </Text>
          )}
        </Stack>

        <PoweredByFooter />
      </Container>

      <ConfirmActionBar
        summary={barSummary}
        disabled={!online}
        onReview={form.review}
        label={isEditing ? 'Review changes' : undefined}
        onCancel={isEditing ? onCancelEdit : undefined}
        cancelLabel={isEditing ? 'Keep my current booking' : undefined}
      />

      <ConfirmReviewModal
        opened={form.reviewOpen}
        onClose={form.closeReview}
        onConfirm={form.submit}
        submitting={form.submitting}
        online={online}
        slot={form.selectedSlot}
        address={address}
        passengerPhone={form.passengerPhone}
        title={isEditing ? 'Check your changes' : undefined}
        subtitle={
          isEditing ? "We'll update your booking as soon as you save." : undefined
        }
        confirmLabel={isEditing ? 'Save changes' : undefined}
      />
    </Box>
  )
}
