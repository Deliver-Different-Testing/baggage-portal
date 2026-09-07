import { queryClient } from './queryClient'
import { getBooking, getTimeslots } from './pax'

const PAX_ROUTE = /^\/c\/([^/]+)/

export function prefetchRouteData(pathname: string): Promise<void> {
  const match = PAX_ROUTE.exec(pathname)
  if (!match) return Promise.resolve()

  const id = decodeURIComponent(match[1])

  return Promise.all([
    queryClient.prefetchQuery({
      queryKey: ['pax', 'booking', id],
      queryFn: () => getBooking(id),
    }),
    queryClient.prefetchQuery({
      queryKey: ['pax', 'timeslots', id],
      queryFn: () => getTimeslots(id),
    }),
  ]).then(() => undefined)
}
