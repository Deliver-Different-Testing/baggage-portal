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
import { WindowSection } from './WindowSection'
import { useConfirmForm } from './useConfirmForm'
import { addressLines } from '../../utils/address'
import { tokens } from '../../styles/mantineTheme'
import type { BookingSummary, TimeSlot } from '../../api/client'

export function ConfirmForm({
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
  const form = useConfirmForm(bookingId, summary, slots)
  const { address, showFieldError } = form

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
      <ConfirmHero summary={summary} accent={form.accent} />

      <Container
        size={tokens.hero.measure}
        px={0}
        mt={tokens.hero.overlap}
        style={{ position: 'relative', zIndex: 1 }}
      >
        <Stack gap="md" px={{ base: 12, sm: 0 }}>
          <Card
            p={0}
            style={{ overflow: 'hidden', borderTop: '2px dashed var(--dd-outline-variant)' }}
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
                showFieldError={showFieldError}
              />

              <Divider />

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
                showFieldError={showFieldError}
              />

              <Divider />

              <WindowSection
                slots={slots}
                available={form.availableSlots}
                selectedId={form.effectiveSlotId}
                selectedSlot={form.selectedSlot}
                supportPhone={summary.supportPhone}
                error={showFieldError('slot')}
                onSelect={form.handleSelectSlot}
                onRunStartPassed={form.handleRunStartPassed}
              />

              <Divider />

              <AtlSection
                options={form.atlOptions}
                selectedId={form.atlOptionId}
                accessNotes={form.accessNotes}
                notesRequired={form.atlNotesRequired}
                onToggle={form.handleAtlToggle}
                onSelect={form.setAtlOptionId}
                onNotesChange={form.setAccessNotes}
                showFieldError={showFieldError}
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

      <ConfirmActionBar summary={barSummary} disabled={!online} onReview={form.review} />

      <ConfirmReviewModal
        opened={form.reviewOpen}
        onClose={form.closeReview}
        onConfirm={form.submit}
        submitting={form.submitting}
        online={online}
        slot={form.selectedSlot}
        address={address}
        passengerPhone={form.passengerPhone}
      />
    </Box>
  )
}
