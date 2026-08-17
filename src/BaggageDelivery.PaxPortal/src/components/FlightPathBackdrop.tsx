import { memo } from 'react'

/**
 * Ambient scenery for the Ink-Blue hero: a route-map arc with an aircraft climbing
 * off the top-right corner.
 *
 * Scattered plane glyphs would be wallpaper. A flight *path* says something true
 * about why this page exists — the bag and the passenger took different routes, and
 * this is the product that closes the gap. Same vernacular as the route map in the
 * back of an inflight magazine, drawn in the brand's own icon language: the aircraft
 * is the Tabler plane on the same 1.25 stroke as every icon in the UI (held there by
 * `vector-effect`, so scaling the artwork never fattens the line).
 *
 * Anchored to the top-right corner at a fixed size rather than stretched to fill.
 * A full-bleed `slice` viewBox re-crops itself at every aspect ratio, and on a phone
 * it cut the lead plane in half against the left edge — which reads as a mistake
 * rather than as composition. Anchoring puts the artwork in the same place relative
 * to the corner at every width, in the one region the hero leaves quiet: right of
 * the airline line and past the ragged right edge of the headline. Bleeding off the
 * corner is then deliberate, and the hero's `overflow: hidden` does the trimming.
 *
 * Deliberately static. The page already spends its motion budget on the card
 * stagger and the tracking pulse; drifting planes would be one effect too many.
 *
 * White at 14–20% on Ink. The first cut ran at 6–9%, which magnified fine in a
 * screenshot and was invisible on a real screen — an unlit `#0d0c2c` moving to
 * about `#201f3d` is below the threshold most displays and most eyes resolve,
 * especially in dark mode. These values were picked by putting four levels side by
 * side at 1:1: 20% is unmistakably there, and the next step up starts pulling the
 * vapour trail into the headline.
 *
 * Out of the accessibility tree and untouchable, so it can never intercept a tap
 * meant for the hero.
 */

// Tabler `plane`, 24×24, nose pointing right — the same glyph the icon set uses.
const PLANE_PATH =
  'M16 10h4a2 2 0 0 1 0 4h-4l-4 7h-3l2 -7h-4l-2 2h-3l2 -4l-2 -4h3l2 2h4l-2 -7h3l4 7'

const ARC = 'rgba(255,255,255,0.14)'
const NODE = 'rgba(255,255,255,0.18)'
const PLANE = 'rgba(255,255,255,0.2)'

function Plane({ x, y, angle, scale }: { x: number; y: number; angle: number; scale: number }) {
  return (
    <g transform={`translate(${x} ${y}) rotate(${angle}) scale(${scale}) translate(-12 -12)`}>
      <path
        d={PLANE_PATH}
        fill="none"
        stroke={PLANE}
        strokeWidth={1.25}
        strokeLinecap="round"
        strokeLinejoin="round"
        vectorEffect="non-scaling-stroke"
      />
    </g>
  )
}

export const FlightPathBackdrop = memo(function FlightPathBackdrop() {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      width={340}
      height={240}
      viewBox="0 0 340 240"
      style={{
        position: 'absolute',
        top: -24,
        right: -28,
        pointerEvents: 'none',
        zIndex: 0,
      }}
    >
      {/* The sector, climbing away to the corner. Dashes thin out along the way, so
          the near end reads as the one in front. */}
      <path
        d="M4 214 C 78 206, 168 176, 236 74"
        fill="none"
        stroke={ARC}
        strokeWidth={1.25}
        strokeDasharray="5 8"
        strokeLinecap="round"
        vectorEffect="non-scaling-stroke"
      />
      <circle cx={4} cy={214} r={2.5} fill={NODE} />

      {/* Trailing aircraft, further down the path and smaller — depth, not a second
          subject. */}
      <Plane x={150} y={186} angle={-22} scale={0.85} />

      {/* The lead, at the head of the arc and half off the corner. */}
      <Plane x={252} y={56} angle={-52} scale={1.7} />
    </svg>
  )
})
