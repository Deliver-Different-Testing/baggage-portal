import type { ModalProps } from '@mantine/core'
import type { ReactNode } from 'react'
import type { HeaderVariant } from './styles'

export interface DialogShellProps extends Omit<ModalProps, 'title' | 'opened' | 'onClose'> {
  opened: boolean
  onClose: () => void
  /** Modal width. Defaults to `dialogSize.sm`. */
  size?: ModalProps['size']
  /**
   * Accessible name for the dialog — normally the header title.
   *
   * It has to be a literal label rather than an `aria-labelledby` pointing at the
   * visible title: Mantine derives `aria-labelledby` from its own `Modal.Title`
   * slot and writes `undefined` over anything we pass when that slot is unused,
   * which it always is here — the header is `<DialogHeader>`, not Mantine's.
   */
  label?: string
}

export interface DialogHeaderProps {
  icon: ReactNode
  title: ReactNode
  subtitle?: ReactNode
  onClose: () => void
  /** Fill palette. Defaults to `primary`. */
  variant?: HeaderVariant
  /** Disables the close button, e.g. while a submit is in flight. */
  closeDisabled?: boolean
  /** Extra controls rendered before the close button. */
  actions?: ReactNode
}

export interface DialogFooterProps {
  /** Required unless `hideCancel` — the dialog's only way out is then the header close. */
  onCancel?: () => void
  /** Required unless `hideConfirm` — a view-only dialog has nothing to confirm. */
  onConfirm?: () => void
  confirmLabel?: ReactNode
  cancelLabel?: ReactNode
  /** Icon before the confirm label, hidden by the loader while submitting. */
  confirmIcon?: ReactNode
  /** Disables confirm independently of `submitting`. */
  confirmDisabled?: boolean
  /** Shows a loader on confirm and disables both buttons. */
  submitting?: boolean
  /** Drops the confirm button — for view-only dialogs that only need a close. */
  hideConfirm?: boolean
  /** Drops the cancel button — for dialogs whose single action also dismisses them. */
  hideCancel?: boolean
}
