import { create } from 'zustand'
import { createJSONStorage, persist } from 'zustand/middleware'

import type { ParameterValues } from '../shared/schema/parameterSchema'

/** The user's working state across views, kept for the browser tab (ADR-0011). */
interface WorkspaceState {
  imageId?: string
  algorithmId: string
  parameters: Record<string, ParameterValues>
  detectionRunId?: string
  matchingImageId?: string
  matchingRunId?: string
  benchmarkRunId?: string
  setImage: (imageId: string) => void
  setAlgorithm: (algorithmId: string) => void
  setParameters: (algorithmId: string, values: ParameterValues) => void
  setDetectionRun: (runId: string | undefined) => void
  setMatchingImage: (imageId: string | undefined) => void
  setMatchingRun: (runId: string | undefined) => void
  setBenchmarkRun: (runId: string | undefined) => void
}

export const useWorkspace = create<WorkspaceState>()(
  persist(
    (set) => ({
      algorithmId: 'sift',
      parameters: {},
      setImage: (imageId) => {
        set({
          imageId,
          detectionRunId: undefined,
          matchingRunId: undefined,
          benchmarkRunId: undefined,
        })
      },
      setAlgorithm: (algorithmId) => {
        set({ algorithmId, detectionRunId: undefined })
      },
      setParameters: (algorithmId, values) => {
        set((s) => ({ parameters: { ...s.parameters, [algorithmId]: values } }))
      },
      setDetectionRun: (detectionRunId) => {
        set({ detectionRunId })
      },
      setMatchingImage: (matchingImageId) => {
        set({ matchingImageId, matchingRunId: undefined })
      },
      setMatchingRun: (matchingRunId) => {
        set({ matchingRunId })
      },
      setBenchmarkRun: (benchmarkRunId) => {
        set({ benchmarkRunId })
      },
    }),
    { name: 'salient-workspace', storage: createJSONStorage(() => sessionStorage) },
  ),
)
