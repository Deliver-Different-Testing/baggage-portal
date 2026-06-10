import { lazy, Suspense } from 'react'
import { Route, Routes, Navigate } from 'react-router-dom'
import Box from '@mui/material/Box'
import CircularProgress from '@mui/material/CircularProgress'
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

function RouteFallback() {
  return (
    <Box sx={{ minHeight: '100vh', display: 'grid', placeItems: 'center' }}>
      <CircularProgress size={32} />
    </Box>
  )
}

export function App() {
  return (
    <Suspense fallback={<RouteFallback />}>
      <Routes>
        <Route path="/c/:id" element={<PaxMobile />} />
        <Route path="/t/:id" element={<Tracking />} />
        <Route path="/internal/process-map" element={<ProcessMap />} />
        <Route path="/expired" element={<TokenExpired />} />
        <Route path="*" element={<Navigate to="/expired" replace />} />
      </Routes>
    </Suspense>
  )
}
