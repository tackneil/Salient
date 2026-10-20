import type { KeypointDto } from '../../shared/api/client'
import type { ParameterValues } from '../../shared/schema/parameterSchema'

/** Parameters the client applies itself to an exploration run instead of starting a new run (ADR-0003). */
export const CLIENT_FILTER_PARAMETERS = ['contrastThreshold', 'edgeThreshold'] as const

/** Largest allowed tr²/det for edge threshold r: (r + 1)² / r — the same formula the server uses. */
export const maxEdgeRatio = (r: number) => ((r + 1) * (r + 1)) / r

export interface KeypointFilter {
  contrastThreshold?: number
  edgeThreshold?: number
  /** Octave groups to show (0, 1, 2 meaning "2 and above"); undefined shows all. */
  octaveGroup?: number
}

export const octaveGroup = (octave: number) => Math.min(Math.max(octave, 0), 2)

export function filterFromParameters(values: ParameterValues): KeypointFilter {
  const number = (v: unknown) => (typeof v === 'number' ? v : undefined)
  return {
    contrastThreshold: number(values.contrastThreshold),
    edgeThreshold: number(values.edgeThreshold),
  }
}

/**
 * Whether a keypoint survives the thresholds. A missing edge ratio means det(H) ≤ 0, which never survives; missing
 * diagnostics (algorithms without them) always survive.
 */
export function passes(k: KeypointDto, filter: KeypointFilter): boolean {
  if (
    filter.contrastThreshold !== undefined &&
    k.contrast !== null &&
    k.contrast < filter.contrastThreshold
  )
    return false
  if (filter.edgeThreshold !== undefined && k.contrast !== null) {
    if (k.edgeRatio === null || k.edgeRatio >= maxEdgeRatio(filter.edgeThreshold)) return false
  }
  return filter.octaveGroup === undefined || octaveGroup(k.octave) === filter.octaveGroup
}

/** Indices of keypoints that pass the filter. */
export function visibleIndices(
  keypoints: readonly KeypointDto[],
  filter: KeypointFilter,
): number[] {
  const result: number[] = []
  keypoints.forEach((k, i) => {
    if (passes(k, filter)) result.push(i)
  })
  return result
}

/** Keypoints surviving each candidate threshold value, for the histograms above the sliders. */
export function survivalCurve(
  keypoints: readonly KeypointDto[],
  values: readonly number[],
  filterFor: (value: number) => KeypointFilter,
): number[] {
  return values.map((v) => {
    const filter = filterFor(v)
    let count = 0
    for (const k of keypoints) if (passes(k, filter)) count++
    return count
  })
}

/** True when two parameter sets differ only in client-side filter parameters, so no new run is needed. */
export function sameScaleSpace(a: ParameterValues, b: ParameterValues): boolean {
  const keys = new Set([...Object.keys(a), ...Object.keys(b)])
  for (const key of keys) {
    if ((CLIENT_FILTER_PARAMETERS as readonly string[]).includes(key)) continue
    if (a[key] !== b[key]) return false
  }
  return true
}
