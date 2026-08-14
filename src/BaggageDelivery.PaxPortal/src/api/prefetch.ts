import { queryClient } from './queryClient'
import { getBooking, getTimeslots, getTracking } from './pax'

// Both passenger pages are lazy route chunks, so without this the booking request
// can't start until the chunk has downloaded, parsed and mounted — two serial
// round-trips on a phone before any data is in flight. The entry script knows the
// token from the URL, so it starts the fetch in parallel with the chunk. The keys
// below must match the useQuery keys in PaxMobile/Tracking or the work is wasted.
const PAX_ROUTE = /^\/(c|t)\/([^/]+)/

export function prefetchRouteData(pathname: string): Promise<void> {
  const match = PAX_ROUTE.exec(pathname)
  if (!match) return Promise.resolve()

  // react-router decodes the param before it reaches useQuery's key.
  const id = decodeURIComponent(match[2])

  // prefetchQuery resolves rather than rejecting on failure — a dead token has to
  // surface through the page's own query (which drives the /expired redirect), not
  // as an unhandled rejection from the entry script.
  const queries =
    match[1] === 'c'
      ? [
          queryClient.prefetchQuery({
            queryKey: ['pax', 'booking', id],
            queryFn: () => getBooking(id),
          }),
          queryClient.prefetchQuery({
            queryKey: ['pax', 'timeslots', id],
            queryFn: () => getTimeslots(id),
          }),
        ]
      : [
          queryClient.prefetchQuery({
            queryKey: ['pax', 'tracking', id],
            queryFn: () => getTracking(id),
          }),
        ]

  return Promise.all(queries).then(() => undefined)
}
