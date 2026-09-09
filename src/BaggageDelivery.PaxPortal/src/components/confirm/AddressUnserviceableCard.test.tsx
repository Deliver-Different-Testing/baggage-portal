import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AddressUnserviceableCard } from './AddressUnserviceableCard'
import { MantineTestProvider } from '../../test/render'
import { summary } from '../../test/paxFixtures'

function renderCard(props: Partial<Parameters<typeof AddressUnserviceableCard>[0]> = {}) {
  const onRequestHelp = vi.fn()
  render(
    <MantineTestProvider>
      <AddressUnserviceableCard
        address={{ ...summary.deliveryAddress, line5: 'Haast', line7: '7886' }}
        airlineLabel="Test Air"
        supportPhone="0800 267 5494"
        requested={false}
        submitting={false}
        onRequestHelp={onRequestHelp}
        {...props}
      />
    </MantineTestProvider>,
  )
  return { onRequestHelp }
}

describe('AddressUnserviceableCard', () => {
  it('tells the passenger we cannot deliver there', () => {
    renderCard()

    expect(screen.getByText(/we cannot deliver there/i)).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent(/haast/i)
  })

  it('offers the airline-contact button naming the airline', () => {
    renderCard()

    expect(
      screen.getByRole('button', { name: /ask test air to contact me/i }),
    ).toBeInTheDocument()
  })

  it('raises the request when the button is pressed', async () => {
    const user = userEvent.setup()
    const { onRequestHelp } = renderCard()

    await user.click(screen.getByRole('button', { name: /ask test air to contact me/i }))

    expect(onRequestHelp).toHaveBeenCalledOnce()
  })

  it('confirms the request and drops the button once it has been sent', () => {
    renderCard({ requested: true })

    expect(screen.getByText(/we've asked test air to contact you/i)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /ask test air/i })).not.toBeInTheDocument()
  })

  it('still offers the support phone number as a fallback', () => {
    renderCard()

    expect(screen.getByRole('link', { name: '0800 267 5494' })).toHaveAttribute(
      'href',
      'tel:08002675494',
    )
  })

  it('omits the phone line when no support number is configured', () => {
    renderCard({ supportPhone: '' })

    expect(screen.queryByRole('link')).not.toBeInTheDocument()
  })
})
