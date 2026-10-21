import AddIcon from '@mui/icons-material/Add'
import FitScreenIcon from '@mui/icons-material/FitScreen'
import RemoveIcon from '@mui/icons-material/Remove'
import Box from '@mui/material/Box'
import IconButton from '@mui/material/IconButton'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type KeyboardEvent,
  type PointerEvent,
  type ReactNode,
} from 'react'

import { monoFontFamily } from '../../app/theme'
import {
  fitTransform,
  pan,
  relativeZoom,
  toImage,
  zoomAt,
  type Point,
  type Size,
  type ViewTransform,
} from '../viewport/viewport'

export interface StageView {
  transform: ViewTransform
  container: Size
}

interface ImageStageProps {
  /** Image size in pixels; all layers share these coordinates. */
  imageSize: Size
  /** Layers in image coordinates (images placed with {@link StageImage}) and canvases drawn with the transform. */
  children: (view: StageView) => ReactNode
  /** Called with image coordinates when the user clicks without dragging. */
  onImageClick?: (point: Point) => void
  onKeyDown?: (event: KeyboardEvent<HTMLDivElement>) => void
  label: string
  /** Caps the height of a stage that sizes itself from the image's aspect ratio. Ignored with `fill`. */
  maxHeight?: number | string
  /** Fills the parent in both directions instead of following the image's aspect ratio; the image is fitted inside. */
  fill?: boolean
}

const DRAG_THRESHOLD = 4

/**
 * A zoomable, pannable viewer (ADR-0007). Wheel zooms around the cursor, drag pans, double-click fits, + and − zoom
 * from the keyboard. Layers receive the current image→screen transform.
 */
