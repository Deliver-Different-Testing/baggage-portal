import { Route, Routes, Navigate } from 'react-router-dom'
import { PaxMobile } from './pages/PaxMobile'
import { Tracking } from './pages/Tracking'
import { ProcessMap } from './pages/ProcessMap'
import { TokenExpired } from './pages/TokenExpired'

export function App() {
  return (
    <Routes>
      <Route path="/c/:id" element={<PaxMobile />} />
      <Route path="/t/:id" element={<Tracking />} />
      <Route path="/internal/process-map" element={<ProcessMap />} />
      <Route path="/expired" element={<TokenExpired />} />
      <Route path="*" element={<Navigate to="/expired" replace />} />
    </Routes>
  )
}
