import { useEffect, useRef, useState } from 'react'

export const SLOT_HOLD_MS = 10 * 60 * 1000

/**
 * Counts a delivery-window hold down from ten minutes, calls `onExpire` when it
 * runs out, and starts again.
 *
 * The elapsed time is measured from a `Date.now()` stamp rather than by counting
 * ticks: a backgrounded mobile tab has its timers throttled to once a minute or
 * slower, so a tick counter would drift and the hold would outlive the runs it is
 * holding — which is the exact failure the warning exists to prevent. Measuring
 * wall-clock means a phone that comes back after twenty minutes expires
 * immediately on the next tick.
 */
export function useSlotHoldCountdown(onExpire: () => void, holdMs: number = SLOT_HOLD_MS) {
  const [remainingMs, setRemainingMs] = useState(holdMs)
  const startedAt = useRef(0)
  // Kept in a ref so a caller passing an inline closure doesn't restart the hold
  // on every render.
  const expire = useRef(onExpire)

  useEffect(() => {
    expire.current = onExpire
  }, [onExpire])

  useEffect(() => {
    startedAt.current = Date.now()

    const id = setInterval(() => {
      const left = holdMs - (Date.now() - startedAt.current)
      if (left > 0) {
        setRemainingMs(left)
        return
      }

      startedAt.current = Date.now()
      setRemainingMs(holdMs)
      expire.current()
    }, 1000)

    return () => clearInterval(id)
  }, [holdMs])

  return remainingMs
}

/** `9:59`, counting whole seconds down. */
export function formatCountdown(remainingMs: number): string {
  const totalSeconds = Math.max(0, Math.ceil(remainingMs / 1000))
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes}:${String(seconds).padStart(2, '0')}`
}
