import Box from '@mui/material/Box'
import CircularProgress from '@mui/material/CircularProgress'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { lazy, Suspense } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router'

import { AppShell } from './AppShell'

// Views are split into separate chunks; the robustness view in particular brings in the chart library.
const DetectorPage = lazy(() =>
  import('../features/detector/DetectorPage').then((m) => ({ default: m.DetectorPage })),
)
const PipelinePage = lazy(() =>
  import('../features/pipeline/PipelinePage').then((m) => ({ default: m.PipelinePage })),
)
const MatchingPage = lazy(() =>
  import('../features/matching/MatchingPage').then((m) => ({ default: m.MatchingPage })),
)
const RobustnessPage = lazy(() =>
  import('../features/robustness/RobustnessPage').then((m) => ({ default: m.RobustnessPage })),
)

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false } },
})

function Loading() {
  return (
    <Box sx={{ display: 'grid', placeItems: 'center', py: 12 }}>
      <CircularProgress aria-label="Loading view" />
    </Box>
  )
}

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Suspense fallback={<Loading />}>
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
        </Suspense>
      </BrowserRouter>
    </QueryClientProvider>
  )
}
