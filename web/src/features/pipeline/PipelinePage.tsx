import CodeIcon from '@mui/icons-material/Code'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import ButtonBase from '@mui/material/ButtonBase'
import Link from '@mui/material/Link'
import Paper from '@mui/material/Paper'
import ToggleButton from '@mui/material/ToggleButton'
import Typography from '@mui/material/Typography'
import { useCallback, useMemo, useState } from 'react'
import { Link as RouterLink } from 'react-router'

import { monoFontFamily, tokens } from '../../app/theme'
import { useWorkspace } from '../../app/workspace'
import {
  imageContentUrl,
  layerUrl,
  type KeypointDto,
  type LayerInfo,
} from '../../shared/api/client'
import {
  useAlgorithms,
  useCandidates,
  useDetection,
  useImage,
  useRun,
} from '../../shared/api/queries'
import { useRunProgress } from '../../shared/api/realtime'
import { drawKeypoints, drawRotatedSquare, type KeypointShape } from '../../shared/canvas/draw'
import { CanvasLayer, type DrawFunction } from '../../shared/components/CanvasLayer'
import { EmptyState } from '../../shared/components/EmptyState'
import { ImageStage, StageImage } from '../../shared/components/ImageStage'
import { Section } from '../../shared/components/Section'
import { fillCardSx, sideColumnSx, viewSx, VIEW_GAP } from '../../shared/layout'
import { normalizeValues, parseSchema } from '../../shared/schema/parameterSchema'
import { filterFromParameters, visibleIndices } from '../detector/filtering'
import { passesUserThresholds, useStageShapes } from './stageShapes'
import { PIPELINE_STEPS, sourceBaseUrl, type PipelineStep } from './steps'

const MAX_OCTAVES_SHOWN = 3
const NO_KEYPOINTS: readonly KeypointDto[] = []

