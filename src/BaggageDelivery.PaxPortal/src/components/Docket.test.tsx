import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { DocketTile, Eyebrow, PunchedTag } from './Docket'
import { MantineTestProvider } from '../test/render'
import { tokens } from '../styles/mantineTheme'

function renderIn(ui: React.ReactNode) {
  return render(<MantineTestProvider>{ui}</MantineTestProvider>)
}

describe('Eyebrow', () => {
  it('renders every micro-label to the one spec', () => {
    renderIn(<Eyebrow>Booking reference</Eyebrow>)

    expect(screen.getByText('Booking reference')).toHaveStyle({
      fontSize: `${tokens.type.eyebrow.fontSize}px`,
      fontWeight: `${tokens.type.eyebrow.fontWeight}`,
      letterSpacing: tokens.type.eyebrow.letterSpacing,
      textTransform: tokens.type.eyebrow.textTransform,
    })
  })

  it('lifts the label off the page grey when it sits on the Ink hero', () => {
    renderIn(<Eyebrow tone="onScrim">Baggage tracking</Eyebrow>)

    expect(screen.getByText('Baggage tracking')).toHaveStyle({
      color: 'rgba(255, 255, 255, 0.7)',
    })
  })
})

describe('DocketTile', () => {
  it('stamps a fact at the printed radius, not a touchable one', () => {
    renderIn(
      <DocketTile label="Delivery window">
        <span>2:00 PM – 5:00 PM</span>
      </DocketTile>,
    )

    expect(screen.getByText('2:00 PM – 5:00 PM')).toBeInTheDocument()
    expect(screen.getByText('Delivery window').parentElement).toHaveStyle({
      borderRadius: `${tokens.radius.tile}px`,
    })
  })

  it('drops the box for a plain docket row', () => {
    renderIn(
      <DocketTile label="Contact" variant="plain">
        <span>Test Passenger</span>
      </DocketTile>,
    )

    expect(screen.getByText('Contact').parentElement).not.toHaveStyle({
      borderRadius: `${tokens.radius.tile}px`,
    })
  })
})

describe('PunchedTag', () => {
  it('keeps the label and the value as separate nodes', () => {
    renderIn(<PunchedTag label="Booking reference" value="URG-179252" />)

    expect(screen.getByText(/^booking reference$/i)).toBeInTheDocument()
    expect(screen.getByText('URG-179252')).toBeInTheDocument()
  })

  it('sets the reference in tabular figures so it reads as a printed code', () => {
    renderIn(<PunchedTag label="Booking reference" value="URG-179252" />)

    expect(screen.getByText('URG-179252')).toHaveStyle({
      fontVariantNumeric: 'tabular-nums',
    })
  })

  it('spends the airline colour on the punch and the divider only', () => {
    const { container } = renderIn(
      <PunchedTag label="Booking reference" value="URG-42" accent="#008c95" onScrim />,
    )

    const painted = container.querySelectorAll('[style*="#008c95"]')
    expect(painted.length).toBeGreaterThan(0)
    expect(screen.getByText('URG-42')).not.toHaveStyle({ color: '#008c95' })
  })
})
