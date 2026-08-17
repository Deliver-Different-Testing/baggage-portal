import { useEffect, useRef, useState } from 'react'

function remainingUntil(targetUtc: string | undefined): number {
  if (!targetUtc) return 0
  const target = Date.parse(targetUtc)
  if (Number.isNaN(target)) return 0
  return Math.max(0, target - Date.now())
}

/**
 * Counts the time left until the chosen run departs, and calls `onExpire` once
 * when it does.
 *
 * The remainder is measured against a wall-clock `Date.now()` rather than by
 * counting ticks: a backgrounded mobile tab has its timers throttled to once a
 * minute or slower, so a tick counter would drift and the page would keep
 * offering a run that has already left — the exact failure the countdown exists
 * to prevent. Measuring wall-clock means a phone that comes back after twenty
 * minutes drops the departed run on its next tick.
 */
export function useRunStartCountdown(targetUtc: string | undefined, onExpire: () => void) {
  const [remainingMs, setRemainingMs] = useState(() => remainingUntil(targetUtc))
  const [armedFor, setArmedFor] = useState(targetUtc)
  // Kept in a ref so a caller passing an inline closure doesn't restart the
  // countdown on every render.
  const expire = useRef(onExpire)

  useEffect(() => {
    expire.current = onExpire
  }, [onExpire])

  // Re-arm during render rather than in an effect: a new run is a new deadline,
  // and the passenger should never see one frame of the old one's remainder
  // against the window they just picked.
  if (armedFor !== targetUtc) {
    setArmedFor(targetUtc)
    setRemainingMs(remainingUntil(targetUtc))
  }

  useEffect(() => {
    if (!targetUtc) return

    // Local to this run's interval, so picking a different window arms a fresh
    // one rather than inheriting a deadline that has already fired.
    let fired = false

    const id = setInterval(() => {
      const left = remainingUntil(targetUtc)
      setRemainingMs(left)
      if (left > 0 || fired) return

      fired = true
      expire.current()
    }, 1000)

    return () => clearInterval(id)
  }, [targetUtc])

  return remainingMs
}

/**
 * `9:59` under an hour, `2:05:09` above it, `1d 04h` from a day out — runs on a
 * later business day are far enough away that a four-digit minute count stops
 * reading as a time at all.
 */
export function formatCountdown(remainingMs: number): string {
  const totalSeconds = Math.max(0, Math.ceil(remainingMs / 1000))
  const pad = (n: number) => String(n).padStart(2, '0')

  if (totalSeconds >= 86_400) {
    return `${Math.floor(totalSeconds / 86_400)}d ${pad(Math.floor((totalSeconds % 86_400) / 3600))}h`
  }

  const minutes = Math.floor((totalSeconds % 3600) / 60)
  const seconds = totalSeconds % 60
  if (totalSeconds >= 3600) {
    return `${Math.floor(totalSeconds / 3600)}:${pad(minutes)}:${pad(seconds)}`
  }

  return `${minutes}:${pad(seconds)}`
}