/** Pipeline view (design artboard "Pipeline"): step through every stage of the current detection run. */
export function PipelinePage() {
  const { imageId, algorithmId, parameters, detectionRunId } = useWorkspace()
  const image = useImage(imageId)
  const run = useRun(detectionRunId)
  useRunProgress(detectionRunId)
  const detection = useDetection(detectionRunId, run.data)
  const algorithms = useAlgorithms()
  const [stepIndex, setStepIndex] = useState(2)
  const [showRejected, setShowRejected] = useState(true)

  const algorithm = algorithms.data?.find((a) => a.id === algorithmId)
  const values = useMemo(
    () => normalizeValues(parseSchema(algorithm?.parameterSchema), parameters[algorithmId]),
    [algorithm, parameters, algorithmId],
  )
  const thresholds = useMemo(() => filterFromParameters(values), [values])
  const step = PIPELINE_STEPS[stepIndex] ?? PIPELINE_STEPS[0]
  const exposesPipeline = algorithm?.capabilities.includes('ExposesPipeline') ?? false

  const candidateStage =
    step?.id === 'extrema'
      ? 'refine'
      : step?.id === 'contrast'
        ? 'contrast'
        : step?.id === 'edge'
          ? 'edge'
          : undefined
  const ready = run.data?.status === 'completed'
  const candidates = useCandidates(detectionRunId, candidateStage, ready)
  const edgeCandidates = useCandidates(detectionRunId, 'edge', ready)
  const contrastCandidates = useCandidates(detectionRunId, 'contrast', ready)
  const keypoints = detection.data?.keypoints ?? NO_KEYPOINTS
  const stage = useStageShapes(step, candidates.data ?? [], keypoints, thresholds)

  if (!imageId || !image.data || !detectionRunId) {
    return (
      <EmptyState
        title="No detection run yet"
        action={
          <Button component={RouterLink} to="/detector" variant="contained">
            Go to the detector
          </Button>
        }
      >
        Upload an image and run the detector; this view then shows each stage of that run.
      </EmptyState>
    )
  }
  if (!exposesPipeline) {
    return (
      <EmptyState title="This algorithm has no pipeline view">
        {algorithm?.displayName ?? 'The selected algorithm'} runs as a single step. Choose Salient’s
        SIFT in the detector to step through its pipeline.
      </EmptyState>
    )
  }

  const stageCount = (id: string) => run.data?.stages.find((s) => s.id === id)
  const finalCount = visibleIndices(keypoints, thresholds).length
  const contrastKept = (contrastCandidates.data ?? []).filter(
    (c) => c.contrast !== null && c.contrast >= (thresholds.contrastThreshold ?? 0),
  ).length
  const edgeKept = (edgeCandidates.data ?? []).filter((c) =>
    passesUserThresholds(c, thresholds),
  ).length
  const funnel = [
    { step: 2, title: 'Extrema (refined)', count: stageCount('refine')?.kept ?? 0 },
    { step: 3, title: 'After contrast threshold', count: contrastKept },
    { step: 4, title: 'After edge rejection', count: edgeKept },
    { step: 5, title: 'With orientation', count: finalCount },
    { step: 6, title: 'With descriptor', count: finalCount },
  ]
  const funnelMax = Math.max(1, stageCount('extrema')?.kept ?? 0, ...funnel.map((f) => f.count))
  const isLayerGrid = step?.id === 'pyramid' || step?.id === 'dog'
  const layers = run.data?.layers ?? []

  return (
    <Box sx={viewSx}>
      <Paper
        variant="outlined"
        component="nav"
        aria-label="Pipeline stages"
        sx={{ flex: '0 0 auto', p: 0.75 }}
      >
        <Box
          component="ol"
          sx={{
            listStyle: 'none',
            m: 0,
            p: 0,
            display: 'flex',
            flexWrap: 'nowrap',
            gap: 0.5,
            overflowX: 'auto',
          }}
        >
          {PIPELINE_STEPS.map((s, i) => {
            const current = i === stepIndex
            const ms = s.stageIds.reduce((sum, id) => sum + (stageCount(id)?.durationMs ?? 0), 0)
            return (
              <Box component="li" key={s.id} sx={{ flex: '1 0 168px', minWidth: 0 }}>
                <ButtonBase
                  aria-current={current ? 'step' : undefined}
                  onClick={() => {
                    setStepIndex(i)
                  }}
                  sx={{
                    width: '100%',
                    justifyContent: 'flex-start',
                    gap: 1.25,
                    minHeight: 44,
                    px: 1.5,
                    py: 0.5,
                    borderRadius: 1,
                    textAlign: 'left',
                    bgcolor: current ? '#26324a' : 'transparent',
                  }}
                >
                  <Box
                    sx={{
                      flex: '0 0 auto',
                      width: 28,
                      height: 28,
                      borderRadius: '50%',
                      display: 'grid',
                      placeItems: 'center',
                      fontSize: 13,
                      fontWeight: 700,
                      bgcolor: current ? tokens.primary : i < stepIndex ? '#2e3d58' : '#262b35',
                      color: current
                        ? tokens.onPrimary
                        : i < stepIndex
                          ? '#cfe0ff'
                          : 'text.secondary',
                    }}
                  >
                    {i + 1}
                  </Box>
                  <Box sx={{ minWidth: 0 }}>
                    <Typography sx={{ fontSize: 13, fontWeight: 600, lineHeight: 1.3 }}>
                      {s.title}
                    </Typography>
                    <Typography
                      sx={{ fontFamily: monoFontFamily, fontSize: 11, color: 'text.secondary' }}
                    >
                      {ms < 1 ? '< 1' : ms.toFixed(0)} ms
                    </Typography>
                  </Box>
                </ButtonBase>
              </Box>
            )
          })}
        </Box>
      </Paper>

      <Box sx={{ flex: '1 1 0', minHeight: 0, display: 'flex', gap: VIEW_GAP }}>
        <Paper
          variant="outlined"
          component="section"
          aria-label="Stage visualization"
          sx={fillCardSx}
        >
          <Box
            sx={{
              flex: '0 0 auto',
              height: 56,
              boxSizing: 'border-box',
              display: 'flex',
              flexWrap: 'nowrap',
              alignItems: 'center',
              gap: 1.5,
              px: 2,
              py: 1,
              borderBottom: 1,
              borderColor: 'divider',
            }}
          >
            <Typography component="h1" sx={{ fontSize: 16, fontWeight: 600, flex: 1 }}>
              Step {stepIndex + 1}. {step?.title}
            </Typography>
            {candidateStage && (
              <ToggleButton
                size="small"
                value="rejected"
                selected={showRejected}
                onChange={() => {
                  setShowRejected((s) => !s)
                }}
              >
                Show rejected
              </ToggleButton>
            )}
          </Box>

          {!ready ? (
            <Typography sx={{ flex: 1, p: 4 }} color="text.secondary">
              Waiting for the run to finish…
            </Typography>
          ) : isLayerGrid ? (
            <LayerGrid
              runId={detectionRunId}
              layers={layers}
              kind={step.id === 'pyramid' ? 'gaussian' : 'dog'}
            />
          ) : (
            <Box sx={{ flex: '1 1 0', minHeight: 0, p: 1.5, bgcolor: '#0b0d11' }}>
              <StageOverlay
                step={step}
                imageId={imageId}
                imageSize={image.data}
                kept={stage.shapes.kept}
                rejected={stage.shapes.rejected}
                windows={stage.windows}
                showRejected={showRejected}
              />
            </Box>
          )}

          <Box
            sx={{
              flex: '0 0 auto',
              display: 'flex',
              flexWrap: 'nowrap',
              alignItems: 'center',
              gap: 1.5,
              px: 2,
              py: 1,
              borderTop: 1,
              borderColor: 'divider',
            }}
          >
            <Typography
              noWrap
              sx={{ fontFamily: monoFontFamily, fontSize: 12, color: 'text.secondary', flex: 1 }}
            >
              {isLayerGrid
                ? gridSummary(layers, step.id === 'pyramid' ? 'gaussian' : 'dog')
                : stage.label}
              {' · '}contrast {thresholds.contrastThreshold?.toFixed(4)}, r ={' '}
              {thresholds.edgeThreshold}
            </Typography>
            <Button
              variant="outlined"
              color="inherit"
              disabled={stepIndex === 0}
              onClick={() => {
                setStepIndex((i) => Math.max(0, i - 1))
              }}
            >
              Back
            </Button>
            <Button
              variant="contained"
              disabled={stepIndex === PIPELINE_STEPS.length - 1}
              onClick={() => {
                setStepIndex((i) => Math.min(PIPELINE_STEPS.length - 1, i + 1))
              }}
            >
              Next
            </Button>
          </Box>
        </Paper>

        <Box component="aside" aria-label="Stage explanation" sx={sideColumnSx(340)}>
          {step && <Explanation step={step} />}
          <Section title="Candidates per stage" gap={1}>
            {funnel.map((f) => {
              const current = f.step === stepIndex
              return (
                <ButtonBase
                  key={f.title}
                  onClick={() => {
                    setStepIndex(f.step)
                  }}
                  sx={{
                    display: 'grid',
                    gridTemplateColumns: 'minmax(0, 1fr) 52px',
                    gap: '4px 10px',
                    alignItems: 'center',
                    width: '100%',
                    p: 0.75,
                    borderRadius: 1,
                    textAlign: 'left',
                    bgcolor: current ? '#222a3a' : 'transparent',
                  }}
                >
                  <Typography
                    sx={{ fontSize: 13, color: current ? 'text.primary' : 'text.secondary' }}
                  >
                    {f.title}
                  </Typography>
                  <Typography sx={{ fontFamily: monoFontFamily, fontSize: 13, textAlign: 'right' }}>
                    {f.count}
                  </Typography>
                  <Box
                    sx={{
                      gridColumn: '1 / -1',
                      height: 6,
                      borderRadius: 1,
                      bgcolor: '#222733',
                      overflow: 'hidden',
                    }}
                  >
                    <Box
                      sx={{
                        height: '100%',
                        width: `${(f.count / funnelMax) * 100}%`,
                        bgcolor: current ? tokens.keypoint : '#4a5263',
                      }}
                    />
                  </Box>
                </ButtonBase>
              )
            })}
          </Section>
        </Box>
      </Box>
    </Box>
  )
}