export function ImageStage({
  imageSize,
  children,
  onImageClick,
  onKeyDown,
  label,
  maxHeight = '72vh',
  fill = false,
}: ImageStageProps) {
  const ref = useRef<HTMLDivElement>(null)
  const [container, setContainer] = useState<Size>({ width: 0, height: 0 })
  const [view, setView] = useState<ViewTransform>({ scale: 1, x: 0, y: 0 })
  const drag = useRef<{ start: Point; last: Point; moved: boolean } | null>(null)
  const [dragging, setDragging] = useState(false)

  useLayoutEffect(() => {
    const element = ref.current
    if (!element) return undefined
    const observer = new ResizeObserver(([entry]) => {
      if (!entry) return
      const size = { width: entry.contentRect.width, height: entry.contentRect.height }
      setContainer(size)
      setView(fitTransform(size, imageSize))
    })
    observer.observe(element)
    return () => {
      observer.disconnect()
    }
  }, [imageSize])

  const zoomBy = useCallback(
    (factor: number, anchor?: Point) => {
      setView((v) =>
        zoomAt(
          v,
          factor,
          anchor ?? { x: container.width / 2, y: container.height / 2 },
          container,
          imageSize,
        ),
      )
    },
    [container, imageSize],
  )

  // Wheel listeners must be non-passive to prevent page scrolling while zooming.
  useEffect(() => {
    const element = ref.current
    if (!element) return undefined
    const onWheel = (event: WheelEvent) => {
      event.preventDefault()
      const rect = element.getBoundingClientRect()
      zoomBy(Math.exp(-event.deltaY * 0.0015), {
        x: event.clientX - rect.left,
        y: event.clientY - rect.top,
      })
    }
    element.addEventListener('wheel', onWheel, { passive: false })
    return () => {
      element.removeEventListener('wheel', onWheel)
    }
  }, [zoomBy])

  const localPoint = (event: PointerEvent) => {
    const rect = event.currentTarget.getBoundingClientRect()
    return { x: event.clientX - rect.left, y: event.clientY - rect.top }
  }

  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    if (event.button !== 0) return
    event.currentTarget.setPointerCapture(event.pointerId)
    const p = localPoint(event)
    drag.current = { start: p, last: p, moved: false }
  }

  const onPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    const state = drag.current
    if (!state) return
    const p = localPoint(event)
    if (!state.moved && Math.hypot(p.x - state.start.x, p.y - state.start.y) > DRAG_THRESHOLD) {
      state.moved = true
      setDragging(true)
    }
    if (state.moved) {
      setView((v) => pan(v, p.x - state.last.x, p.y - state.last.y, container, imageSize))
    }
    state.last = p
  }

  const onPointerUp = (event: PointerEvent<HTMLDivElement>) => {
    const state = drag.current
    drag.current = null
    setDragging(false)
    if (state && !state.moved && onImageClick) onImageClick(toImage(view, localPoint(event)))
  }

  const fit = () => {
    setView(fitTransform(container, imageSize))
  }

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === '+' || event.key === '=') zoomBy(1.25)
    else if (event.key === '-') zoomBy(0.8)
    else if (event.key === '0') fit()
    onKeyDown?.(event)
  }

  const zoomPercent =
    container.width > 0 ? Math.round(relativeZoom(view, container, imageSize) * 100) : 100

  return (
    <Box sx={{ position: 'relative', width: '100%', height: fill ? '100%' : undefined }}>
      <Box
        ref={ref}
        role="application"
        aria-label={label}
        tabIndex={0}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={onPointerUp}
        onDoubleClick={fit}
        onKeyDown={handleKeyDown}
        sx={{
          position: 'relative',
          width: '100%',
          ...(fill
            ? { height: '100%' }
            : {
                aspectRatio: `${String(imageSize.width)} / ${String(imageSize.height)}`,
                maxHeight,
              }),
          overflow: 'hidden',
          borderRadius: 1,
          bgcolor: '#0b0d11',
          cursor: dragging ? 'grabbing' : 'crosshair',
          touchAction: 'none',
          outline: 'none',
          '&:focus-visible': { boxShadow: (t) => `0 0 0 2px ${t.palette.primary.main}` },
        }}
      >
        {container.width > 0 && children({ transform: view, container })}
      </Box>
      <Box
        sx={{
          position: 'absolute',
          right: 8,
          bottom: 8,
          display: 'flex',
          alignItems: 'center',
          gap: 0.5,
          px: 0.5,
          borderRadius: 2,
          bgcolor: 'rgba(11,13,17,0.8)',
        }}
      >
        <Tooltip title="Zoom out (−)">
          <IconButton
            size="small"
            aria-label="Zoom out"
            onClick={() => {
              zoomBy(0.8)
            }}
          >
            <RemoveIcon fontSize="small" />
          </IconButton>
        </Tooltip>
        <Typography
          sx={{ fontFamily: monoFontFamily, fontSize: 12, minWidth: 44, textAlign: 'center' }}
        >
          {zoomPercent}%
        </Typography>
        <Tooltip title="Zoom in (+)">
          <IconButton
            size="small"
            aria-label="Zoom in"
            onClick={() => {
              zoomBy(1.25)
            }}
          >
            <AddIcon fontSize="small" />
          </IconButton>
        </Tooltip>
        <Tooltip title="Fit (0 or double-click)">
          <IconButton size="small" aria-label="Fit image" onClick={fit}>
            <FitScreenIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Box>
    </Box>
  )
}

/** An image layer placed in image coordinates under the stage transform. */
export function StageImage({
  src,
  size,
  view,
  opacity = 1,
  alt = '',
  filter,
}: {
  src: string
  size: Size
  view: StageView
  opacity?: number
  alt?: string
  filter?: string
}) {
  const { scale, x, y } = view.transform
  return (
    <Box
      component="img"
      src={src}
      alt={alt}
      draggable={false}
      sx={{
        position: 'absolute',
        left: 0,
        top: 0,
        width: size.width,
        height: size.height,
        maxWidth: 'none',
        transformOrigin: '0 0',
        transform: `matrix(${String(scale)}, 0, 0, ${String(scale)}, ${String(x)}, ${String(y)})`,
        opacity,
        filter,
        imageRendering: scale > 3 ? 'pixelated' : 'auto',
        pointerEvents: 'none',
        userSelect: 'none',
      }}
    />
  )
}
