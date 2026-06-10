import LinkOffRoundedIcon from '@mui/icons-material/LinkOffRounded'
import { FullPageMessage } from '../components/FullPageMessage'

export function TokenExpired() {
  return (
    <FullPageMessage
      icon={<LinkOffRoundedIcon sx={{ fontSize: 36, color: 'warning.dark' }} />}
      iconColor="#FF9800"
      title="Booking not found"
      description="We could not find a baggage delivery booking for this link. If you still need to confirm your delivery, please contact our team — we will send you a new link."
    />
  )
}
