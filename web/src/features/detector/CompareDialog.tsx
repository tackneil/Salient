import CloseIcon from '@mui/icons-material/Close'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import IconButton from '@mui/material/IconButton'
import LinearProgress from '@mui/material/LinearProgress'
import ToggleButton from '@mui/material/ToggleButton'
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup'
import Typography from '@mui/material/Typography'
import { useCallback, useEffect, useState } from 'react'

import { monoFontFamily, tokens } from '../../app/theme'
import { imageContentUrl, type ComparisonResult, type ImageInfo } from '../../shared/api/client'
import { useComparison, useCreateComparison, useRun } from '../../shared/api/queries'
import { useRunProgress } from '../../shared/api/realtime'
import type { DrawFunction } from '../../shared/components/CanvasLayer'
import { CanvasLayer } from '../../shared/components/CanvasLayer'
import type { ParameterValues } from '../../shared/schema/parameterSchema'
import { fitTransform } from '../../shared/viewport/viewport'

type Mode = 'overlay' | 'side'

/** Salient's SIFT against OpenCV's on the current image (design: compact comparison dialog). */
export function CompareDialog({
  open,
  onClose,
  image,
  parameters,
}: {
  open: boolean
  onClose: () => void
  image: ImageInfo
  parameters: ParameterValues
}) {
  const [runId, setRunId] = useState<string>()
  const [mode, setMode] = useState<Mode>('overlay')
  const create = useCreateComparison()
  const run = useRun(runId)
  useRunProgress(runId)
  const result = useComparison(runId, run.data)

  const { mutate } = create
  useEffect(() => {
    if (!open) return
    mutate(
      { imageId: image.id, parameters },
      {
        onSuccess: (created) => {
          setRunId(created.runId)
        },
      },
    )
  }, [open, image.id, parameters, mutate])

  const data = result.data
  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth aria-labelledby="compare-title">
      <DialogTitle id="compare-title" sx={{ pr: 7 }}>
        Compare with reference
        <Typography variant="body2" color="text.secondary">
          Salient SIFT vs OpenCV cv::SIFT · same parameters, both with upsampling
        </Typography>
        <IconButton
          aria-label="Close"
          onClick={onClose}
          sx={{ position: 'absolute', right: 12, top: 12 }}
        >
          <CloseIcon />
        </IconButton>
      </DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, flexWrap: 'wrap' }}>
          <ToggleButtonGroup
            size="small"
            exclusive
            value={mode}
            onChange={(_, value: Mode | null) => {
              if (value) setMode(value)
            }}
            aria-label="Comparison mode"
          >
            <ToggleButton value="overlay">Overlay</ToggleButton>
            <ToggleButton value="side">Side by side</ToggleButton>
          </ToggleButtonGroup>
          <Box sx={{ flex: 1 }} />
          <Legend color={tokens.keypoint} label="Salient" round />
          <Legend color={tokens.primary} label="OpenCV" />
        </Box>

        {!data ? (
          <Box sx={{ py: 6, textAlign: 'center' }}>
            <Typography
              color={run.data?.status === 'failed' ? 'error' : 'text.secondary'}
              sx={{ mb: 2 }}
            >
              {run.data?.status === 'failed'
                ? (run.data.error ?? 'The comparison failed.')
                : 'Running both detectors…'}
            </Typography>
            {run.data?.status !== 'failed' && <LinearProgress sx={{ maxWidth: 320, mx: 'auto' }} />}
          </Box>
        ) : (
          <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'flex-start' }}>
            <Box sx={{ flex: '1 1 420px', minWidth: 0 }}>
              {mode === 'overlay' ? (
                <ComparisonView image={image} data={data} show="both" />
              ) : (
                <Box
                  sx={{ display: 'grid', gridTemplateColumns: 'repeat(2, minmax(0, 1fr))', gap: 1 }}
                >
                  <figure style={{ margin: 0 }}>
                    <ComparisonView image={image} data={data} show="salient" />
                    <Typography component="figcaption" variant="caption" color="text.secondary">
                      Salient
                    </Typography>
                  </figure>
                  <figure style={{ margin: 0 }}>
                    <ComparisonView image={image} data={data} show="reference" />
                    <Typography component="figcaption" variant="caption" color="text.secondary">
                      OpenCV cv::SIFT
                    </Typography>
                  </figure>
                </Box>
              )}
            </Box>
            <Metrics data={data} />
          </Box>
        )}
      </DialogContent>
      <DialogActions>
        <Button variant="contained" onClick={onClose}>
          Done
        </Button>
      </DialogActions>
    </Dialog>
  )
}

function Legend({ color, label, round }: { color: string; label: string; round?: boolean }) {
  return (
    <Box
      sx={{
        display: 'flex',
        alignItems: 'center',
        gap: 0.75,
        fontSize: 12,
        color: 'text.secondary',
      }}
    >
      <Box
        sx={{
          width: 10,
          height: 10,
          border: `2px solid ${color}`,
          borderRadius: round ? '50%' : 0,
        }}
      />
      {label}
    </Box>
  )
}

