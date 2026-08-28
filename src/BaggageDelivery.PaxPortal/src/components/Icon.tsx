/**
 * Brand icon set. Per the DFRNT icon dev-spec, transport/logistics glyphs come from
 * Tabler and generic UI chrome from Lucide, both rendered at stroke 1.25 (their
 * defaults of 2 are wrong for the brand). `size` defaults to the spec's 36px but is
 * overridable per call site — dense passenger UI (chips, inputs, timeline bullets)
 * passes a smaller size while keeping the brand stroke.
 */
import type {ComponentType} from 'react'
// Deep-path imports, not the package barrels: `@tabler/icons-react` re-exports
// 6169 icons and Rolldown eagerly resolves every entry (build-time [LARGE_BARREL_MODULES]
// warning). Importing each glyph's own module keeps the barrel out of the graph.
// Types for these subpaths come from src/types/icons.d.ts.
import IconLuggage from '@tabler/icons-react/dist/esm/icons/IconLuggage.mjs'
import IconTruckDelivery from '@tabler/icons-react/dist/esm/icons/IconTruckDelivery.mjs'
import IconMapPin from '@tabler/icons-react/dist/esm/icons/IconMapPin.mjs'
import IconClock from '@tabler/icons-react/dist/esm/icons/IconClock.mjs'
import IconHourglass from '@tabler/icons-react/dist/esm/icons/IconHourglass.mjs'
import IconFileInvoice from '@tabler/icons-react/dist/esm/icons/IconFileInvoice.mjs'
import User from 'lucide-react/dist/esm/icons/user.mjs'
import Lock from 'lucide-react/dist/esm/icons/lock.mjs'
import Pencil from 'lucide-react/dist/esm/icons/pencil.mjs'
import Check from 'lucide-react/dist/esm/icons/check.mjs'
import CircleCheck from 'lucide-react/dist/esm/icons/circle-check.mjs'
import ArrowRight from 'lucide-react/dist/esm/icons/arrow-right.mjs'
import Unlink from 'lucide-react/dist/esm/icons/unlink.mjs'
import AlertTriangle from 'lucide-react/dist/esm/icons/alert-triangle.mjs'

const SIZE = 36
const STROKE = 1.25

export interface IconProps {
  size?: number
  color?: string
  className?: string
  'aria-hidden'?: boolean
}

// Tabler icons expose stroke *width* via the `stroke` prop.
function tabler(Cmp: ComponentType<Record<string, unknown>>) {
  return ({size = SIZE, ...rest}: IconProps) => (
      <Cmp size={size} stroke={STROKE} {...rest} />
  )
}

// Lucide icons expose stroke width via `strokeWidth` (`stroke` is the colour).
function lucide(Cmp: ComponentType<Record<string, unknown>>) {
  return ({size = SIZE, ...rest}: IconProps) => (
      <Cmp size={size} strokeWidth={STROKE} {...rest} />
  )
}

// Transport / logistics — Tabler.
export const LuggageIcon = tabler(IconLuggage)
export const TruckIcon = tabler(IconTruckDelivery)
export const MapPinIcon = tabler(IconMapPin)
export const ClockIcon = tabler(IconClock)
export const HourglassIcon = tabler(IconHourglass)
// The docket the review step reads back — a delivery record, not generic paperwork.
export const DocketIcon = tabler(IconFileInvoice)

// UI chrome — Lucide.
export const UserIcon = lucide(User)
export const LockIcon = lucide(Lock)
export const EditIcon = lucide(Pencil)
export const CheckIcon = lucide(Check)
export const CheckCircleIcon = lucide(CircleCheck)
export const ArrowRightIcon = lucide(ArrowRight)
export const UnlinkIcon = lucide(Unlink)
export const AlertIcon = lucide(AlertTriangle)
