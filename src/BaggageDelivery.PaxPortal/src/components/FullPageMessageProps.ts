import type { ReactNode } from 'react'

export interface FullPageMessageProps {
  icon: ReactNode
  iconColor: string
  title: string
  description: string
  actionLabel?: string
  onAction?: () => void
  actionIcon?: ReactNode
}