function ComparisonView({
  image,
  data,
  show,
}: {
  image: ImageInfo
  data: ComparisonResult
  show: 'both' | 'salient' | 'reference'
}) {
  const draw = useCallback<DrawFunction>(
    (context, _transform, size) => {
      const view = fitTransform(size, image)
      const plot = (
        points: ComparisonResult['salient'],
        color: string,
        round: boolean,
        radius: number,
      ) => {
        context.strokeStyle = color
        context.lineWidth = 1.25
        for (const p of points) {
          const x = p.x * view.scale + view.x
          const y = p.y * view.scale + view.y
          context.beginPath()
          if (round) context.arc(x, y, radius, 0, Math.PI * 2)
          else context.rect(x - radius, y - radius, radius * 2, radius * 2)
          context.stroke()
        }
      }
      if (show !== 'salient')
        plot(data.reference, tokens.primary, false, show === 'both' ? 3.5 : 2.5)
      if (show !== 'reference') plot(data.salient, tokens.keypoint, true, show === 'both' ? 5 : 2.5)
    },
    [data, image, show],
  )
  return (
    <Box
      sx={{
        position: 'relative',
        aspectRatio: `${image.width} / ${image.height}`,
        borderRadius: 1,
        overflow: 'hidden',
        bgcolor: '#000',
      }}
    >
      <Box
        component="img"
        src={imageContentUrl(image.id)}
        alt=""
        sx={{
          position: 'absolute',
          inset: 0,
          width: '100%',
          height: '100%',
          objectFit: 'contain',
          filter: 'brightness(0.5) saturate(0.4)',
        }}
      />
      <SizedCanvas draw={draw} />
    </Box>
  )
}

/** A canvas filling its parent; measures itself so it can be used outside an ImageStage. */
function SizedCanvas({ draw }: { draw: DrawFunction }) {
  const [element, setElement] = useState<HTMLDivElement | null>(null)
  const [size, setSize] = useState({ width: 0, height: 0 })
  useEffect(() => {
    if (!element) return undefined
    const observer = new ResizeObserver(([entry]) => {
      if (entry) setSize({ width: entry.contentRect.width, height: entry.contentRect.height })
    })
    observer.observe(element)
    return () => {
      observer.disconnect()
    }
  }, [element])
  return (
    <Box ref={setElement} sx={{ position: 'absolute', inset: 0 }}>
      {size.width > 0 && (
        <CanvasLayer transform={{ scale: 1, x: 0, y: 0 }} size={size} draw={draw} />
      )}
    </Box>
  )
}

function Metrics({ data }: { data: ComparisonResult }) {
  const rows: [string, string, string][] = [
    ['Keypoints', String(data.salient.length), String(data.reference.length)],
    ['Time', `${data.salientMs.toFixed(0)} ms`, `${data.referenceMs.toFixed(0)} ms`],
  ]
  const repeat = `${(data.repeatability * 100).toFixed(1)}%`
  return (
    <Box sx={{ flex: '1 1 240px', display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      <Box component="table" sx={{ borderCollapse: 'collapse', fontSize: 13, width: '100%' }}>
        <thead>
          <tr>
            <Box
              component="th"
              scope="col"
              sx={{ textAlign: 'left', pb: 1, color: 'text.secondary', fontWeight: 500 }}
            >
              Metric
            </Box>
            <Box
              component="th"
              scope="col"
              sx={{ textAlign: 'right', pb: 1, color: tokens.keypoint }}
            >
              Salient
            </Box>
            <Box
              component="th"
              scope="col"
              sx={{ textAlign: 'right', pb: 1, color: tokens.primary }}
            >
              OpenCV
            </Box>
          </tr>
        </thead>
        <tbody>
          {rows.map(([label, a, b]) => (
            <tr key={label}>
              <Box
                component="th"
                scope="row"
                sx={{
                  textAlign: 'left',
                  py: 1,
                  borderTop: 1,
                  borderColor: 'divider',
                  fontWeight: 500,
                }}
              >
                {label}
              </Box>
              <Box
                component="td"
                sx={{
                  textAlign: 'right',
                  py: 1,
                  borderTop: 1,
                  borderColor: 'divider',
                  fontFamily: monoFontFamily,
                }}
              >
                {a}
              </Box>
              <Box
                component="td"
                sx={{
                  textAlign: 'right',
                  py: 1,
                  borderTop: 1,
                  borderColor: 'divider',
                  fontFamily: monoFontFamily,
                }}
              >
                {b}
              </Box>
            </tr>
          ))}
        </tbody>
      </Box>
      <Tile label="Repeatability (±2 px)" value={repeat} bar={data.repeatability} />
      <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(3, minmax(0, 1fr))', gap: 1 }}>
        <Tile label="Mean offset" value={`${data.meanOffset.toFixed(2)} px`} />
        <Tile label="Salient only" value={String(data.onlySalient)} />
        <Tile label="OpenCV only" value={String(data.onlyReference)} />
      </Box>
    </Box>
  )
}

function Tile({ label, value, bar }: { label: string; value: string; bar?: number }) {
  return (
    <Box
      sx={{
        p: 1.5,
        borderRadius: 1.5,
        bgcolor: '#222733',
        display: 'flex',
        flexDirection: 'column',
        gap: 0.5,
      }}
    >
      <Typography variant="caption" color="text.secondary">
        {label}
      </Typography>
      <Typography sx={{ fontFamily: monoFontFamily, fontSize: 20 }}>{value}</Typography>
      {bar !== undefined && (
        <LinearProgress variant="determinate" value={bar * 100} color="success" />
      )}
    </Box>
  )
}
