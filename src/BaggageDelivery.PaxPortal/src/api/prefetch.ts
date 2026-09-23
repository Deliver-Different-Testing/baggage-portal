import { queryClient } from './queryClient'
import { warmAntiforgeryToken } from './client'
import { getBooking, getTimeslots } from './pax'

const PAX_ROUTE = /^\/c\/([^/]+)/

export function prefetchRouteData(pathname: string): Promise<void> {
  const match = PAX_ROUTE.exec(pathname)
  if (!match) return Promise.resolve()

  const id = decodeURIComponent(match[1])

  const booking = queryClient
    .fetchQuery({ queryKey: ['pax', 'booking', id], queryFn: () => getBooking(id) })
    .catch(() => null)

  return Promise.all([
    warmAntiforgeryToken(),
    booking.then((data) =>
      data && !data.confirmation
        ? queryClient.prefetchQuery({
            queryKey: ['pax', 'timeslots', id],
            queryFn: () => getTimeslots(id),
          })
        : undefined,
    ),
  ]).then(() => undefined)
}
