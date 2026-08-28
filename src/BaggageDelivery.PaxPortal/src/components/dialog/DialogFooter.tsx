/**
 * DialogFooter
 *
 * A default (outlined) cancel and a filled primary action over a keyline top
 * border. While `submitting` the confirm button shows Mantine's loader and both
 * buttons disable. Buttons are pills via the theme's Button default radius.
 *
 * The one place this departs from Despatch: Despatch right-aligns the pair, which
 * suits a dispatch console. `.dd-dialog-footer` keeps that row from 576px up, and
 * below it the pair goes full width and stacks in source order — cancel, then the
 * primary at the bottom of the sheet where the thumb is. Order is never reversed
 * in CSS, so the visual, DOM and tab orders stay identical.
 */
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
      {/* size="md" (42px), not Mantine's 36px default. Despatch's footer buttons
          are console-sized and land under the 44px touch target on a phone; 42px
          is what every other control in this app is set to, and stacked on mobile
          they are full width as well. */}
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
