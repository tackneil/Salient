import { useMemo } from 'react'

import { tokens } from '../../app/theme'
import type { CandidateDto, KeypointDto } from '../../shared/api/client'
import type { KeypointShape } from '../../shared/canvas/draw'
import { maxEdgeRatio, visibleIndices } from '../detector/filtering'
import type { PipelineStep } from './steps'

export const REJECTED_COLOR = '#ff7a93'
const DESCRIPTOR_WINDOWS = 18

export interface Thresholds {
  contrastThreshold?: number
  edgeThreshold?: number
}

/** Whether a refined candidate survives the contrast and edge thresholds currently set in the detector. */
export function passesUserThresholds(c: CandidateDto, t: Thresholds) {
  if (c.contrast === null || c.contrast < (t.contrastThreshold ?? 0)) return false
  return c.edgeRatio !== null && c.edgeRatio < maxEdgeRatio(t.edgeThreshold ?? 10)
}

/** What a keypoint stage draws on the image, and the one-line summary shown in the view's footer. */
export function useStageShapes(
  step: PipelineStep | undefined,
  candidates: readonly CandidateDto[],
  keypoints: readonly KeypointDto[],
  thresholds: Thresholds,
) {
  const shapes = useMemo(() => {
    const kept: KeypointShape[] = []
    const rejected: KeypointShape[] = []
    const add = (
      list: KeypointShape[],
      x: number,
      y: number,
      scale: number,
      color: string,
      dashed = false,
      angle?: number,
    ) => {
      list.push({ x, y, radius: Math.max(scale * 1.6, 3) / 2, color, dashed, angle })
    }
    if (step?.id === 'extrema') {
      for (const c of candidates)
        add(
          c.rejection ? rejected : kept,
          c.x,
          c.y,
          c.scale,
          c.rejection ? REJECTED_COLOR : tokens.keypoint,
          Boolean(c.rejection),
        )
    } else if (step?.id === 'contrast') {
      for (const c of candidates) {
        const ok = c.contrast !== null && c.contrast >= (thresholds.contrastThreshold ?? 0)
        add(ok ? kept : rejected, c.x, c.y, c.scale, ok ? tokens.keypoint : REJECTED_COLOR, !ok)
      }
    } else if (step?.id === 'edge') {
      for (const c of candidates) {
        if (c.contrast === null || c.contrast < (thresholds.contrastThreshold ?? 0)) continue
        const ok = passesUserThresholds(c, thresholds)
        add(ok ? kept : rejected, c.x, c.y, c.scale, ok ? tokens.keypoint : REJECTED_COLOR, !ok)
      }
    } else {
      for (const i of visibleIndices(keypoints, thresholds)) {
        const k = keypoints[i]
        if (k)
          add(
            kept,
            k.x,
            k.y,
            k.scale,
            step?.id === 'descriptor' ? 'rgba(255,181,71,0.35)' : tokens.keypoint,
            false,
            k.angle,
          )
      }
    }
    return { kept, rejected }
  }, [step, candidates, keypoints, thresholds])

  const windows = useMemo(() => {
    if (step?.id !== 'descriptor') return []
    return visibleIndices(keypoints, thresholds)
      .flatMap((i) => (keypoints[i] ? [keypoints[i]] : []))
      .sort((a, b) => b.scale * b.response - a.scale * a.response)
      .slice(0, DESCRIPTOR_WINDOWS)
  }, [step, keypoints, thresholds])

  const label =
    step?.id === 'descriptor'
      ? `${String(shapes.kept.length)} descriptors · ${String(windows.length)} descriptor windows shown`
      : `${String(shapes.kept.length)} kept · ${String(shapes.rejected.length)} rejected at this stage`

  return { shapes, windows, label }
}
