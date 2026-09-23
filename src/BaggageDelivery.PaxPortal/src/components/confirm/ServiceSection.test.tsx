import { describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ServiceSection } from './ServiceSection'
import { MantineTestProvider } from '../../test/render'
import type { AvailableService } from '../../api/client'

const economyRun: AvailableService = {
  jobTypeId: 37,
  scheduleId: null,
  name: 'Economy Run',
  description: 'Delivered on our next scheduled run',
  bookDateUtc: null,
  durationMinutes: 180,
  isScheduled: false,
}

const scheduled: AvailableService = {
  jobTypeId: 4037,
  scheduleId: 4,
  name: 'Christchurch PM run',
  description: null,
  bookDateUtc: '2026-09-11T02:00:00Z',
  durationMinutes: 120,
  isScheduled: true,
}

function renderSection(props: Partial<Parameters<typeof ServiceSection>[0]> = {}) {
  const onSelect = vi.fn()
  const onRetry = vi.fn()
  render(
    <MantineTestProvider>
      <ServiceSection
        services={[economyRun, scheduled]}
        selectedId={null}
        loading={false}
        error={false}
        onSelect={onSelect}
        onRetry={onRetry}
        {...props}
      />
    </MantineTestProvider>,
  )
  return { onSelect, onRetry }
}

describe('ServiceSection', () => {
  it('lists every offered service as a radio', () => {
    renderSection()

    const group = screen.getByRole('radiogroup', { name: /delivery service/i })
    expect(within(group).getAllByRole('radio')).toHaveLength(2)
    expect(within(group).getByRole('radio', { name: /economy run/i })).toBeInTheDocument()
  })

  it('shows the description when the service has one', () => {
    renderSection()

    expect(screen.getByText('Delivered on our next scheduled run')).toBeInTheDocument()
  })

  it('reports the chosen service by its job type id', async () => {
    const user = userEvent.setup()
    const { onSelect } = renderSection()

    await user.click(screen.getByRole('radio', { name: /christchurch pm run/i }))

    expect(onSelect).toHaveBeenCalledWith(4037)
  })

  it('marks the selected service as checked', () => {
    renderSection({ selectedId: 37 })

    expect(screen.getByRole('radio', { name: /economy run/i })).toBeChecked()
    expect(screen.getByRole('radio', { name: /christchurch pm run/i })).not.toBeChecked()
  })

  it('moves the choice with the arrow keys', async () => {
    const user = userEvent.setup()
    const { onSelect } = renderSection({ selectedId: 37 })

    await user.click(screen.getByRole('radio', { name: /economy run/i }))
    onSelect.mockClear()
    await user.keyboard('{ArrowDown}')

    expect(onSelect).toHaveBeenCalledWith(4037)
  })

  it('says it is checking while the lookup is in flight', () => {
    renderSection({ loading: true, services: [] })

    expect(screen.getByText(/checking what we can deliver/i)).toBeInTheDocument()
    expect(screen.queryByRole('radiogroup')).not.toBeInTheDocument()
  })

  it('offers a retry when the lookup failed', async () => {
    const user = userEvent.setup()
    const { onRetry } = renderSection({ error: true, services: [] })

    await user.click(screen.getByRole('button', { name: /try again/i }))

    expect(onRetry).toHaveBeenCalledOnce()
  })

  it('shows the validation message when one is supplied', () => {
    renderSection({ fieldError: 'Please choose a delivery service for your new address.' })

    expect(screen.getByText(/please choose a delivery service/i)).toBeInTheDocument()
  })
})
