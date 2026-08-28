import { Modal } from '@mantine/core'
import { dialogShellStyles, dialogSize } from './styles'
import type { DialogShellProps } from './types'

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
