import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'

// The pax API client tags 404 responses with `normalisedKind: 'not_found'`
// (see api/client.ts). When a booking/tracking query fails that way the URL
// token is invalid or expired, so send the passenger to the /expired screen.
export function useRedirectOnNotFound(error: unknown) {
  const navigate = useNavigate()

  useEffect(() => {
    if (error && (error as { normalisedKind?: string })?.normalisedKind === 'not_found') {
      navigate('/expired', { replace: true })
    }
  }, [error, navigate])
}
