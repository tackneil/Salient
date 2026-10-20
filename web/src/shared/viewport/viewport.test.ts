import { describe, expect, it } from 'vitest'

import { PointGrid } from './pointGrid'
import { clampPan, fitTransform, MAX_ZOOM, pan, toImage, toScreen, zoomAt } from './viewport'

const container = { width: 800, height: 600 }
const image = { width: 1600, height: 1000 }

describe('viewport', () => {
  it('fits and centers the image', () => {
    const view = fitTransform(container, image)
    expect(view.scale).toBe(0.5)
    expect(view.x).toBe(0)
    expect(view.y).toBe(50)
  })

  it('keeps the point under the cursor fixed while zooming', () => {
    const view = fitTransform(container, image)
    const anchor = { x: 300, y: 200 }
    const before = toImage(view, anchor)

    const zoomed = zoomAt(view, 3, anchor, container, image)

    expect(zoomed.scale).toBeCloseTo(1.5)
    const after = toImage(zoomed, anchor)
    expect(after.x).toBeCloseTo(before.x)
    expect(after.y).toBeCloseTo(before.y)
  })

  it('clamps zoom to the fit scale and the maximum', () => {
    const view = fitTransform(container, image)
    expect(zoomAt(view, 0.1, { x: 0, y: 0 }, container, image).scale).toBeCloseTo(0.5)
    expect(zoomAt(view, 1000, { x: 0, y: 0 }, container, image).scale).toBeCloseTo(0.5 * MAX_ZOOM)
  })

  it('never pans empty space into view and centers small content', () => {
    const zoomed = zoomAt(fitTransform(container, image), 2, { x: 400, y: 300 }, container, image)

    const farRight = pan(zoomed, 10_000, 10_000, container, image)
    expect(farRight.x).toBe(0)
    expect(farRight.y).toBe(0)

    const small = clampPan({ scale: 0.1, x: -500, y: 900 }, container, image)
    expect(small.x).toBe((800 - 160) / 2)
    expect(small.y).toBe((600 - 100) / 2)
  })

  it('converts between screen and image coordinates', () => {
    const view = { scale: 2.5, x: -40, y: 12 }
    const p = { x: 123.4, y: 56.7 }
    const back = toImage(view, toScreen(view, p))
    expect(back.x).toBeCloseTo(p.x)
    expect(back.y).toBeCloseTo(p.y)
  })
})

describe('PointGrid', () => {
  it('finds the same nearest point as a brute-force search', () => {
    let seed = 42
    const random = () => {
      seed = (seed * 1103515245 + 12345) % 2 ** 31
      return seed / 2 ** 31
    }
    const points = Array.from({ length: 2000 }, () => ({ x: random() * 900, y: random() * 600 }))
    const grid = new PointGrid(points, 12)

    for (let i = 0; i < 200; i++) {
      const q = { x: random() * 900, y: random() * 600 }
      let best: number | undefined
      let bestDistance = 20 * 20
      points.forEach((p, index) => {
        const d = (p.x - q.x) ** 2 + (p.y - q.y) ** 2
        if (d <= bestDistance) {
          bestDistance = d
          best = index
        }
      })
      const found = grid.nearest(q.x, q.y, 20)
      if (best === undefined) expect(found).toBeUndefined()
      else expect(points[found ?? -1]).toEqual(points[best])
    }
  })

  it('ignores excluded points', () => {
    const grid = new PointGrid(
      [
        { x: 0, y: 0 },
        { x: 5, y: 0 },
      ],
      16,
      (i) => i !== 0,
    )
    expect(grid.nearest(0, 0, 10)).toBe(1)
  })
})
