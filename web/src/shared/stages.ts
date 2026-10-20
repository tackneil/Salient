/** Display names for pipeline stage ids reported by the server (ADR-0004). */
export const stageLabels: Record<string, string> = {
  load: 'Load',
  'gaussian-pyramid': 'Gaussian pyramid',
  dog: 'DoG',
  extrema: 'Extrema',
  refine: 'Refine',
  contrast: 'Contrast',
  edge: 'Edges',
  orientation: 'Orientation',
  heatmaps: 'Heatmaps',
  descriptor: 'Descriptors',
  opencv: 'OpenCV',
  matching: 'Matching',
  ransac: 'RANSAC',
  sweep: 'Sweep',
  salient: 'Salient SIFT',
  compare: 'Compare',
}

/** Label for a stage id; ids prefixed with "a:" or "b:" belong to one image of a matching run. */
export function labelFor(id: string): string {
  const separator = id.indexOf(':')
  const stage = separator < 0 ? id : id.slice(separator + 1)
  const label = stageLabels[stage] ?? stage
  return separator < 0 ? label : `${id.slice(0, separator).toUpperCase()} · ${label}`
}

/** Stages of a Salient SIFT detection run, in order. */
export const SIFT_DETECTION_STAGES = [
  'load',
  'gaussian-pyramid',
  'dog',
  'extrema',
  'refine',
  'contrast',
  'edge',
  'orientation',
  'heatmaps',
  'descriptor',
] as const
