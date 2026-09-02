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

  it('keeps the SMS hint copy alongside the in-page CTA', () => {
    renderConfirmed()

    expect(
      screen.getByText(/we'll also text you when our driver is on the way/i),
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

  it('greys out tracking until a courier is actually under way', async () => {
    renderConfirmed({ trackingAvailable: false })

    expect(screen.queryByRole('link', { name: /track your delivery/i })).not.toBeInTheDocument()

    const button = screen.getByRole('button', { name: /track your delivery/i })
    expect(button).toBeDisabled()

    expect(
      screen.getByText(/tracking will be available once your delivery starts/i),
    ).toBeInTheDocument()
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
