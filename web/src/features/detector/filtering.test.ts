import { describe, expect, it } from 'vitest'

import type { KeypointDto } from '../../shared/api/client'
import { maxEdgeRatio, passes, sameScaleSpace, survivalCurve, visibleIndices } from './filtering'

const keypoint = (contrast: number | null, edgeRatio: number | null, octave = 0): KeypointDto => ({
  x: 0,
  y: 0,
  scale: 2,
  angle: 0,
  response: 0,
  octave,
  contrast,
  edgeRatio,
})

describe('client-side filtering', () => {
  it('uses the same edge formula as the server', () => {
    expect(maxEdgeRatio(10)).toBeCloseTo(12.1)
  })

  it('applies contrast and edge thresholds like a strict run', () => {
    const filter = { contrastThreshold: 0.04, edgeThreshold: 10 }
    expect(passes(keypoint(0.05, 5), filter)).toBe(true)
    expect(passes(keypoint(0.039, 5), filter)).toBe(false)
    expect(passes(keypoint(0.05, 12.1), filter)).toBe(false)
    // det(H) ≤ 0 arrives as null and never survives.
    expect(passes(keypoint(0.05, null), filter)).toBe(false)
    // Algorithms without diagnostics are never filtered out.
    expect(passes(keypoint(null, null), filter)).toBe(true)
  })

  it('filters by octave group', () => {
    const points = [keypoint(1, 1, -1), keypoint(1, 1, 0), keypoint(1, 1, 1), keypoint(1, 1, 4)]
    expect(visibleIndices(points, { octaveGroup: 0 })).toEqual([0, 1])
    expect(visibleIndices(points, { octaveGroup: 2 })).toEqual([3])
  })

  it('computes survival counts per threshold', () => {
    const points = [keypoint(0.01, 1), keypoint(0.03, 1), keypoint(0.05, 1)]
    expect(survivalCurve(points, [0, 0.02, 0.04, 0.06], (t) => ({ contrastThreshold: t }))).toEqual(
      [3, 2, 1, 0],
    )
  })

  it('ignores filter parameters when deciding whether to rerun', () => {
    expect(
      sameScaleSpace(
        { sigma: 1.6, contrastThreshold: 0.04 },
        { sigma: 1.6, contrastThreshold: 0.02 },
      ),
    ).toBe(true)
    expect(sameScaleSpace({ sigma: 1.6 }, { sigma: 2.0 })).toBe(false)
  })
})
