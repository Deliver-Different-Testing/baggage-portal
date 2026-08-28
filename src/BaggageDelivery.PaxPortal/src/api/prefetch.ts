import { queryClient } from './queryClient'
import { getBooking, getTimeslots, getTracking } from './pax'

const PAX_ROUTE = /^\/(c|t)\/([^/]+)/

export function prefetchRouteData(pathname: string): Promise<void> {
  const match = PAX_ROUTE.exec(pathname)
  if (!match) return Promise.resolve()

  const id = decodeURIComponent(match[2])

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