function Explanation({ step }: { step: PipelineStep }) {
  return (
    <Section title="What happens" grow>
      <Typography sx={{ lineHeight: 1.6, color: '#d4d8e1' }}>{step.text}</Typography>
      <Box
        sx={{
          p: 1.5,
          borderRadius: 1,
          bgcolor: '#0f1218',
          border: 1,
          borderColor: 'divider',
          fontFamily: monoFontFamily,
          fontSize: 13,
          color: '#cfe0ff',
        }}
      >
        {step.formula}
      </Box>
      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: '8px 16px', alignItems: 'center' }}>
        <Link
          href="https://www.cs.ubc.ca/~lowe/papers/ijcv04.pdf"
          target="_blank"
          rel="noreferrer"
          underline="hover"
          sx={{ fontSize: 13 }}
        >
          Lowe, 2004 — {step.section}
        </Link>
        {sourceBaseUrl ? (
          <Link
            href={`${sourceBaseUrl}${step.sourcePath}`}
            target="_blank"
            rel="noreferrer"
            underline="hover"
            sx={{ fontSize: 13, display: 'flex', alignItems: 'center', gap: 0.5 }}
          >
            <CodeIcon fontSize="small" /> Show code
          </Link>
        ) : (
          <Typography
            sx={{
              fontSize: 12,
              color: 'text.secondary',
              display: 'flex',
              alignItems: 'center',
              gap: 0.5,
              fontFamily: monoFontFamily,
              wordBreak: 'break-all',
            }}
          >
            <CodeIcon fontSize="small" /> {step.sourcePath}
          </Typography>
        )}
      </Box>
    </Section>
  )
}

