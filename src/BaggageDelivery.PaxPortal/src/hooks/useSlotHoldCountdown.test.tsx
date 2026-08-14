import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, renderHook } from '@testing-library/react'
import { formatCountdown, SLOT_HOLD_MS, useSlotHoldCountdown } from './useSlotHoldCountdown'

describe('useSlotHoldCountdown', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => vi.useRealTimers())

  it('counts down from ten minutes without expiring early', () => {
    const onExpire = vi.fn()
    const { result } = renderHook(() => useSlotHoldCountdown(onExpire))

    expect(result.current).toBe(SLOT_HOLD_MS)

    act(() => void vi.advanceTimersByTime(60_000))

    expect(result.current).toBe(SLOT_HOLD_MS - 60_000)
    expect(onExpire).not.toHaveBeenCalled()
  })

  it('expires once the hold runs out and starts the next hold', () => {
    const onExpire = vi.fn()
    const { result } = renderHook(() => useSlotHoldCountdown(onExpire))

    act(() => void vi.advanceTimersByTime(SLOT_HOLD_MS))

    expect(onExpire).toHaveBeenCalledTimes(1)
    expect(result.current).toBe(SLOT_HOLD_MS)
  })

  it('expires on the first tick after a backgrounded tab misses its timers', () => {
    const onExpire = vi.fn()
    renderHook(() => useSlotHoldCountdown(onExpire))

    // A throttled tab fires far fewer ticks than the elapsed wall clock. Counting
    // ticks would leave the hold alive; measuring Date.now() expires it.
    act(() => {
      vi.setSystemTime(Date.now() + 20 * 60_000)
      vi.advanceTimersByTime(1000)
    })

    expect(onExpire).toHaveBeenCalledTimes(1)
  })

  it('does not keep ticking after unmount', () => {
    const onExpire = vi.fn()
    const { unmount } = renderHook(() => useSlotHoldCountdown(onExpire))

    unmount()
    act(() => void vi.advanceTimersByTime(SLOT_HOLD_MS * 2))

    expect(onExpire).not.toHaveBeenCalled()
  })
})

describe('formatCountdown', () => {
  it.each([
    [SLOT_HOLD_MS, '10:00'],
    [59_400, '1:00'],
    [9_000, '0:09'],
    [0, '0:00'],
    [-5_000, '0:00'],
  ])('renders %ims as %s', (ms, expected) => {
    expect(formatCountdown(ms)).toBe(expected)
  })
})
