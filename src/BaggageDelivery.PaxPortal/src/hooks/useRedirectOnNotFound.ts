import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'

export function useRedirectOnNotFound(error: unknown) {
  const navigate = useNavigate()

  useEffect(() => {
    if (error && (error as { normalisedKind?: string })?.normalisedKind === 'not_found') {
      navigate('/expired', { replace: true })
    }
  }, [error, navigate])
}
