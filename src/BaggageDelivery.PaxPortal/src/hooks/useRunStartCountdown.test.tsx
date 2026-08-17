import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, renderHook } from '@testing-library/react'
import { formatCountdown, useRunStartCountdown } from './useRunStartCountdown'

const NOW = new Date('2026-08-17T20:00:00.000Z')
const runIn = (ms: number) => new Date(NOW.getTime() + ms).toISOString()

describe('useRunStartCountdown', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(NOW)
  })
  afterEach(() => vi.useRealTimers())

  it('counts down the time left until the run starts', () => {
    const onExpire = vi.fn()
    const { result } = renderHook(() => useRunStartCountdown(runIn(15 * 60_000), onExpire))

    expect(result.current).toBe(15 * 60_000)

    act(() => void vi.advanceTimersByTime(60_000))

    expect(result.current).toBe(14 * 60_000)
    expect(onExpire).not.toHaveBeenCalled()
  })

  it('fires once when the run start passes, and stays at zero', () => {
    const onExpire = vi.fn()
    const { result } = renderHook(() => useRunStartCountdown(runIn(5_000), onExpire))

    act(() => void vi.advanceTimersByTime(10_000))

    expect(onExpire).toHaveBeenCalledTimes(1)
    expect(result.current).toBe(0)

    act(() => void vi.advanceTimersByTime(60_000))

    expect(onExpire).toHaveBeenCalledTimes(1)
  })

  it('fires on the first tick after a backgrounded tab misses its timers', () => {
    const onExpire = vi.fn()
    renderHook(() => useRunStartCountdown(runIn(20 * 60_000), onExpire))

    // A throttled tab fires far fewer ticks than the elapsed wall clock. Counting
    // ticks would leave a departed run selected; measuring Date.now() drops it.
    act(() => {
      vi.setSystemTime(Date.now() + 30 * 60_000)
      vi.advanceTimersByTime(1000)
    })

    expect(onExpire).toHaveBeenCalledTimes(1)
  })

  it('fires immediately when the run has already started', () => {
    const onExpire = vi.fn()
    const { result } = renderHook(() => useRunStartCountdown(runIn(-60_000), onExpire))

    act(() => void vi.advanceTimersByTime(1000))

    expect(result.current).toBe(0)
    expect(onExpire).toHaveBeenCalledTimes(1)
  })

  it('re-arms against a newly chosen run', () => {
    const onExpire = vi.fn()
    const { result, rerender } = renderHook(
      ({ target }: { target: string }) => useRunStartCountdown(target, onExpire),
      { initialProps: { target: runIn(5_000) } },
    )

    act(() => void vi.advanceTimersByTime(10_000))
    expect(onExpire).toHaveBeenCalledTimes(1)

    // The passenger picks a later window: the countdown must come back to life
    // rather than stay parked on the run that just departed.
    rerender({ target: runIn(10_000 + 60_000) })
    act(() => void vi.advanceTimersByTime(1000))

    expect(result.current).toBe(59_000)

    act(() => void vi.advanceTimersByTime(60_000))
    expect(onExpire).toHaveBeenCalledTimes(2)
  })

  it('does nothing without a run to count towards', () => {
    const onExpire = vi.fn()
    const { result } = renderHook(() => useRunStartCountdown(undefined, onExpire))

    act(() => void vi.advanceTimersByTime(60 * 60_000))

    expect(result.current).toBe(0)
    expect(onExpire).not.toHaveBeenCalled()
  })

  it('does not keep ticking after unmount', () => {
    const onExpire = vi.fn()
    const { unmount } = renderHook(() => useRunStartCountdown(runIn(5_000), onExpire))

    unmount()
    act(() => void vi.advanceTimersByTime(60_000))

    expect(onExpire).not.toHaveBeenCalled()
  })
})

describe('formatCountdown', () => {
  it.each([
    // Runs on a later business day are a day or more out, so the countdown has to
    // read as a date-scale wait rather than a four-digit minute count.
    [50 * 60 * 60_000, '2d 02h'],
    [24 * 60 * 60_000, '1d 00h'],
    [(2 * 60 + 5) * 60_000 + 9_000, '2:05:09'],
    [60 * 60_000, '1:00:00'],
    [59 * 60_000 + 59_000, '59:59'],
    [59_400, '1:00'],
    [9_000, '0:09'],
    [0, '0:00'],
    [-5_000, '0:00'],
  ])('renders %ims as %s', (ms, expected) => {
    expect(formatCountdown(ms)).toBe(expected)
  })
})
