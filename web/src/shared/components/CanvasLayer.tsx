import Box from '@mui/material/Box'
import { useEffect, useRef } from 'react'

import type { Size, ViewTransform } from '../viewport/viewport'

export type DrawFunction = (
  context: CanvasRenderingContext2D,
  transform: ViewTransform,
  size: Size,
) => void

/**
 * A full-size canvas over the stage, sized to the device pixel ratio and redrawn in an animation frame whenever the
 * transform or the draw function changes (ADR-0007). Drawing happens in screen space, so line widths stay constant
 * while zooming.
 */
export function CanvasLayer({
  transform,
  size,
  draw,
}: {
  transform: ViewTransform
  size: Size
  draw: DrawFunction
}) {
  const ref = useRef<HTMLCanvasElement>(null)

  useEffect(() => {
    const canvas = ref.current
    if (!canvas) return undefined
    const frame = requestAnimationFrame(() => {
      const ratio = window.devicePixelRatio || 1
      const width = Math.round(size.width * ratio)
      const height = Math.round(size.height * ratio)
      if (canvas.width !== width || canvas.height !== height) {
        canvas.width = width
        canvas.height = height
      }
      const context = canvas.getContext('2d')
      if (!context) return
      context.setTransform(ratio, 0, 0, ratio, 0, 0)
      context.clearRect(0, 0, size.width, size.height)
      draw(context, transform, size)
    })
    return () => {
      cancelAnimationFrame(frame)
    }
  }, [transform, size, draw])

  return (
    <Box
      component="canvas"
      ref={ref}
      aria-hidden
      sx={{ position: 'absolute', inset: 0, width: '100%', height: '100%', pointerEvents: 'none' }}
    />
  )
}
