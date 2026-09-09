import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { AtlSection } from './AtlSection'
import { MantineTestProvider } from '../../test/render'

const options = [{ id: 1, name: 'Safe place' }]

function renderSection() {
  render(
    <MantineTestProvider>
      <AtlSection
        options={options}
        selectedId={1}
        accessNotes=""
        notesRequired={false}
        onToggle={vi.fn()}
        onSelect={vi.fn()}
        onNotesChange={vi.fn()}
        errors={{}}
      />
    </MantineTestProvider>,
  )
}

describe('AtlSection', () => {
  it('states the consent in the first person and accepts the risk of loss', () => {
    renderSection()

    expect(
      screen.getByText(
        "Leave my baggage in a safe place if I'm not available. I accept the risk of loss or damage once it has been left.",
      ),
    ).toBeInTheDocument()
  })

  it('spells out what picking a location means, including hotel reception', () => {
    renderSection()

    expect(screen.getByText('What this means')).toBeInTheDocument()
    expect(
      screen.getByText('Pick a location that is waterproof so your bag stays dry.'),
    ).toBeInTheDocument()
    expect(
      screen.getByText('Pick a location that is out of view of the street.'),
    ).toBeInTheDocument()
    expect(
      screen.getByText('Pick a location that is safe and easy for the driver to reach.'),
    ).toBeInTheDocument()
    expect(
      screen.getByText('If at a motel or hotel, inform reception that your bag is on its way.'),
    ).toBeInTheDocument()
  })

  it('promises a confirmation message and a photo of the bag once left', () => {
    renderSection()

    expect(screen.getByText(/photograph of the bag at that location/i)).toBeInTheDocument()
  })
})
