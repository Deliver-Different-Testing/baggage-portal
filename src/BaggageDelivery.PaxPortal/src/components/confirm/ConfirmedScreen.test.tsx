import { describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { ConfirmedScreen } from './ConfirmedScreen'
import { MantineTestProvider } from '../../test/render'
import { slot, summary } from '../../test/paxFixtures'
import type { BookingSummary } from '../../api/client'


function renderConfirmed(overrides: Partial<BookingSummary> = {}) {
  return render(
    <MemoryRouter>
      <MantineTestProvider>
        <ConfirmedScreen
          summary={{ ...summary, ...overrides }}
          slot={slot}
          address={summary.deliveryAddress}
          passengerName="Test Passenger"
          passengerPhone="+64211234567"
          passengerEmail="test@example.com"
          atlOption={{ id: 7, name: 'Safe Place' }}
          accessNotes="Behind the blue bin"
        />
      </MantineTestProvider>
    </MemoryRouter>,
  )
}

describe('ConfirmedScreen', () => {
  it('tells the passenger the bag comes within the delivery window and a tracking link follows', () => {
    renderConfirmed()

    const steps = screen.getByRole('list')
    expect(steps).toHaveTextContent(
      'We collect your bag and deliver it to your address within the Delivery Window.',
    )
    expect(steps).toHaveTextContent(
      'We will text/email you when your bag is collected from the airport with a tracking link.',
    )
    expect(steps).toHaveTextContent('You can track the driver from the airport to your address.')
    expect(steps).not.toHaveTextContent('The driver calls when they are close.')
  })

  it('points the help line at the job number, the reference support can look up', () => {
    renderConfirmed()

    const help = screen.getByText(/need to change something/i)
    expect(help).toHaveTextContent('quote job number URG-179252')
    expect(help).not.toHaveTextContent('AKLNZ12345')
    expect(within(help).getByRole('link', { name: '0800 267 5494' })).toHaveAttribute(
      'href',
      'tel:08002675494',
    )
  })

  it('stops at the phone number when the job carries no number', () => {
    renderConfirmed({ jobNumber: '' })

    expect(screen.getByText(/need to change something/i)).not.toHaveTextContent('quote')
  })

  it('renders a tracking link pointing at the Despatch tracking page', () => {
    renderConfirmed()

    const link = screen.getByRole('link', { name: /track your delivery/i })
    expect(link).toHaveAttribute('href', 'https://tracking.example.com/#/ENCRYPTED')
  })

  it('greys out tracking when the job has no tracking url', () => {
    renderConfirmed({ trackingUrl: null })

    expect(screen.queryByRole('link', { name: /track your delivery/i })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /track your delivery/i })).toBeDisabled()
  })

  it('says when tracking goes live rather than repeating the next-steps text promise', () => {
    renderConfirmed()

    expect(
      screen.getByText(/tracking goes live once your bag is collected from the airport/i),
    ).toBeInTheDocument()
    expect(
      screen.queryByText(/we'll also text you when our driver is on the way/i),
    ).not.toBeInTheDocument()
  })

  it('says the same thing about tracking whether or not the link is ready yet', () => {
    renderConfirmed({ trackingUrl: null })

    expect(
      screen.getByText(/tracking goes live once your bag is collected from the airport/i),
    ).toBeInTheDocument()
  })

  it('narrates the job number, the file reference and the chosen window', () => {
    renderConfirmed()

    expect(screen.getByText(/^job number$/i)).toBeInTheDocument()
    expect(screen.getByText('URG-179252')).toBeInTheDocument()
    expect(screen.getByText(/^file reference$/i)).toBeInTheDocument()
    expect(screen.getByText('AKLNZ12345')).toBeInTheDocument()
    expect(screen.getByText('Delivery window')).toBeInTheDocument()
    expect(screen.getByText('Today, Wed 10 Jun')).toBeInTheDocument()
    expect(screen.getByText('2:00 PM – 5:00 PM')).toBeInTheDocument()
  })

  it('leads with the job number and closes with the file reference', () => {
    renderConfirmed()

    const jobNumber = screen.getByText('URG-179252')
    const fileReference = screen.getByText('AKLNZ12345')
    const authorityToLeave = screen.getByText('Authority to leave')

    expect(jobNumber.compareDocumentPosition(authorityToLeave) & 4).toBeTruthy()
    expect(authorityToLeave.compareDocumentPosition(fileReference) & 4).toBeTruthy()
  })

  it('drops the job number tag when Despatch has no number for the job', () => {
    renderConfirmed({ jobNumber: '' })

    expect(screen.queryByText(/^job number$/i)).not.toBeInTheDocument()
  })

  it('scrolls back to the top so the passenger lands on the hero', () => {
    const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => {})

    renderConfirmed()

    expect(scrollTo).toHaveBeenCalledWith(0, 0)
    scrollTo.mockRestore()
  })

  it('hands the passenger over to the tracking app before a courier is assigned', async () => {
    renderConfirmed({ trackingAvailable: false })

    const link = screen.getByRole('link', { name: /track your delivery/i })
    expect(link).toHaveAttribute('href', 'https://tracking.example.com/#/ENCRYPTED')

    expect(
      screen.queryByText(/tracking will be available once your delivery starts/i),
    ).not.toBeInTheDocument()
  })

  it('issues a full docket of what was submitted', () => {
    renderConfirmed()

    expect(screen.getByText('Deliver to')).toBeInTheDocument()
    expect(screen.getByText('123 Test St')).toBeInTheDocument()
    expect(screen.getByText('Suburb, Auckland 1010')).toBeInTheDocument()

    expect(screen.getByText('Contact')).toBeInTheDocument()
    expect(screen.getByText('Test Passenger')).toBeInTheDocument()
    expect(screen.getByText('+64211234567')).toBeInTheDocument()
    expect(screen.getByText('test@example.com')).toBeInTheDocument()

    expect(screen.getByText('Authority to leave')).toBeInTheDocument()
    expect(screen.getByText('Safe Place')).toBeInTheDocument()
    expect(screen.getByText(/behind the blue bin/i)).toBeInTheDocument()
  })

  it('reads the extra delivery information back with the address', () => {
    render(
      <MemoryRouter>
        <MantineTestProvider>
          <ConfirmedScreen
            summary={summary}
            slot={slot}
            address={{ ...summary.deliveryAddress, line2: 'Apartment 4B, ring the buzzer' }}
            passengerName="Test Passenger"
            passengerPhone="+64211234567"
            passengerEmail="test@example.com"
            atlOption={undefined}
            accessNotes=""
          />
        </MantineTestProvider>
      </MemoryRouter>,
    )

    expect(screen.getByText('Apartment, unit or suite')).toBeInTheDocument()
    expect(screen.getByText('Apartment 4B, ring the buzzer')).toBeInTheDocument()
  })

  it('leaves no empty extra-information block when the booking has none', () => {
    renderConfirmed()

    expect(screen.queryByText(/apartment, unit or suite/i)).not.toBeInTheDocument()
  })

  it('keeps the Ink hero rather than introducing a green one', () => {
    const { container } = renderConfirmed()

    const hero = container.querySelector('[style*="ink-9"]')
    expect(hero).not.toBeNull()
    expect(container.querySelector('[style*="green-8"]')).toBeNull()
    expect(screen.getByText('Confirmed')).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: /you're all set/i })).toBeInTheDocument()
  })

  it('shows the Powered by Deliver DFRNT footer', () => {
    renderConfirmed()

    expect(screen.getByText(/powered by/i)).toBeInTheDocument()
    expect(screen.getByRole('img', { name: /deliver dfrnt/i })).toBeInTheDocument()
  })

  it('signs off with the attribution alone', () => {
    renderConfirmed()

    const footer = within(screen.getByRole('contentinfo'))
    expect(footer.queryByText('Test Air')).not.toBeInTheDocument()
    expect(footer.queryByRole('link', { name: /call/i })).not.toBeInTheDocument()
    expect(footer.getByRole('img', { name: /deliver dfrnt/i })).toBeInTheDocument()
  })
})
