import { beforeEach, describe, expect, it, vi } from 'vitest'
import { renderHook } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { useRedirectOnNotFound } from './useRedirectOnNotFound'

const navigateMock = vi.fn()
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>()
  return { ...actual, useNavigate: () => navigateMock }
})

describe('useRedirectOnNotFound', () => {
  beforeEach(() => navigateMock.mockClear())

  it('redirects to /expired when the error is a normalised not_found', () => {
    renderHook(() => useRedirectOnNotFound({ normalisedKind: 'not_found' }), {
      wrapper: MemoryRouter,
    })
    expect(navigateMock).toHaveBeenCalledWith('/expired', { replace: true })
  })

  it('does nothing when there is no error', () => {
    renderHook(() => useRedirectOnNotFound(null), { wrapper: MemoryRouter })
    expect(navigateMock).not.toHaveBeenCalled()
  })

  it('does nothing for an error that is not a not_found', () => {
    renderHook(() => useRedirectOnNotFound({ normalisedKind: 'server_error' }), {
      wrapper: MemoryRouter,
    })
    expect(navigateMock).not.toHaveBeenCalled()
  })
})
