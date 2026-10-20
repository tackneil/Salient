import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
  api,
  isFinished,
  unwrap,
  uploadImage,
  type BenchmarkResult,
  type CandidateDto,
  type ComparisonResult,
  type DetectionResult,
  type KeypointDetail,
  type MatchingResult,
  type RunInfo,
  type Schemas,
} from './client'

export const queryKeys = {
  algorithms: ['algorithms'] as const,
  image: (id: string) => ['image', id] as const,
  run: (id: string) => ['run', id] as const,
  result: (id: string, kind: string) => ['run', id, 'result', kind] as const,
  keypoint: (id: string, index: number) => ['run', id, 'keypoint', index] as const,
  candidates: (id: string, stage: string) => ['run', id, 'candidates', stage] as const,
}

export function useAlgorithms() {
  return useQuery({
    queryKey: queryKeys.algorithms,
    queryFn: () => unwrap(api.GET('/api/algorithms')),
    staleTime: Infinity,
  })
}

export function useImage(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.image(id ?? ''),
    queryFn: () => unwrap(api.GET('/api/images/{id}', { params: { path: { id: id ?? '' } } })),
    enabled: Boolean(id),
    staleTime: Infinity,
  })
}

/**
 * Run status. Live updates come from SignalR (useRunProgress); polling every 2 s is only a fallback for a dropped
 * connection and stops once the run has finished.
 */
export function useRun(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.run(id ?? ''),
    queryFn: () => unwrap(api.GET('/api/runs/{id}', { params: { path: { id: id ?? '' } } })),
    enabled: Boolean(id),
    refetchInterval: (query) => (isFinished(query.state.data?.status) ? false : 2000),
  })
}

function useRunResult<T>(
  id: string | undefined,
  run: RunInfo | undefined,
  kind: string,
  fetch: (id: string) => Promise<T>,
) {
  return useQuery({
    queryKey: queryKeys.result(id ?? '', kind),
    queryFn: () => fetch(id ?? ''),
    enabled: Boolean(id) && run?.status === 'completed',
    staleTime: Infinity,
  })
}

export const useDetection = (id: string | undefined, run: RunInfo | undefined) =>
  useRunResult<DetectionResult>(id, run, 'detection', (runId) =>
    unwrap(api.GET('/api/runs/{id}/detection', { params: { path: { id: runId } } })),
  )

export const useMatching = (id: string | undefined, run: RunInfo | undefined) =>
  useRunResult<MatchingResult>(id, run, 'matching', (runId) =>
    unwrap(api.GET('/api/runs/{id}/matching', { params: { path: { id: runId } } })),
  )

export const useBenchmark = (id: string | undefined, run: RunInfo | undefined) =>
  useRunResult<BenchmarkResult>(id, run, 'benchmark', (runId) =>
    unwrap(api.GET('/api/runs/{id}/benchmark', { params: { path: { id: runId } } })),
  )

export const useComparison = (id: string | undefined, run: RunInfo | undefined) =>
  useRunResult<ComparisonResult>(id, run, 'comparison', (runId) =>
    unwrap(api.GET('/api/runs/{id}/comparison', { params: { path: { id: runId } } })),
  )

export function useKeypoint(runId: string | undefined, index: number | undefined, enabled = true) {
  return useQuery<KeypointDetail>({
    queryKey: queryKeys.keypoint(runId ?? '', index ?? -1),
    queryFn: () =>
      unwrap(
        api.GET('/api/runs/{id}/keypoints/{index}', {
          params: { path: { id: runId ?? '', index: index ?? 0 } },
        }),
      ),
    enabled: enabled && Boolean(runId) && index !== undefined && index >= 0,
    staleTime: Infinity,
  })
}

export function useCandidates(
  runId: string | undefined,
  stageId: string | undefined,
  enabled = true,
) {
  return useQuery<CandidateDto[]>({
    queryKey: queryKeys.candidates(runId ?? '', stageId ?? ''),
    queryFn: () =>
      unwrap(
        api.GET('/api/runs/{id}/candidates/{stageId}', {
          params: { path: { id: runId ?? '', stageId: stageId ?? '' } },
        }),
      ),
    enabled: enabled && Boolean(runId) && Boolean(stageId),
    staleTime: Infinity,
  })
}

export function useUploadImage() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: uploadImage,
    onSuccess: (image) => client.setQueryData(queryKeys.image(image.id), image),
  })
}

export function useCreateDetection() {
  return useMutation({
    mutationFn: (body: Schemas['DetectionRequest']) => unwrap(api.POST('/api/runs', { body })),
  })
}

export function useCreateMatching() {
  return useMutation({
    mutationFn: (body: Schemas['MatchingRequest']) => unwrap(api.POST('/api/matches', { body })),
  })
}

export function useCreateBenchmark() {
  return useMutation({
    mutationFn: (body: Schemas['BenchmarkRequest']) =>
      unwrap(api.POST('/api/benchmarks', { body })),
  })
}

export function useCreateComparison() {
  return useMutation({
    mutationFn: (body: Schemas['ComparisonRequest']) =>
      unwrap(api.POST('/api/comparisons', { body })),
  })
}

export function useCancelRun() {
  return useMutation({
    mutationFn: async (id: string) => {
      await api.DELETE('/api/runs/{id}', { params: { path: { id } } })
    },
  })
}
