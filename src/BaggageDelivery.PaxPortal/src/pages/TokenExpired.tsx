import { UnlinkIcon } from '../components/Icon'
import { FullPageMessage } from '../components/FullPageMessage'

const WARNING = 'var(--mantine-color-orange-6)'

export function TokenExpired() {
  return (
    <FullPageMessage
      icon={<UnlinkIcon size={36} color={WARNING} />}
      iconColor={WARNING}
      title="Booking not found"
      description="We could not find a baggage delivery booking for this link. If you still need to confirm your delivery, please contact our team — we will send you a new link."
    />
  )
}
