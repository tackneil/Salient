import ChevronLeftIcon from '@mui/icons-material/ChevronLeft'
import ChevronRightIcon from '@mui/icons-material/ChevronRight'
import Box from '@mui/material/Box'
import IconButton from '@mui/material/IconButton'
import Skeleton from '@mui/material/Skeleton'
import Typography from '@mui/material/Typography'
import { useEffect, useRef } from 'react'
import { Link as RouterLink } from 'react-router'

import { monoFontFamily, tokens } from '../../app/theme'
import { imageContentUrl, type KeypointDto } from '../../shared/api/client'
import { useKeypoint } from '../../shared/api/queries'
import { drawRotatedSquare } from '../../shared/canvas/draw'
import { Section } from '../../shared/components/Section'

const PATCH_SIZE = 132

interface KeypointInspectorProps {
  runId: string
  imageId: string
  index: number
  keypoint: KeypointDto
  onPrevious: () => void
  onNext: () => void
}

/** Everything behind one keypoint: patch, orientation histogram, descriptor and measurements (design: inspector). */
export function KeypointInspector({
  runId,
  imageId,
  index,
  keypoint,
  onPrevious,
  onNext,
}: KeypointInspectorProps) {
  const detail = useKeypoint(runId, index)

  return (
    <Section
      title={`Keypoint #${index + 1}`}
      action={
        <Box sx={{ display: 'flex', gap: 0.5 }}>
          <IconButton size="small" aria-label="Previous keypoint" onClick={onPrevious}>
            <ChevronLeftIcon fontSize="small" />
          </IconButton>
          <IconButton size="small" aria-label="Next keypoint" onClick={onNext}>
            <ChevronRightIcon fontSize="small" />
          </IconButton>
        </Box>
      }
    >
      <Box sx={{ display: 'flex', gap: 1.75, alignItems: 'flex-start' }}>
        <Box component="figure" sx={{ m: 0, display: 'flex', flexDirection: 'column', gap: 0.75 }}>
          <Patch imageId={imageId} keypoint={keypoint} />
          <Typography component="figcaption" variant="caption" color="text.secondary">
            Patch · descriptor window
          </Typography>
        </Box>
        <Box
          component="dl"
          sx={{
            m: 0,
            flex: 1,
            display: 'grid',
            gridTemplateColumns: 'auto 1fr',
            gap: '6px 10px',
            fontSize: 12,
          }}
        >
          <Stat label="Position" value={`${keypoint.x.toFixed(1)}, ${keypoint.y.toFixed(1)}`} />
          <Stat label="Scale" value={`σ ${keypoint.scale.toFixed(2)}`} />
          <Stat label="Orientation" value={`${keypoint.angle.toFixed(1)}°`} />
          <Stat label="Octave" value={String(keypoint.octave)} />
          <Stat
            label="Contrast"
            value={keypoint.contrast === null ? '—' : keypoint.contrast.toFixed(4)}
          />
          <Stat
            label="Edge ratio"
            value={
              keypoint.edgeRatio === null
                ? keypoint.contrast === null
                  ? '—'
                  : 'det ≤ 0'
                : keypoint.edgeRatio.toFixed(2)
            }
          />
        </Box>
      </Box>

      {detail.data?.orientationHistogram ? (
        <OrientationHistogram bins={detail.data.orientationHistogram} />
      ) : detail.isLoading ? (
        <Skeleton variant="rounded" height={84} />
      ) : null}

      {detail.data?.descriptor ? (
        <DescriptorRoses values={detail.data.descriptor} />
      ) : detail.data?.binaryDescriptor ? (
        <Typography variant="caption" color="text.secondary">
          Binary descriptor · {detail.data.binaryDescriptor.length * 8} bits
        </Typography>
      ) : detail.isLoading ? (
        <Skeleton variant="rounded" height={160} />
      ) : null}

      <Typography
        component={RouterLink}
        to="/pipeline"
        sx={{ fontSize: 13, color: 'primary.main', textDecoration: 'none' }}
      >
        Trace keypoints through the pipeline →
      </Typography>
    </Section>
  )
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <>
      <Box component="dt" sx={{ color: 'text.secondary' }}>
        {label}
      </Box>
      <Box component="dd" sx={{ m: 0, fontFamily: monoFontFamily, textAlign: 'right' }}>
        {value}
      </Box>
    </>
  )
}

