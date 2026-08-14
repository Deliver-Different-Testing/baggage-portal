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
import {
  mergeThemeOverrides,
  type MantineColorShade,
  type MantineColorsTuple,
  type MantineThemeOverride,
} from '@mantine/core'
import { dfrntTheme } from './mantineTheme'
import { getMd3Scheme } from './md3'
import type { AirlineBrand } from './airlineBranding'

/** The dark page surface a filled brand colour has to hold up against. */
const DARK_SURFACE = getMd3Scheme(true, 'dark').surface

/** WCAG 2.1 non-text minimum — the bar for a button fill or a tinted tile. */
const MIN_CONTRAST = 3

function relativeLuminance(hex: string): number {
  const v = hex.replace('#', '')
  const [r, g, b] = [0, 2, 4].map((i) => {
    const c = parseInt(v.slice(i, i + 2), 16) / 255
    return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4
  })
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

function contrastRatio(a: string, b: string): number {
  const [la, lb] = [relativeLuminance(a), relativeLuminance(b)]
  return (Math.max(la, lb) + 0.05) / (Math.min(la, lb) + 0.05)
}

/**
 * Where the seed hex actually landed in its generated ramp. `generateColors` places
 * the seed by luminance, so a deep brand (Air NZ #008c95) lands at 9 while a mid one
 * (#2196f3) lands at 5 — the reason a fixed `primaryShade` shipped every airline a
 * neon-ified stand-in for its own colour.
 */
function seedShade(ramp: MantineColorsTuple, seed: string): MantineColorShade {
  const i = ramp.findIndex((c) => c.toLowerCase() === seed.toLowerCase())
  return (i === -1 ? 5 : i) as MantineColorShade
}

/**
 * The seed itself where it reads on the charcoal page, else the nearest lighter shade
 * that does. Near-black brands (Lufthansa #05164d) are invisible as a fill on
 * `--dd-surface`, so they step up the ramp until they clear {@link MIN_CONTRAST}.
 * Light mode keeps the seed unconditionally — brand fidelity is the point, and the
 * label colour is handled by the theme's `autoContrast`.
 */
function darkShade(ramp: MantineColorsTuple, from: MantineColorShade): MantineColorShade {
  for (let i = from; i > 0; i--) {
    if (contrastRatio(ramp[i], DARK_SURFACE) >= MIN_CONTRAST) return i as MantineColorShade
  }
  return 0
}

/**
 * Full theme override for a given airline brand: the DFRNT theme with its `brand`
 * color tuple regenerated from the airline's primary hex, and `primaryShade` pointed
 * at the shade that colour actually occupies. Pass to a nested
 * `<MantineProvider theme={airlineThemeOverride(brand)}>`. `autoContrast` (enabled on
 * the base theme) picks readable button text, covering light-on-dark airlines like
 * Spirit (`NK`, yellow) without an explicit contrast var.
 */
export function airlineThemeOverride(brand: AirlineBrand): MantineThemeOverride {
  const colors = generateColors(brand.primary)
  const light = seedShade(colors, brand.primary)
  return mergeThemeOverrides(dfrntTheme, {
    colors: { brand: colors },
    primaryShade: { light, dark: darkShade(colors, light) },
  })
}
