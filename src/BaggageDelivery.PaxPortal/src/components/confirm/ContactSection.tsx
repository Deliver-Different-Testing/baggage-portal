import { Stack, TextInput } from '@mantine/core'
import { FormSection } from './FormSection'
import { fieldTargetId } from './confirmValidation'

const PASSENGER_EMAIL_MAX = 100

export function ContactSection({
  passengerName,
  passengerPhone,
  passengerEmail,
  onNameChange,
  onPhoneChange,
  onEmailChange,
  showFieldError,
}: {
  passengerName: string
  passengerPhone: string
  passengerEmail: string
  onNameChange: (value: string) => void
  onPhoneChange: (value: string) => void
  onEmailChange: (value: string) => void
  showFieldError: (key: string) => string | undefined
}) {
  return (
    <FormSection title="Contact" subtitle="Who the driver should ask for">
      <Stack gap="xs">
        <TextInput
          id={fieldTargetId('passengerName')}
          label="Full name"
          value={passengerName}
          onChange={(e) => onNameChange(e.currentTarget.value)}
          autoComplete="name"
          required
          error={showFieldError('passengerName')}
        />
        <TextInput
          id={fieldTargetId('passengerPhone')}
          label="Phone number"
          description="So the driver can call you when they're close"
          inputWrapperOrder={['label', 'description', 'input', 'error']}
          value={passengerPhone}
          onChange={(e) => onPhoneChange(e.currentTarget.value)}
          autoComplete="tel"
          inputMode="tel"
          type="tel"
          required
          error={showFieldError('passengerPhone')}
        />
        <TextInput
          id={fieldTargetId('passengerEmail')}
          label="Email address"
          value={passengerEmail}
          onChange={(e) => onEmailChange(e.currentTarget.value)}
          autoComplete="email"
          type="email"
          autoCapitalize="off"
          required
          maxLength={PASSENGER_EMAIL_MAX}
          error={showFieldError('passengerEmail')}
        />
      </Stack>
    </FormSection>
  )
}
