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
    // Six hand-tuned variants had drifted across the hero, the review dialog, the
    // ETA card and the slot rows before this component existed.
    renderIn(<Eyebrow>File reference</Eyebrow>)

    expect(screen.getByText('File reference')).toHaveStyle({
      fontSize: `${tokens.type.eyebrow.fontSize}px`,
      fontWeight: `${tokens.type.eyebrow.fontWeight}`,
      letterSpacing: tokens.type.eyebrow.letterSpacing,
      textTransform: tokens.type.eyebrow.textTransform,
    })
  })

  it('lifts the label off the page grey when it sits on the Ink hero', () => {
    // The page-level dimmed grey is mixed for a light body background and loses
    // its contrast entirely on Ink Blue.
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
    // Round = you touch it, square = it's printed. A window the passenger has
    // already chosen is printed.
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
    // The helpline asks the passenger to read the reference back on its own, so
    // the value has to be selectable without dragging the label along with it.
    renderIn(<PunchedTag label="File reference" value="AKLA2633476" />)

    expect(screen.getByText(/^file reference$/i)).toBeInTheDocument()
    expect(screen.getByText('AKLA2633476')).toBeInTheDocument()
  })

  it('sets the reference in tabular figures so it reads as a printed code', () => {
    renderIn(<PunchedTag label="File reference" value="AKLA2633476" />)

    expect(screen.getByText('AKLA2633476')).toHaveStyle({
      fontVariantNumeric: 'tabular-nums',
    })
  })

  it('spends the airline colour on the punch and the divider only', () => {
    // Airline colour is identity, DFRNT cyan is affordance — the tag's ground and
    // its value stay neutral so the reference keeps its contrast for every carrier.
    const { container } = renderIn(
      <PunchedTag label="File reference" value="REF-42" accent="#008c95" onScrim />,
    )

    const painted = container.querySelectorAll('[style*="#008c95"]')
    expect(painted.length).toBeGreaterThan(0)
    expect(screen.getByText('REF-42')).not.toHaveStyle({ color: '#008c95' })
  })
})
