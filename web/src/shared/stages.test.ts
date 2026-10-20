import { describe, expect, it } from 'vitest'

import type { RunInfo } from './api/client'
import { upsertStage } from './api/realtime'
import { labelFor } from './stages'

const run: RunInfo = {
  id: 'r',
  kind: 'detection',
  status: 'queued',
  algorithmId: 'sift',
  imageId: 'i',
  secondImageId: null,
  createdAt: '2026-10-20T00:00:00Z',
  durationMs: null,
  error: null,
  stages: [],
  layers: [],
  progress: null,
}

describe('stage labels', () => {
  it('labels known stages and prefixed matching stages', () => {
    expect(labelFor('gaussian-pyramid')).toBe('Gaussian pyramid')
    expect(labelFor('a:orientation')).toBe('A · Orientation')
    expect(labelFor('custom-stage')).toBe('custom-stage')
  })
})

describe('upsertStage', () => {
  it('adds a running stage and replaces it when it completes', () => {
    const started = upsertStage(run, {
      id: 'dog',
      state: 'running',
      durationMs: null,
      input: 0,
      kept: 0,
      rejected: 0,
    })
    expect(started.status).toBe('running')
    expect(started.stages).toHaveLength(1)

    const completed = upsertStage(started, {
      id: 'dog',
      state: 'completed',
      durationMs: 12,
      input: 0,
      kept: 0,
      rejected: 0,
    })
    expect(completed.stages).toEqual([
      { id: 'dog', state: 'completed', durationMs: 12, input: 0, kept: 0, rejected: 0 },
    ])
  })
})
