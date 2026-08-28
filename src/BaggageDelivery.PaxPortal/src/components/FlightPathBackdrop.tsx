import { memo } from 'react'

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

export const FlightPathBackdrop = memo(() => (
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
        <path
            d="M4 214 C 78 206, 168 176, 236 74"
            fill="none"
            stroke={ARC}
            strokeWidth={1.25}
            strokeDasharray="5 8"
            strokeLinecap="round"
            vectorEffect="non-scaling-stroke"
        />
        <circle cx={4} cy={214} r={2.5} fill={NODE}/>

        <Plane x={150} y={186} angle={-22} scale={0.85}/>

        <Plane x={252} y={56} angle={-52} scale={1.7}/>
    </svg>
))
