import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router'

import { DetectorPage } from '../features/detector/DetectorPage'
import { MatchingPage } from '../features/matching/MatchingPage'
import { PipelinePage } from '../features/pipeline/PipelinePage'
import { RobustnessPage } from '../features/robustness/RobustnessPage'
import { AppShell } from './AppShell'

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false } },
})

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          <Route element={<AppShell />}>
            <Route index element={<Navigate to="/detector" replace />} />
            <Route path="/detector" element={<DetectorPage />} />
            <Route path="/pipeline" element={<PipelinePage />} />
            <Route path="/matching" element={<MatchingPage />} />
            <Route path="/robustness" element={<RobustnessPage />} />
            <Route path="*" element={<Navigate to="/detector" replace />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  )
}
