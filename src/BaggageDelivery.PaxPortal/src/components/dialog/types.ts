import type { ModalProps } from '@mantine/core'
import type { ReactNode } from 'react'
import type { HeaderVariant } from './styles'

export interface DialogShellProps extends Omit<ModalProps, 'title' | 'opened' | 'onClose'> {
  opened: boolean
  onClose: () => void
  size?: ModalProps['size']
  label?: string
}

export interface DialogHeaderProps {
  icon: ReactNode
  title: ReactNode
  subtitle?: ReactNode
  onClose: () => void
  variant?: HeaderVariant
  closeDisabled?: boolean
  actions?: ReactNode
}

export interface DialogFooterProps {
  onCancel?: () => void
  onConfirm?: () => void
  confirmLabel?: ReactNode
  cancelLabel?: ReactNode
  confirmIcon?: ReactNode
  confirmDisabled?: boolean
  submitting?: boolean
  hideConfirm?: boolean
  hideCancel?: boolean
}
