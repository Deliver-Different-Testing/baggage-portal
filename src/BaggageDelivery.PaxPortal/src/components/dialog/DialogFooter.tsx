import { Button } from '@mantine/core'
import type { DialogFooterProps } from './types'

export function DialogFooter({
  onCancel,
  onConfirm,
  confirmLabel,
  cancelLabel = 'Cancel',
  confirmIcon,
  confirmDisabled = false,
  submitting = false,
  hideConfirm = false,
  hideCancel = false,
}: DialogFooterProps) {
  return (
    <div className="dd-dialog-footer">
      {!hideCancel && (
        <Button variant="default" size="md" onClick={onCancel} disabled={submitting}>
          {cancelLabel}
        </Button>
      )}
      {!hideConfirm && (
        <Button
          size="md"
          onClick={onConfirm}
          loading={submitting}
          disabled={confirmDisabled}
          leftSection={confirmIcon}
        >
          {confirmLabel}
        </Button>
      )}
    </div>
  )
}
