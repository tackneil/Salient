import AutoFixHighIcon from '@mui/icons-material/AutoFixHighOutlined'
import UploadIcon from '@mui/icons-material/FileUploadOutlined'
import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import FormControlLabel from '@mui/material/FormControlLabel'
import MenuItem from '@mui/material/MenuItem'
import Paper from '@mui/material/Paper'
import Slider from '@mui/material/Slider'
import Switch from '@mui/material/Switch'
import TextField from '@mui/material/TextField'
import ToggleButton from '@mui/material/ToggleButton'
import Typography from '@mui/material/Typography'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link as RouterLink } from 'react-router'

import { monoFontFamily, tokens } from '../../app/theme'
import { useWorkspace } from '../../app/workspace'
import { imageContentUrl, type ImageInfo, type MatchingResult } from '../../shared/api/client'
import {
  useAlgorithms,
  useCreateMatching,
  useImage,
  useMatching,
  useRun,
  useUploadImage,
} from '../../shared/api/queries'
import { useRunProgress } from '../../shared/api/realtime'
import { drawLines, type LineShape } from '../../shared/canvas/draw'
import { CanvasLayer, type DrawFunction } from '../../shared/components/CanvasLayer'
import { EmptyState } from '../../shared/components/EmptyState'
import { InfoHint } from '../../shared/components/ParameterForm'
import { RunProgressBar } from '../../shared/components/RunProgressBar'
import { Section } from '../../shared/components/Section'
import { fillCardSx, sideColumnSx, viewSx, VIEW_GAP } from '../../shared/layout'
import { createRotatedCopy } from './testPair'

const OUTLIER = '#ff7a93'
const GAP = 4
const RATIO_BINS = 18

