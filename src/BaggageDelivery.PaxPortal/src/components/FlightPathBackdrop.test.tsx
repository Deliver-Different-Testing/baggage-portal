import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { FlightPathBackdrop } from './FlightPathBackdrop'
import { MantineTestProvider } from '../test/render'

function renderBackdrop() {
  return render(
    <MantineTestProvider>
      <FlightPathBackdrop />
    </MantineTestProvider>,
  )
}

describe('FlightPathBackdrop', () => {
  it('stays out of the accessibility tree and out of the way of taps', () => {
    // Scenery, not content: it must not be announced, focusable, or clickable over
    // the hero's own controls.
    const { container } = renderBackdrop()
    const svg = container.querySelector('svg') as SVGElement

    expect(svg).toHaveAttribute('aria-hidden', 'true')
    expect(svg).toHaveAttribute('focusable', 'false')
    expect(svg).toHaveStyle({ pointerEvents: 'none' })
    expect(screen.queryByRole('img')).not.toBeInTheDocument()
  })

  it('sits behind the hero content rather than over it', () => {
    const { container } = renderBackdrop()
    const svg = container.querySelector('svg') as SVGElement

    expect(svg).toHaveStyle({ position: 'absolute', zIndex: '0' })
  })

  it('keeps every mark inside the visible-but-behind band', () => {
    // Two failure modes, and the first cut hit one of them: at 6–9% white on Ink it
    // was invisible on a real screen, which is not "subtle", it is "missing". Past
    // roughly a quarter it stops being scenery and starts competing with the
    // headline. Both bounds are asserted so a future nudge can't drift out either
    // side without a deliberate change here.
    const { container } = renderBackdrop()
    const alphas = [...container.querySelectorAll('[stroke], [fill]')]
      .flatMap((el) => [el.getAttribute('stroke'), el.getAttribute('fill')])
      .filter((v): v is string => !!v && v.startsWith('rgba'))
      .map((v) => Number(v.split(',')[3].replace(')', '')))

    expect(alphas.length).toBeGreaterThan(0)
    for (const alpha of alphas) {
      expect(alpha).toBeGreaterThanOrEqual(0.12)
      expect(alpha).toBeLessThanOrEqual(0.25)
    }
  })

  it('draws the aircraft on the brand icon stroke, whatever the artwork scale', () => {
    // The planes are scaled per-arc for depth; non-scaling-stroke is what stops the
    // nearest one rendering at a heavier weight than every icon in the UI.
    const { container } = renderBackdrop()
    const planes = [...container.querySelectorAll('path[vector-effect="non-scaling-stroke"]')]

    expect(planes.length).toBeGreaterThan(0)
    for (const p of planes) expect(p).toHaveAttribute('stroke-width', '1.25')
  })

  it('is animation-free', () => {
    // The page spends its motion budget on the card stagger and the tracking pulse.
    const { container } = renderBackdrop()

    expect(container.querySelector('animate, animateTransform, animateMotion')).toBeNull()
  })
})
