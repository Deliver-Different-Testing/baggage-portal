/**
 * Per-airline accent for the passenger confirm flow.
 *
 * The split this module enforces: **airline colour is identity, DFRNT cyan is
 * affordance.** The carrier's colour appears only on the Ink-Blue hero — the rule at
 * its bottom edge, the luggage chip, the baggage tag's punch and divider. Every
 * button, focus ring, selected slot and section chip stays DFRNT cyan for every
 * tenant, so "what can I press" never changes meaning between carriers.
 *
 * This replaces the earlier `airlineThemeOverride`, which regenerated the whole
 * `brand` ramp from the carrier hex and had it exactly backwards: affordance
 * recoloured per tenant while the hero — the one surface where carrier identity reads
 * as branding rather than as a mis-skinned app — stayed Ink Blue.
 *
 * No Mantine variants are needed for any of the three surfaces, so there is no nested
 * `MantineProvider` any more: these are flat values passed as props.
 */
import { generateColors } from '@mantine/colors-generator'
import { alpha, type MantineColorShade, type MantineColorsTuple } from '@mantine/core'
import { contrastRatio } from './md3'
import type { AirlineBrand } from './airlineBranding'

/** The Ink-Blue hero every accent has to hold up against. Matches `ink[9]`. */
const INK_HERO = '#0d0c2c'

/** WCAG 2.1 non-text minimum — the bar for a rule, a chip fill or a tag edge. */
const MIN_CONTRAST = 3

/** How far the chip fill is knocked back so a white glyph still reads on top of it. */
const TINT_ALPHA = 0.28

export interface AirlineAccent {
  /** The carrier colour, stepped until it reads on the Ink hero. */
  accent: string
  /** The same colour as a knocked-back fill, for the hero's luggage chip. */
  accentTint: string
}

/**
 * Where the seed hex actually landed in its generated ramp. `generateColors` places
 * the seed by luminance, so a deep brand (Air NZ #008c95) lands at 9 while a mid one
 * (#2196f3) lands at 5 — the reason a fixed shade shipped every airline a neon-ified
 * stand-in for its own colour.
 */
function seedShade(ramp: MantineColorsTuple, seed: string): MantineColorShade {
  const i = ramp.findIndex((c) => c.toLowerCase() === seed.toLowerCase())
  return (i === -1 ? 5 : i) as MantineColorShade
}

/**
 * The seed itself where it reads on `background`, else the nearest lighter shade that
 * does. Near-black brands (Lufthansa #05164d, Delta #003a70) are invisible against the
 * Ink hero, so they step up the ramp until they clear {@link MIN_CONTRAST}. Brands that
 * already clear it — including Spirit's #fff200 — keep their seed, which is the whole
 * point: brand fidelity wherever fidelity is legible.
 */
function shadeMeeting(
  ramp: MantineColorsTuple,
  from: MantineColorShade,
  background: string,
): MantineColorShade {
  for (let i = from; i > 0; i--) {
    if (contrastRatio(ramp[i], background) >= MIN_CONTRAST) return i as MantineColorShade
  }
  return 0
}

/**
 * The accent pair for a carrier. Pass `getAirlineBrand(code)`; memoize on the airline
 * code at the call site, because `ConfirmHero` is memoized and a fresh object every
 * render would defeat it.
 */
export function airlineAccent(brand: AirlineBrand): AirlineAccent {
  const ramp = generateColors(brand.primary)
  const accent = ramp[shadeMeeting(ramp, seedShade(ramp, brand.primary), INK_HERO)]
  return { accent, accentTint: alpha(accent, TINT_ALPHA) }
}
