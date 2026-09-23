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

    expect(result.current.label).toBe('15:00')

    act(() => void vi.advanceTimersByTime(60_000))

    expect(result.current.label).toBe('14:00')
    expect(onExpire).not.toHaveBeenCalled()
  })

  it('fires once when the run start passes, and stays at zero', () => {
    const onExpire = vi.fn()
    const { result } = renderHook(() => useRunStartCountdown(runIn(5_000), onExpire))

    act(() => void vi.advanceTimersByTime(10_000))

    expect(onExpire).toHaveBeenCalledTimes(1)
    expect(result.current.label).toBe('0:00')

    act(() => void vi.advanceTimersByTime(60_000))

    expect(onExpire).toHaveBeenCalledTimes(1)
  })

  it('fires on the first tick after a backgrounded tab misses its timers', () => {
    const onExpire = vi.fn()
    renderHook(() => useRunStartCountdown(runIn(20 * 60_000), onExpire))

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

    expect(result.current.label).toBe('0:00')
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

    rerender({ target: runIn(10_000 + 60_000) })
    act(() => void vi.advanceTimersByTime(1000))

    expect(result.current.label).toBe('0:59')

    act(() => void vi.advanceTimersByTime(60_000))
    expect(onExpire).toHaveBeenCalledTimes(2)
  })

  it('does nothing without a run to count towards', () => {
    const onExpire = vi.fn()
    const { result } = renderHook(() => useRunStartCountdown(undefined, onExpire))

    act(() => void vi.advanceTimersByTime(60 * 60_000))

    expect(result.current.label).toBe('0:00')
    expect(onExpire).not.toHaveBeenCalled()
  })

  it('flags the last ten minutes as urgent', () => {
    const onExpire = vi.fn()
    const { result } = renderHook(() => useRunStartCountdown(runIn(11 * 60_000), onExpire))

    expect(result.current.urgent).toBe(false)

    act(() => void vi.advanceTimersByTime(90_000))

    expect(result.current.urgent).toBe(true)
  })

  it('does not re-render while the visible label is unchanged', () => {
    const onExpire = vi.fn()
    let renders = 0
    const { result } = renderHook(() => {
      renders += 1
      return useRunStartCountdown(runIn(50.5 * 60 * 60_000), onExpire)
    })

    expect(result.current.label).toBe('2d 02h')
    const before = renders

    act(() => void vi.advanceTimersByTime(5_000))

    expect(renders).toBe(before)
    expect(result.current.label).toBe('2d 02h')
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
