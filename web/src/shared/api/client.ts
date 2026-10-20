import createClient from 'openapi-fetch'

import type { components, paths } from './generated/schema'

/** Generated API types (ADR-0010). */
export type Schemas = components['schemas']

export type AlgorithmInfo = Schemas['AlgorithmInfo']
export type ImageInfo = Schemas['ImageInfo']
export type RunInfo = Schemas['RunInfo']
export type RunCreated = Schemas['RunCreated']
export type RunStatus = Schemas['RunStatus']
export type StageInfo = Schemas['StageInfo']
export type LayerInfo = Schemas['LayerInfo']
export type LayerLegend = Schemas['LayerLegend']
export type KeypointDto = Schemas['KeypointDto']
export type KeypointDetail = Schemas['KeypointDetail']
export type CandidateDto = Schemas['CandidateDto']
export type DetectionResult = Schemas['DetectionResultDto']
export type MatchingResult = Schemas['MatchingResultDto']
export type BenchmarkResult = Schemas['BenchmarkResultDto']
export type ComparisonResult = Schemas['ComparisonResultDto']
export type SweepResult = Schemas['SweepResult']
export type TransformationFamily = Schemas['TransformationFamily']

/** Same-origin client: the Vite dev server proxies /api and /hubs to the backend. */
export const api = createClient<paths>({ baseUrl: '' })

/** An error response from the API, carrying RFC 9457 problem details when available. */
export class ApiError extends Error {
  readonly status: number
  readonly problem: unknown

  constructor(status: number, problem: unknown) {
    super(describe(status, problem))
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

function describe(status: number, problem: unknown): string {
  if (problem && typeof problem === 'object') {
    const p = problem as { detail?: unknown; title?: unknown; errors?: Record<string, unknown> }
    if (p.errors) {
      const messages = Object.values(p.errors)
        .flat()
        .filter((m) => typeof m === 'string')
      if (messages.length > 0) return messages.join(' ')
    }
    if (typeof p.detail === 'string') return p.detail
    if (typeof p.title === 'string') return p.title
  }
  return `Request failed with status ${status}`
}

/** Unwraps an openapi-fetch result, throwing {@link ApiError} for error responses. */
export async function unwrap<T>(
  request: Promise<{ data?: T; error?: unknown; response: Response }>,
): Promise<T> {
  const { data, error, response } = await request
  if (!response.ok || data === undefined) {
    throw new ApiError(response.status, error)
  }
  return data
}

export const layerUrl = (runId: string, layerId: string) =>
  `/api/runs/${runId}/layers/${encodeURIComponent(layerId)}`

export const imageContentUrl = (imageId: string) =>
  `/api/images/${encodeURIComponent(imageId)}/content`

/** Uploads an image file; multipart bodies are sent with plain fetch. */
export async function uploadImage(file: File): Promise<ImageInfo> {
  const body = new FormData()
  body.append('file', file)
  const response = await fetch('/api/images', { method: 'POST', body })
  const json: unknown = await response.json().catch(() => undefined)
  if (!response.ok) throw new ApiError(response.status, json)
  return json as ImageInfo
}

export const isFinished = (status: RunStatus | undefined) =>
  status === 'completed' || status === 'failed' || status === 'canceled'
