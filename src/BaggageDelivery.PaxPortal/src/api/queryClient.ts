import { QueryClient } from '@tanstack/react-query'

// Module-level and shared with the entry script, so prefetchRouteData can prime the
// cache before React mounts (see ./prefetch).
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
      networkMode: 'offlineFirst',
    },
    mutations: {
      networkMode: 'online',
    },
  },
})
