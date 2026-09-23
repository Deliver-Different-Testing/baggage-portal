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
    const { container } = renderBackdrop()
    const planes = [...container.querySelectorAll('path[vector-effect="non-scaling-stroke"]')]

    expect(planes.length).toBeGreaterThan(0)
    for (const p of planes) expect(p).toHaveAttribute('stroke-width', '1.25')
  })

  it('is animation-free', () => {
    const { container } = renderBackdrop()

    expect(container.querySelector('animate, animateTransform, animateMotion')).toBeNull()
  })
})