/** Octaves and layers of one kind, for the footer under the tiles. */
function layerRows(layers: readonly LayerInfo[], kind: 'gaussian' | 'dog') {
  const ofKind = layers.filter((l) => l.kind === kind && l.octave !== null && l.layer !== null)
  const octaves = [...new Set(ofKind.map((l) => l.octave ?? 0))]
    .sort((a, b) => a - b)
    .slice(0, MAX_OCTAVES_SHOWN)
  const columns = Math.max(1, ...ofKind.map((l) => (l.layer ?? 0) + 1))
  return { ofKind, octaves, columns }
}

function gridSummary(layers: readonly LayerInfo[], kind: 'gaussian' | 'dog') {
  const { octaves, columns } = layerRows(layers, kind)
  return `${String(octaves.length)} octaves × ${String(columns)} ${kind === 'dog' ? 'DoG ' : ''}layers`
}

/** Octave × layer tiles of Gaussian or DoG layers; the rows share the height of the card. */
function LayerGrid({
  runId,
  layers,
  kind,
}: {
  runId: string
  layers: readonly LayerInfo[]
  kind: 'gaussian' | 'dog'
}) {
  const { ofKind, octaves, columns } = layerRows(layers, kind)
  return (
    <Box
      sx={{
        flex: '1 1 0',
        minHeight: 0,
        px: 2,
        py: 1.5,
        display: 'flex',
        flexDirection: 'column',
        gap: 1.25,
        bgcolor: '#0b0d11',
      }}
    >
      {octaves.map((octave) => {
        const row = ofKind
          .filter((l) => l.octave === octave)
          .sort((a, b) => (a.layer ?? 0) - (b.layer ?? 0))
        const first = row[0]
        return (
          <Box
            key={octave}
            sx={{
              flex: '1 1 0',
              minHeight: 0,
              display: 'flex',
              flexDirection: 'column',
              gap: 0.75,
            }}
          >
            <Box sx={{ flex: '0 0 auto', display: 'flex', alignItems: 'baseline', gap: 1.25 }}>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>Octave {octave}</Typography>
              {first && (
                <Typography
                  sx={{ fontFamily: monoFontFamily, fontSize: 12, color: 'text.secondary' }}
                >
                  {first.width} × {first.height}
                </Typography>
              )}
            </Box>
            <Box
              sx={{
                flex: '1 1 0',
                minHeight: 0,
                display: 'grid',
                gridTemplateColumns: `repeat(${String(columns)}, minmax(0, 1fr))`,
                gap: 1,
              }}
            >
              {row.map((layer) => (
                <Box
                  component="figure"
                  key={layer.id}
                  sx={{ m: 0, minHeight: 0, display: 'flex', flexDirection: 'column', gap: 0.5 }}
                >
                  <Box
                    sx={{
                      flex: '1 1 0',
                      minHeight: 0,
                      borderRadius: 1,
                      border: 1,
                      borderColor: 'divider',
                      overflow: 'hidden',
                      bgcolor: '#000',
                    }}
                  >
                    <Box
                      component="img"
                      loading="lazy"
                      src={layerUrl(runId, layer.id)}
                      alt={`${kind === 'gaussian' ? 'Gaussian' : 'DoG'} layer ${String(layer.layer ?? 0)} of octave ${String(octave)}`}
                      sx={{
                        width: '100%',
                        height: '100%',
                        objectFit: 'contain',
                        display: 'block',
                        imageRendering: layer.width < 300 ? 'pixelated' : 'auto',
                      }}
                    />
                  </Box>
                  <Typography
                    component="figcaption"
                    sx={{ fontFamily: monoFontFamily, fontSize: 11, color: 'text.secondary' }}
                  >
                    {kind === 'gaussian'
                      ? `σ = ${layer.sigma.toFixed(2)}`
                      : `D${String((layer.layer ?? 0) + 1)} · σ ${layer.sigma.toFixed(2)}`}
                  </Typography>
                </Box>
              ))}
            </Box>
          </Box>
        )
      })}
    </Box>
  )
}