/** The neighbourhood of the keypoint, magnified, with the rotated 4×4 descriptor grid on top. */
function Patch({ imageId, keypoint }: { imageId: string; keypoint: KeypointDto }) {
  const ref = useRef<HTMLCanvasElement>(null)

  useEffect(() => {
    const canvas = ref.current
    if (!canvas) return undefined
    const image = new Image()
    let cancelled = false
    image.onload = () => {
      if (cancelled) return
      const ratio = window.devicePixelRatio || 1
      canvas.width = PATCH_SIZE * ratio
      canvas.height = PATCH_SIZE * ratio
      const context = canvas.getContext('2d')
      if (!context) return
      context.setTransform(ratio, 0, 0, ratio, 0, 0)
      // The descriptor covers 4 cells of 3σ; show a little more around it.
      const side = Math.max(16 * keypoint.scale, 24)
      const zoom = PATCH_SIZE / side
      context.imageSmoothingEnabled = zoom < 3
      context.fillStyle = '#000'
      context.fillRect(0, 0, PATCH_SIZE, PATCH_SIZE)
      context.drawImage(
        image,
        keypoint.x - side / 2,
        keypoint.y - side / 2,
        side,
        side,
        0,
        0,
        PATCH_SIZE,
        PATCH_SIZE,
      )
      drawRotatedSquare(
        context,
        { x: PATCH_SIZE / 2, y: PATCH_SIZE / 2 },
        6 * keypoint.scale * zoom,
        keypoint.angle,
        tokens.keypoint,
        4,
      )
    }
    image.src = imageContentUrl(imageId)
    return () => {
      cancelled = true
    }
  }, [imageId, keypoint])

  return (
    <Box
      component="canvas"
      ref={ref}
      role="img"
      aria-label="Magnified image patch around the keypoint"
      sx={{
        width: PATCH_SIZE,
        height: PATCH_SIZE,
        borderRadius: 1,
        border: 1,
        borderColor: 'divider',
      }}
    />
  )
}

function OrientationHistogram({ bins }: { bins: readonly number[] }) {
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.75 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
        <Typography sx={{ fontSize: 12, fontWeight: 600 }}>Orientation histogram</Typography>
        <Typography variant="caption" color="text.secondary">
          {bins.length} bins · peaks ≥ 80%
        </Typography>
      </Box>
      <Box
        role="img"
        aria-label="Orientation histogram"
        sx={{
          position: 'relative',
          height: 64,
          display: 'flex',
          alignItems: 'flex-end',
          gap: '2px',
          borderBottom: 1,
          borderColor: tokens.borderStrong,
        }}
      >
        {bins.map((v, i) => (
          <Box
            key={i}
            sx={{
              flex: '1 1 0',
              height: `${Math.max(v * 100, 3)}%`,
              bgcolor: v >= 0.8 ? tokens.keypoint : '#4e6a99',
              borderRadius: '1px 1px 0 0',
            }}
          />
        ))}
        <Box
          sx={{
            position: 'absolute',
            left: 0,
            right: 0,
            bottom: '80%',
            borderTop: '1px dashed rgba(255,181,71,.7)',
          }}
        />
      </Box>
      <Box
        sx={{
          display: 'flex',
          justifyContent: 'space-between',
          fontFamily: monoFontFamily,
          fontSize: 10,
          color: 'text.secondary',
        }}
      >
        <span>0°</span>
        <span>90°</span>
        <span>180°</span>
        <span>270°</span>
        <span>360°</span>
      </Box>
    </Box>
  )
}

/** 4×4 cells, each with 8 orientation bins drawn as a rose. */
function DescriptorRoses({ values }: { values: readonly number[] }) {
  const max = Math.max(1e-9, ...values)
  const cells = Array.from({ length: 16 }, (_, c) => values.slice(c * 8, c * 8 + 8))
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.75 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
        <Typography sx={{ fontSize: 12, fontWeight: 600 }}>Descriptor</Typography>
        <Typography variant="caption" color="text.secondary">
          4 × 4 cells × 8 orientations = {values.length}-D
        </Typography>
      </Box>
      <Box
        role="img"
        aria-label="Descriptor as 16 orientation roses"
        sx={{
          display: 'grid',
          gridTemplateColumns: 'repeat(4, minmax(0, 1fr))',
          gap: '2px',
          bgcolor: 'divider',
          border: 1,
          borderColor: 'divider',
          borderRadius: 1,
          overflow: 'hidden',
        }}
      >
        {cells.map((bins, c) => (
          <Box
            key={c}
            component="svg"
            viewBox="-1 -1 2 2"
            sx={{ display: 'block', width: '100%', aspectRatio: '1', bgcolor: '#12151b' }}
          >
            {bins.map((v, b) => {
              const a = (b * Math.PI) / 4
              const length = (v / max) * 0.92
              return (
                <line
                  key={b}
                  x1={0}
                  y1={0}
                  x2={length * Math.cos(a)}
                  y2={-length * Math.sin(a)}
                  stroke={v / max > 0.6 ? tokens.keypoint : tokens.primary}
                  strokeWidth={0.07}
                  strokeLinecap="round"
                />
              )
            })}
          </Box>
        ))}
      </Box>
    </Box>
  )
}
