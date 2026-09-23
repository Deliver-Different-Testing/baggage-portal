import { useEffect, useRef, useState } from 'react'

function remainingUntil(targetUtc: string | undefined): number {
  if (!targetUtc) return 0
  const target = Date.parse(targetUtc)
  if (Number.isNaN(target)) return 0
  return Math.max(0, target - Date.now())
}

const URGENT_MS = 10 * 60_000

export interface RunCountdown {
  label: string
  urgent: boolean
}

function countdownAt(targetUtc: string | undefined): RunCountdown {
  const remaining = remainingUntil(targetUtc)
  return { label: formatCountdown(remaining), urgent: remaining <= URGENT_MS }
}

export function useRunStartCountdown(
  targetUtc: string | undefined,
  onExpire: () => void,
): RunCountdown {
  const [countdown, setCountdown] = useState(() => countdownAt(targetUtc))
  const [armedFor, setArmedFor] = useState(targetUtc)
  const expire = useRef(onExpire)

  useEffect(() => {
    expire.current = onExpire
  }, [onExpire])

  if (armedFor !== targetUtc) {
    setArmedFor(targetUtc)
    setCountdown(countdownAt(targetUtc))
  }

  useEffect(() => {
    if (!targetUtc) return

    let fired = false

    const id = setInterval(() => {
      const left = remainingUntil(targetUtc)
      const next = { label: formatCountdown(left), urgent: left <= URGENT_MS }
      setCountdown((prev) =>
        prev.label === next.label && prev.urgent === next.urgent ? prev : next,
      )
      if (left > 0 || fired) return

      fired = true
      expire.current()
    }, 1000)

    return () => clearInterval(id)
  }, [targetUtc])

  return countdown
}

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