/** Matching view (design artboard "Matching"). */
export function MatchingPage() {
  const { imageId, matchingImageId, matchingRunId, setMatchingImage, setMatchingRun } =
    useWorkspace()
  const imageA = useImage(imageId)
  const imageB = useImage(matchingImageId)
  const algorithms = useAlgorithms()
  const upload = useUploadImage()
  const create = useCreateMatching()
  const run = useRun(matchingRunId)
  useRunProgress(matchingRunId)
  const result = useMatching(matchingRunId, run.data)
  const fileInput = useRef<HTMLInputElement>(null)

  const [algorithmId, setAlgorithmId] = useState('sift')
  const [ratio, setRatio] = useState(0.8)
  const [ransac, setRansac] = useState(true)
  const [showLines, setShowLines] = useState(true)
  const [error, setError] = useState<string>()

  const describing = algorithms.data?.filter((a) => a.capabilities.includes('Describes')) ?? []

  if (!imageId || !imageA.data) {
    return (
      <EmptyState
        title="Upload an image first"
        action={
          <Button component={RouterLink} to="/detector" variant="contained">
            Go to the detector
          </Button>
        }
      >
        Matching compares the current image with a second one. Upload an image, then come back here.
      </EmptyState>
    )
  }

  const uploadB = (file: File) => {
    setError(undefined)
    upload.mutate(file, {
      onSuccess: (image) => {
        setMatchingImage(image.id)
      },
      onError: (e) => {
        setError(e.message)
      },
    })
  }

  const createTestPair = async () => {
    try {
      setError(undefined)
      uploadB(await createRotatedCopy(imageId, imageA.data.width, imageA.data.height, 10, 0.85))
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not create the test image.')
    }
  }

  const match = () => {
    if (!matchingImageId) return
    create.mutate(
      {
        imageA: imageId,
        imageB: matchingImageId,
        algorithmId,
        parameters: null,
        ratio: 0.8,
        ransacThreshold: 3,
      },
      {
        onSuccess: (created) => {
          setMatchingRun(created.runId)
        },
        onError: (e) => {
          setError(e.message)
        },
      },
    )
  }

  const busy = create.isPending || run.data?.status === 'running' || run.data?.status === 'queued'

  return (
    <Box sx={viewSx}>
      <RunProgressBar
        run={run.data}
        expectedStages={['load-a', 'load-b', 'matching', 'ransac']}
        title="Match"
      />
      <Box sx={{ flex: '1 1 0', minHeight: 0, display: 'flex', gap: VIEW_GAP }}>
        <Box
          sx={{
            flex: '1 1 0',
            minWidth: 0,
            display: 'flex',
            flexDirection: 'column',
            gap: VIEW_GAP,
          }}
        >
          <Paper variant="outlined" component="section" aria-label="Image matching" sx={fillCardSx}>
            <Box
              sx={{
                flex: '0 0 auto',
                display: 'flex',
                flexWrap: 'nowrap',
                alignItems: 'center',
                gap: 1.5,
                px: 1.5,
                py: 1,
                borderBottom: 1,
                borderColor: 'divider',
                overflowX: 'auto',
                '& > *': { flex: '0 0 auto' },
              }}
            >
              <Chip label={<NameLabel tag="A" image={imageA.data} />} />
              <Chip
                label={imageB.data ? <NameLabel tag="B" image={imageB.data} /> : 'B · not chosen'}
                variant={imageB.data ? 'filled' : 'outlined'}
              />
              <input
                ref={fileInput}
                type="file"
                accept="image/png,image/jpeg,image/webp,image/gif,image/bmp"
                hidden
                onChange={(e) => {
                  const file = e.target.files?.[0]
                  if (file) uploadB(file)
                  e.target.value = ''
                }}
              />
              <Button
                size="small"
                variant="outlined"
                color="inherit"
                startIcon={<UploadIcon />}
                onClick={() => fileInput.current?.click()}
                disabled={upload.isPending}
                sx={{ whiteSpace: 'nowrap' }}
              >
                Upload image B
              </Button>
              <Button
                size="small"
                variant="outlined"
                color="inherit"
                startIcon={<AutoFixHighIcon />}
                onClick={() => void createTestPair()}
                disabled={upload.isPending}
                sx={{ whiteSpace: 'nowrap' }}
              >
                Test pair: 10°, ×0.85
              </Button>
              <Box sx={{ flex: '1 1 0 !important' }} />
              <ToggleButton
                size="small"
                value="lines"
                selected={showLines}
                onChange={() => {
                  setShowLines((s) => !s)
                }}
                sx={{ whiteSpace: 'nowrap' }}
              >
                Match lines
              </ToggleButton>
            </Box>
            <Box sx={{ flex: '1 1 0', minHeight: 0, p: 1.5, bgcolor: '#0b0d11' }}>
              {imageB.data ? (
                <PairView
                  a={imageA.data}
                  b={imageB.data}
                  result={result.data}
                  ratio={ratio}
                  ransac={ransac}
                  showLines={showLines}
                />
              ) : (
                <Typography color="text.secondary" sx={{ py: 8, textAlign: 'center' }}>
                  Upload a second image, or create a rotated and scaled copy of the first one.
                </Typography>
              )}
            </Box>
            <Box
              sx={{
                flex: '0 0 auto',
                display: 'flex',
                flexWrap: 'nowrap',
                alignItems: 'center',
                gap: 2.5,
                px: 2,
                py: 1,
                borderTop: 1,
                borderColor: 'divider',
                fontSize: 12,
                color: 'text.secondary',
                whiteSpace: 'nowrap',
              }}
            >
              <LegendLine color={tokens.primary} label={ransac ? 'inliers' : 'matches'} />
              {ransac && <LegendLine color={OUTLIER} label="RANSAC outliers" dashed />}
              {result.data && (
                <Typography sx={{ ml: 'auto', fontFamily: monoFontFamily, fontSize: 12 }}>
                  {result.data.keypointsA.length} / {result.data.keypointsB.length} keypoints
                </Typography>
              )}
            </Box>
          </Paper>
          {result.data && <ResultStrip result={result.data} ratio={ratio} ransac={ransac} />}
        </Box>

        <Box component="aside" aria-label="Matching parameters" sx={sideColumnSx(360)}>
          <Section title="Parameters" gap={2} grow>
            <TextField
              select
              size="small"
              label="Detector and descriptor"
              value={describing.length > 0 ? algorithmId : ''}
              onChange={(e) => {
                setAlgorithmId(e.target.value)
              }}
            >
              {describing.map((a) => (
                <MenuItem key={a.id} value={a.id}>
                  {a.displayName}
                </MenuItem>
              ))}
            </TextField>
            <Box
              sx={{
                flex: '0 0 auto',
                display: 'flex',
                flexDirection: 'column',
                gap: 0.5,
              }}
            >
              <Box
                sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}
              >
                <Box sx={{ display: 'inline-flex', alignItems: 'baseline', gap: 0.5 }}>
                  <Typography component="label" id="ratio-label" sx={{ fontWeight: 500 }}>
                    Lowe’s ratio test
                  </Typography>
                  <InfoHint text="Accept when d₁ / d₂ is below the threshold. Bars: correct (blue) and wrong (pink) matches per ratio bin." />
                </Box>
                <Typography
                  sx={{ fontFamily: monoFontFamily, fontSize: 13, color: 'primary.main' }}
                >
                  {ratio.toFixed(2)}
                </Typography>
              </Box>
              {result.data ? (
                <RatioHistogram result={result.data} ratio={ratio} />
              ) : (
                <Box sx={{ height: 120 }} />
              )}
              <Slider
                size="small"
                min={0.3}
                max={0.95}
                step={0.01}
                value={ratio}
                aria-labelledby="ratio-label"
                onChange={(_, v) => {
                  setRatio(Number(Array.isArray(v) ? v[0] : v))
                }}
              />
            </Box>
            <Box sx={{ flex: 1 }} />
            <FormControlLabel
              control={
                <Switch
                  checked={ransac}
                  onChange={(_, checked) => {
                    setRansac(checked)
                  }}
                />
              }
              label={
                <Box>
                  <Typography sx={{ fontWeight: 500 }}>RANSAC</Typography>
                  <Typography variant="caption" color="text.secondary">
                    Show the homography and flag outliers (3 px)
                  </Typography>
                </Box>
              }
            />
            <Button
              variant="contained"
              size="large"
              startIcon={<PlayArrowIcon />}
              onClick={match}
              disabled={!matchingImageId || busy}
            >
              {busy ? 'Matching…' : 'Match'}
            </Button>
            {error && (
              <Typography color="error" variant="body2" role="alert">
                {error}
              </Typography>
            )}
          </Section>
        </Box>
      </Box>
    </Box>
  )
}

