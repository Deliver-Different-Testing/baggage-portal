import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MantineTestProvider } from '../test/render'
import { DeliveryDocket, ExtraDeliveryInfo } from './DeliveryDocket'
import type { AddressDto } from '../api/client'

const address: AddressDto = {
  line3: '123',
  line4: 'Test St',
  line5: 'Suburb',
  line6: 'Auckland',
  line7: '1010',
  country: 'NZ',
}

type DocketProps = Partial<Parameters<typeof DeliveryDocket>[0]>

function renderDocket(overrides: DocketProps = {}) {
  return render(
    <MantineTestProvider>
      <DeliveryDocket
        address={address}
        passengerName="Test Passenger"
        passengerPhone="+64211234567"
        passengerEmail="test@example.com"
        atlOption={{ id: 7, name: 'Safe Place' }}
        accessNotes="Behind the blue bin"
        fileReference="AKLNZ12345"
        {...overrides}
      />
    </MantineTestProvider>,
  )
}

describe('DeliveryDocket', () => {
  it('lists the address the bag is going to', () => {
    renderDocket()

    expect(screen.getByText('Deliver to')).toBeInTheDocument()
    expect(screen.getByText('123 Test St')).toBeInTheDocument()
    expect(screen.getByText('Suburb, Auckland 1010')).toBeInTheDocument()
    expect(screen.getByText('NZ')).toBeInTheDocument()
  })

  it('lists who the driver should contact', () => {
    renderDocket()

    expect(screen.getByText('Contact')).toBeInTheDocument()
    expect(screen.getByText('Test Passenger')).toBeInTheDocument()
    expect(screen.getByText('+64211234567')).toBeInTheDocument()
    expect(screen.getByText('test@example.com')).toBeInTheDocument()
  })

  it('names the authority to leave option and quotes the access notes', () => {
    renderDocket()

    expect(screen.getByText('Authority to leave')).toBeInTheDocument()
    expect(screen.getByText('Safe Place')).toBeInTheDocument()
    expect(screen.getByText(/behind the blue bin/i)).toBeInTheDocument()
  })

  it('spells out that nobody may leave the bag when there is no authority', () => {
    renderDocket({ atlOption: undefined, accessNotes: '' })

    expect(screen.getByText('Not authorised')).toBeInTheDocument()
    expect(screen.getByText(/someone will need to be there to take the bag/i)).toBeInTheDocument()
  })

  it('keeps the access notes off the docket when no authority was given', () => {
    renderDocket({ atlOption: undefined, accessNotes: 'Behind the blue bin' })

    expect(screen.queryByText(/behind the blue bin/i)).not.toBeInTheDocument()
  })

  it('reads the file reference back with the delivery details', () => {
    renderDocket()

    expect(screen.getByText(/^file reference$/i)).toBeInTheDocument()
    expect(screen.getByText('AKLNZ12345')).toBeInTheDocument()
  })

  it('omits the file reference block when the job carries none', () => {
    renderDocket({ fileReference: undefined })

    expect(screen.queryByText(/^file reference$/i)).not.toBeInTheDocument()
  })

  it('closes with the file reference, after the authority to leave', () => {
    renderDocket()

    const atl = screen.getByText('Authority to leave')
    const reference = screen.getByText(/^file reference$/i)

    expect(atl.compareDocumentPosition(reference)).toBe(Node.DOCUMENT_POSITION_FOLLOWING)
  })

  it('shows extra delivery information alongside the address', () => {
    renderDocket({ address: { ...address, line2: 'Apartment 4B, ring the buzzer' } })

    expect(screen.getByText('Apartment, unit or suite')).toBeInTheDocument()
    expect(screen.getByText('Apartment 4B, ring the buzzer')).toBeInTheDocument()
  })

  it('leaves no empty extra-information block when the booking has none', () => {
    renderDocket()

    expect(screen.queryByText(/apartment, unit or suite/i)).not.toBeInTheDocument()
  })
})

describe('ExtraDeliveryInfo', () => {
  it('renders nothing for blank values', () => {
    render(
      <MantineTestProvider>
        <ExtraDeliveryInfo value="   " />
      </MantineTestProvider>,
    )

    expect(screen.queryByText(/apartment, unit or suite/i)).not.toBeInTheDocument()
  })

  it('trims the value it shows', () => {
    render(
      <MantineTestProvider>
        <ExtraDeliveryInfo value="  Gate code 1234  " />
      </MantineTestProvider>,
    )

    expect(screen.getByText('Gate code 1234')).toBeInTheDocument()
  })
})