function StageOverlay({
  step,
  imageId,
  imageSize,
  kept,
  rejected,
  windows,
  showRejected,
}: {
  step: PipelineStep | undefined
  imageId: string
  imageSize: { width: number; height: number }
  kept: readonly KeypointShape[]
  rejected: readonly KeypointShape[]
  windows: readonly KeypointDto[]
  showRejected: boolean
}) {
  const draw = useCallback<DrawFunction>(
    (context, transform, size) => {
      if (showRejected) drawKeypoints(context, transform, size, rejected)
      drawKeypoints(context, transform, size, kept)
      for (const k of windows) {
        drawRotatedSquare(
          context,
          { x: k.x * transform.scale + transform.x, y: k.y * transform.scale + transform.y },
          6 * k.scale * transform.scale,
          k.angle,
          tokens.primary,
          4,
        )
      }
    },
    [kept, rejected, windows, showRejected],
  )

  return (
    <ImageStage
      fill
      imageSize={imageSize}
      label={`${step?.title ?? 'Stage'}: keypoints kept and rejected`}
    >
      {(view) => (
        <>
          <StageImage
            src={imageContentUrl(imageId)}
            size={imageSize}
            view={view}
            filter="brightness(0.55) saturate(0.4)"
          />
          <CanvasLayer transform={view.transform} size={view.container} draw={draw} />
        </>
      )}
    </ImageStage>
  )
}