function NameLabel({ tag, image }: { tag: string; image: ImageInfo }) {
  return (
    <span>
      <Box component="span" sx={{ fontFamily: monoFontFamily, color: 'primary.main', mr: 1 }}>
        {tag}
      </Box>
      {image.fileName ?? image.id.slice(0, 8)}
    </span>
  )
}

function LegendLine({ color, label, dashed }: { color: string; label: string; dashed?: boolean }) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
      <Box sx={{ width: 20, borderTop: `2px ${dashed ? 'dashed' : 'solid'} ${color}` }} />
      {label}
    </Box>
  )
}

/** Both images side by side at the same height, centred in the available area, with match lines across them. */
function PairView({
  a,
  b,
  result,
  ratio,
  ransac,
  showLines,
}: {
  a: ImageInfo
  b: ImageInfo
  result: MatchingResult | undefined
  ratio: number
  ransac: boolean
  showLines: boolean
}) {
  const [element, setElement] = useState<HTMLDivElement | null>(null)
  const [area, setArea] = useState({ width: 0, height: 0 })
  useEffect(() => {
    if (!element) return undefined
    const observer = new ResizeObserver(([entry]) => {
      if (entry) setArea({ width: entry.contentRect.width, height: entry.contentRect.height })
    })
    observer.observe(element)
    return () => {
      observer.disconnect()
    }
  }, [element])

  // The pair is as tall as the width allows, but never taller than the area it sits in.
  const aspects = a.width / a.height + b.width / b.height
  const height = area.width > 0 ? Math.min((area.width - GAP) / aspects, area.height) : 0
  const width = height * aspects + GAP
  const scaleA = height / a.height
  const scaleB = height / b.height
  const offsetB = a.width * scaleA + GAP

  const draw = useCallback<DrawFunction>(
    (context) => {
      if (!result) return
      const visible = result.matches.filter((m) => m.ratio < ratio)
      const lines: LineShape[] = []
      for (const m of visible) {
        const pa = result.keypointsA[m.a]
        const pb = result.keypointsB[m.b]
        if (!pa || !pb) continue
        const outlier = ransac && !m.inlier
        lines.push({
          from: { x: pa.x * scaleA, y: pa.y * scaleA },
          to: { x: offsetB + pb.x * scaleB, y: pb.y * scaleB },
          color: outlier ? OUTLIER : tokens.primary,
          dashed: outlier,
        })
      }
      if (showLines) drawLines(context, lines)
      context.strokeStyle = tokens.keypoint
      context.lineWidth = 1.5
      for (const line of lines) {
        for (const p of [line.from, line.to]) {
          context.beginPath()
          context.arc(p.x, p.y, 3.5, 0, Math.PI * 2)
          context.stroke()
        }
      }
    },
    [result, ratio, ransac, showLines, scaleA, scaleB, offsetB],
  )

  return (
    <Box
      ref={setElement}
      sx={{
        position: 'relative',
        width: '100%',
        height: '100%',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      {height > 0 && (
        <Box sx={{ position: 'relative', flex: '0 0 auto', width, height }}>
          <Box
            component="img"
            src={imageContentUrl(a.id)}
            alt="Image A"
            sx={{
              position: 'absolute',
              left: 0,
              top: 0,
              width: a.width * scaleA,
              height,
              filter: 'brightness(0.7) saturate(0.5)',
            }}
          />
          <Box
            component="img"
            src={imageContentUrl(b.id)}
            alt="Image B"
            sx={{
              position: 'absolute',
              left: offsetB,
              top: 0,
              width: b.width * scaleB,
              height,
              filter: 'brightness(0.7) saturate(0.5)',
            }}
          />
          <CanvasLayer transform={{ scale: 1, x: 0, y: 0 }} size={{ width, height }} draw={draw} />
        </Box>
      )}
    </Box>
  )
}

function RatioHistogram({ result, ratio }: { result: MatchingResult; ratio: number }) {
  const bins = useMemo(() => {
    const counts = Array.from({ length: RATIO_BINS }, () => ({ good: 0, bad: 0 }))
    for (const m of result.matches) {
      const bin = counts[Math.min(RATIO_BINS - 1, Math.floor(m.ratio / (1 / RATIO_BINS)))]
      if (!bin) continue
      if (m.inlier) bin.good++
      else bin.bad++
    }
    return counts
  }, [result])
  const peak = Math.max(1, ...bins.map((b) => b.good + b.bad))
  return (
    <Box
      aria-hidden
      sx={{
        height: 120,
        display: 'flex',
        alignItems: 'flex-end',
        gap: '1px',
        px: 0.75,
        mt: 0.5,
      }}
    >
      {bins.map((b, i) => {
        const active = i / RATIO_BINS < ratio
        return (
          <Box
            key={i / RATIO_BINS}
            sx={{
              flex: '1 1 0',
              height: '100%',
              display: 'flex',
              flexDirection: 'column',
              justifyContent: 'flex-end',
            }}
          >
            <Box
              sx={{ height: `${(b.bad / peak) * 100}%`, bgcolor: active ? OUTLIER : '#4a2b33' }}
            />
            <Box
              sx={{
                height: `${(b.good / peak) * 100}%`,
                bgcolor: active ? tokens.primary : '#2e3d58',
              }}
            />
          </Box>
        )
      })}
    </Box>
  )
}

/** Counts, the homography and the recovered motion, in a strip under the image pair. */
function ResultStrip({
  result,
  ratio,
  ransac,
}: {
  result: MatchingResult
  ratio: number
  ransac: boolean
}) {
  const shown = result.matches.filter((m) => m.ratio < ratio)
  const inliers = shown.filter((m) => m.inlier).length
  const h = result.homography
  return (
    <Paper
      variant="outlined"
      component="section"
      aria-label="Matching result"
      sx={{
        flex: '0 0 auto',
        px: 2,
        pt: 1.25,
        pb: 1.75,
        display: 'flex',
        flexDirection: 'column',
        gap: 1.25,
      }}
    >
      <Typography variant="overline" component="h2" color="text.secondary" sx={{ lineHeight: 1.5 }}>
        Result
      </Typography>
      <Box sx={{ display: 'flex', flexWrap: 'nowrap', alignItems: 'stretch', gap: 2 }}>
        <Tile label="Matches shown" value={String(shown.length)} />
        <Tile
          label="Inliers"
          value={
            ransac
              ? `${inliers} · ${shown.length ? Math.round((inliers / shown.length) * 100) : 0}%`
              : '—'
          }
        />
        {ransac && h && (
          <>
            <Box
              sx={{
                flex: '0 0 auto',
                display: 'flex',
                flexDirection: 'column',
                justifyContent: 'center',
                gap: 0.75,
              }}
            >
              <Typography variant="caption" color="text.secondary">
                Homography H (A → B)
              </Typography>
              <Box
                sx={{
                  width: 300,
                  display: 'grid',
                  gridTemplateColumns: 'repeat(3, minmax(0, 1fr))',
                  gap: 0.5,
                  px: 1.25,
                  py: 1,
                  borderRadius: 1,
                  bgcolor: '#0f1218',
                  border: 1,
                  borderColor: 'divider',
                  fontFamily: monoFontFamily,
                  fontSize: 13,
                  color: '#cfe0ff',
                  textAlign: 'right',
                }}
              >
                {h.map((v, i) => (
                  <span key={['h00', 'h01', 'h02', 'h10', 'h11', 'h12', 'h20', 'h21', 'h22'][i]}>
                    {i === 2 || i === 5 ? v.toFixed(1) : v.toFixed(3)}
                  </span>
                ))}
              </Box>
            </Box>
            <Box
              sx={{
                flex: '1 1 0',
                minWidth: 0,
                display: 'flex',
                flexDirection: 'column',
                justifyContent: 'center',
                gap: 0.5,
              }}
            >
              <Typography sx={{ fontSize: 13 }}>
                Recovered rotation{' '}
                <Box component="span" sx={{ fontFamily: monoFontFamily }}>
                  {result.rotationDegrees?.toFixed(2)}°
                </Box>
              </Typography>
              <Typography sx={{ fontSize: 13 }}>
                Recovered scale{' '}
                <Box component="span" sx={{ fontFamily: monoFontFamily }}>
                  {result.scale?.toFixed(4)}
                </Box>
              </Typography>
            </Box>
          </>
        )}
        {ransac && !h && (
          <Typography color="text.secondary" sx={{ alignSelf: 'center' }}>
            No homography could be estimated from these matches.
          </Typography>
        )}
      </Box>
    </Paper>
  )
}

function Tile({ label, value }: { label: string; value: string }) {
  return (
    <Box
      sx={{
        flex: '0 0 150px',
        px: 1.5,
        py: 1.25,
        borderRadius: 1,
        bgcolor: '#1e222b',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        gap: 0.5,
      }}
    >
      <Typography variant="caption" color="text.secondary">
        {label}
      </Typography>
      <Typography sx={{ fontFamily: monoFontFamily, fontSize: 22 }}>{value}</Typography>
    </Box>
  )
}
