import type { Size, ViewTransform } from '../viewport/viewport'

export interface KeypointShape {
  x: number
  y: number
  /** Circle radius in image pixels. */
  radius: number
  /** Orientation in degrees, counterclockwise on screen. */
  angle?: number
  color: string
  dashed?: boolean
}

const visible = (x: number, y: number, r: number, size: Size) =>
  x >= -r && y >= -r && x <= size.width + r && y <= size.height + r

/** Draws keypoints as circles with an orientation tick, culling those outside the view (ADR-0007). */
export function drawKeypoints(
  context: CanvasRenderingContext2D,
  transform: ViewTransform,
  size: Size,
  shapes: readonly KeypointShape[],
) {
  context.lineWidth = 1.25
  for (const shape of shapes) {
    const cx = shape.x * transform.scale + transform.x
    const cy = shape.y * transform.scale + transform.y
    const r = Math.max(shape.radius * transform.scale, 2.5)
    if (!visible(cx, cy, r, size)) continue
    context.strokeStyle = shape.color
    context.setLineDash(shape.dashed ? [3, 3] : [])
    context.beginPath()
    context.arc(cx, cy, r, 0, Math.PI * 2)
    if (shape.angle !== undefined) {
      const radians = (shape.angle * Math.PI) / 180
      context.moveTo(cx, cy)
      context.lineTo(cx + r * Math.cos(radians), cy - r * Math.sin(radians))
    }
    context.stroke()
  }
  context.setLineDash([])
}

export interface LineShape {
  from: { x: number; y: number }
  to: { x: number; y: number }
  color: string
  dashed?: boolean
}

/** Draws lines given in screen coordinates (matching lines span two stages). */
export function drawLines(
  context: CanvasRenderingContext2D,
  lines: readonly LineShape[],
  width = 1.25,
) {
  context.lineWidth = width
  for (const line of lines) {
    context.strokeStyle = line.color
    context.setLineDash(line.dashed ? [5, 4] : [])
    context.beginPath()
    context.moveTo(line.from.x, line.from.y)
    context.lineTo(line.to.x, line.to.y)
    context.stroke()
  }
  context.setLineDash([])
}

/** Draws a rotated square, for descriptor windows. */
export function drawRotatedSquare(
  context: CanvasRenderingContext2D,
  center: { x: number; y: number },
  halfSide: number,
  angleDegrees: number,
  color: string,
  gridCells = 0,
) {
  const a = (angleDegrees * Math.PI) / 180
  const ux = { x: Math.cos(a), y: -Math.sin(a) }
  const uy = { x: Math.sin(a), y: Math.cos(a) }
  const corner = (sx: number, sy: number) => ({
    x: center.x + halfSide * (sx * ux.x + sy * uy.x),
    y: center.y + halfSide * (sx * ux.y + sy * uy.y),
  })
  context.strokeStyle = color
  context.lineWidth = 1.25
  context.beginPath()
  const corners = [corner(-1, -1), corner(1, -1), corner(1, 1), corner(-1, 1)]
  corners.forEach((p, i) => {
    if (i === 0) context.moveTo(p.x, p.y)
    else context.lineTo(p.x, p.y)
  })
  context.closePath()
  for (let i = 1; i < gridCells; i++) {
    const t = -1 + (2 * i) / gridCells
    const a1 = corner(t, -1)
    const a2 = corner(t, 1)
    const b1 = corner(-1, t)
    const b2 = corner(1, t)
    context.moveTo(a1.x, a1.y)
    context.lineTo(a2.x, a2.y)
    context.moveTo(b1.x, b1.y)
    context.lineTo(b2.x, b2.y)
  }
  context.stroke()
}
