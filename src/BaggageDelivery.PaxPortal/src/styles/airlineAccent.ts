import { generateColors } from '@mantine/colors-generator'
import { alpha, type MantineColorShade, type MantineColorsTuple } from '@mantine/core'
import { contrastRatio } from './md3'
import type { AirlineBrand } from './airlineBranding'

const INK_HERO = '#0d0c2c'

const MIN_CONTRAST = 3

const TINT_ALPHA = 0.28

export interface AirlineAccent {
  accent: string
  accentTint: string
}

function seedShade(ramp: MantineColorsTuple, seed: string): MantineColorShade {
  const i = ramp.findIndex((c) => c.toLowerCase() === seed.toLowerCase())
  return (i === -1 ? 5 : i) as MantineColorShade
}

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

export function airlineAccent(brand: AirlineBrand): AirlineAccent {
  const ramp = generateColors(brand.primary)
  const accent = ramp[shadeMeeting(ramp, seedShade(ramp, brand.primary), INK_HERO)]
  return { accent, accentTint: alpha(accent, TINT_ALPHA) }
}
