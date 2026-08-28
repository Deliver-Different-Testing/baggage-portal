import { lazy, Suspense } from 'react'
import { Route, Routes, Navigate } from 'react-router-dom'
import { Center, Loader } from '@mantine/core'
import { TokenExpired } from './pages/TokenExpired'

const PaxMobile = lazy(() =>
  import('./pages/PaxMobile').then((m) => ({ default: m.PaxMobile })),
)
const Tracking = lazy(() =>
  import('./pages/Tracking').then((m) => ({ default: m.Tracking })),
)
const ProcessMap = lazy(() =>
  import('./pages/ProcessMap').then((m) => ({ default: m.ProcessMap })),
)
const DevLanding = lazy(() =>
  import('./pages/DevLanding').then((m) => ({ default: m.DevLanding })),
)

function RouteFallback() {
  return (
    <Center mih="100vh">
      <Loader size="lg" />
    </Center>
  )
}

export function App() {
  return (
    <Suspense fallback={<RouteFallback />}>
      <Routes>
        <Route path="/" element={<DevLanding />} />
        <Route path="/c/:id" element={<PaxMobile />} />
        <Route path="/t/:id" element={<Tracking />} />
        <Route path="/internal/process-map" element={<ProcessMap />} />
        <Route path="/expired" element={<TokenExpired />} />
        <Route path="*" element={<Navigate to="/expired" replace />} />
      </Routes>
    </Suspense>
  )
}
