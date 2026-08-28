/**
 * DialogHeader
 *
 * The solid brand-fill header: a 40×40 icon chip, a title (+ optional subtitle)
 * and a close button, over a flat colour bar. No gradient — the colour change is
 * the separator. `variant` selects the fill; see {@link headerColors}.
 */
import { Box, CloseButton, Group, Text, ThemeIcon } from '@mantine/core'
import { dialogStickyChromeStyle, headerChipProps, headerColors, headerOnColor } from './styles'
import type { DialogHeaderProps } from './types'

export function DialogHeader({
  icon,
  title,
  subtitle,
  onClose,
  variant = 'primary',
  closeDisabled = false,
  actions,
}: DialogHeaderProps) {
  const fg = headerOnColor(variant)
  return (
    <Group
      wrap="nowrap"
      gap="md"
      px="lg"
      py="sm"
      style={{
        backgroundColor: headerColors[variant].bg,
        color: fg,
        ...dialogStickyChromeStyle('top'),
      }}
    >
      <ThemeIcon {...headerChipProps(variant)}>{icon}</ThemeIcon>
      <Box style={{ flex: 1, minWidth: 0 }}>
        {/* A real heading, so the dialog title is reachable by role. */}
        <Text component="h2" m={0} fw={600} fz="lg" c={fg}>
          {title}
        </Text>
        {subtitle != null && (
          <Text fz="sm" c={fg} style={{ opacity: 0.85 }}>
            {subtitle}
          </Text>
        )}
      </Box>
      {actions}
      {/* Mantine's own close affordance — it brings the icon, the size ramp and
          the hover, so the header only has to set the on-colour. */}
      <CloseButton
        onClick={onClose}
        disabled={closeDisabled}
        aria-label="Close dialog"
        c={fg}
        iconSize={20}
      />
    </Group>
  )
}
