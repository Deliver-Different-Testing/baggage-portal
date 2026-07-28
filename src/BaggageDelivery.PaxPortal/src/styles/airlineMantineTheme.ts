/**
 * Per-airline brand override for the passenger confirm flow. DFRNT cyan is the house
 * primary for all tenants; when a booking resolves an airline code we recolor the
 * confirm page to that airline's brand — the Mantine analog of the old nested MUI
 * `ThemeProvider` in PaxMobile.
 *
 * Mantine is CSS-variable driven, so we can't just swap a palette slot: a nested
 * `MantineProvider` merges its theme with Mantine's DEFAULT theme (not the parent),
 * so we merge the airline `brand` tuple over the full `dfrntTheme` to keep the fonts,
 * tokens and pill/component overrides. The nested provider's generated `:root`
 * variables are injected after the root provider's, so they win by CSS source order
 * for as long as PaxMobile is mounted, then revert on unmount. Only one route renders
 * at a time, so page-wide scoping is exactly what we want here.
 */
import { generateColors } from '@mantine/colors-generator'
import { mergeThemeOverrides, type MantineThemeOverride } from '@mantine/core'
import { dfrntTheme } from './mantineTheme'
import type { AirlineBrand } from './airlineBranding'

/**
 * Full theme override for a given airline brand: the DFRNT theme with its `brand`
 * color tuple regenerated from the airline's primary hex. Pass to a nested
 * `<MantineProvider theme={airlineThemeOverride(brand)}>`. `autoContrast` (enabled on
 * the base theme) picks readable button text, covering light-on-dark airlines like
 * Spirit (`NK`, yellow) without an explicit contrast var.
 */
export function airlineThemeOverride(brand: AirlineBrand): MantineThemeOverride {
  return mergeThemeOverrides(dfrntTheme, {
    colors: { brand: generateColors(brand.primary) },
  })
}
