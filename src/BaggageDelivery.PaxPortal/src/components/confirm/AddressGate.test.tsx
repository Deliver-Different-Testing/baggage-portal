import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AddressGate } from './AddressGate'
import { MantineTestProvider } from '../../test/render'
import { summary } from '../../test/paxFixtures'

function renderGate(props: Partial<Parameters<typeof AddressGate>[0]> = {}) {
  const onChange = vi.fn()
  const onToggleEdit = vi.fn()
  render(
    <MantineTestProvider>
      <AddressGate
        address={summary.deliveryAddress}
        confirmed={false}
        editing={false}
        onChange={onChange}
        onToggleEdit={onToggleEdit}
        {...props}
      />
    </MantineTestProvider>,
  )
  return { onChange, onToggleEdit }
}

const tick = () => screen.getByRole('checkbox', { name: /this address is correct/i })

describe('AddressGate', () => {
  it('reads the saved address back a line at a time', () => {
    renderGate()

    expect(screen.getByText('123 Test St')).toBeInTheDocument()
    expect(screen.getByText('Suburb, Auckland 1010')).toBeInTheDocument()
    expect(screen.getByText('NZ')).toBeInTheDocument()
  })

  it('names the extra delivery information rather than burying it in the street line', () => {
    renderGate({
      address: { ...summary.deliveryAddress, line2: 'Gate code 1234' },
    })

    expect(screen.getByText('Gate code 1234')).toBeInTheDocument()
    expect(screen.getByText('123 Test St')).toBeInTheDocument()
  })

  it('holds the address back while the fields are open, so it is not stated twice', () => {
    renderGate({ editing: true })

    expect(screen.queryByText('123 Test St')).not.toBeInTheDocument()
    expect(tick()).toBeInTheDocument()
  })

  it('reports the tick to the caller', async () => {
    const user = userEvent.setup()
    const { onChange } = renderGate()

    await user.click(tick())

    expect(onChange).toHaveBeenCalledWith(true)
  })

  it('asks to edit while closed, and to finish while open', async () => {
    const user = userEvent.setup()
    const { onToggleEdit } = renderGate()

    await user.click(screen.getByRole('button', { name: /^edit$/i }))

    expect(onToggleEdit).toHaveBeenCalledOnce()
  })

  it('names the toggle Done once the fields are open', () => {
    renderGate({ editing: true })

    expect(screen.getByRole('button', { name: /^done$/i })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^edit$/i })).not.toBeInTheDocument()
  })

  it('carries the validation message on the tick itself', () => {
    renderGate({ error: 'Please confirm your delivery address is correct.' })

    expect(tick()).toHaveAccessibleDescription(
      /please confirm your delivery address is correct/i,
    )
  })
})
