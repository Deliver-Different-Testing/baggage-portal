/**
 * DialogShell
 *
 * The standard modal wrapper for the DFRNT dialog design language, ported from
 * Despatch. Wraps `<Modal>` with the design defaults: centred, no built-in close
 * button (the header owns it), zero body padding (header/content/footer control
 * their own), the 28px corner, and a scrollable content box.
 *
 * The overlay is a light, unblurred scrim on purpose. In Despatch that is so the
 * job list stays readable underneath; here it is so the passenger can still see
 * the form they are confirming, which is the whole point of a read-back.
 *
 * Compose with <DialogHeader>, a content region and <DialogFooter> as children.
 */
import { Modal } from '@mantine/core'
import { dialogShellStyles, dialogSize } from './styles'
import type { DialogShellProps } from './types'

/**
 * Composed of `Modal.Root`/`Overlay`/`Content`/`Body` rather than the `Modal`
 * shorthand purely so `label` can reach the `role="dialog"` element — `Modal`
 * forwards extra props to the root, which is not the dialog node.
 */
export function DialogShell({
  opened,
  onClose,
  size = dialogSize.sm,
  label,
  children,
  styles,
  overlayProps,
  ...rest
}: DialogShellProps) {
  return (
    <Modal.Root
      opened={opened}
      onClose={onClose}
      size={size}
      centered
      padding={0}
      radius="xl"
      {...rest}
      styles={{ ...dialogShellStyles, ...styles }}
    >
      <Modal.Overlay backgroundOpacity={0.25} {...overlayProps} />
      <Modal.Content aria-label={label}>
        <Modal.Body>{children}</Modal.Body>
      </Modal.Content>
    </Modal.Root>
  )
}
