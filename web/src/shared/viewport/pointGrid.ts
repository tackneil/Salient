import type { Point } from './viewport'

/**
 * A uniform grid over points for fast nearest-point queries, used for hit-testing thousands of keypoints on the canvas
 * (ADR-0007). Built once per data set.
 */
export class PointGrid {
  private readonly cells = new Map<string, number[]>()
  private readonly points: readonly Point[]
  private readonly cellSize: number

  constructor(
    points: readonly Point[],
    cellSize = 16,
    include: (index: number) => boolean = () => true,
  ) {
    this.points = points
    this.cellSize = cellSize
    points.forEach((p, index) => {
      if (!include(index)) return
      const key = this.key(Math.floor(p.x / cellSize), Math.floor(p.y / cellSize))
      const cell = this.cells.get(key)
      if (cell) cell.push(index)
      else this.cells.set(key, [index])
    })
  }

  /** Index of the point nearest to (x, y) within `maxDistance`, or undefined. */
  nearest(x: number, y: number, maxDistance: number): number | undefined {
    const reach = Math.ceil(maxDistance / this.cellSize)
    const cx = Math.floor(x / this.cellSize)
    const cy = Math.floor(y / this.cellSize)
    let best: number | undefined
    let bestDistance = maxDistance * maxDistance
    for (let gy = cy - reach; gy <= cy + reach; gy++) {
      for (let gx = cx - reach; gx <= cx + reach; gx++) {
        for (const index of this.cells.get(this.key(gx, gy)) ?? []) {
          const p = this.points[index]
          if (!p) continue
          const d = (p.x - x) ** 2 + (p.y - y) ** 2
          if (d <= bestDistance) {
            bestDistance = d
            best = index
          }
        }
      }
    }
    return best
  }

  private key(gx: number, gy: number) {
    return `${gx}:${gy}`
  }
}
