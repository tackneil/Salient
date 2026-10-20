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
  type CandidateDto,
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
import { normalizeValues, parseSchema } from '../../shared/schema/parameterSchema'
import { filterFromParameters, maxEdgeRatio, visibleIndices } from '../detector/filtering'
import { PIPELINE_STEPS, sourceBaseUrl, type PipelineStep } from './steps'

const REJECTED = '#ff7a93'
const MAX_OCTAVES_SHOWN = 3
const DESCRIPTOR_WINDOWS = 18

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
  const thresholds = filterFromParameters(values)
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
  const keypoints = detection.data?.keypoints ?? []
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

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Paper variant="outlined" component="nav" aria-label="Pipeline stages" sx={{ p: 1 }}>
        <Box
          component="ol"
          sx={{ listStyle: 'none', m: 0, p: 0, display: 'flex', flexWrap: 'wrap', gap: 0.5 }}
        >
          {PIPELINE_STEPS.map((s, i) => {
            const current = i === stepIndex
            const ms = s.stageIds.reduce((sum, id) => sum + (stageCount(id)?.durationMs ?? 0), 0)
            return (
              <Box component="li" key={s.id} sx={{ flex: '1 1 150px', minWidth: 0 }}>
                <ButtonBase
                  aria-current={current ? 'step' : undefined}
                  onClick={() => {
                    setStepIndex(i)
                  }}
                  sx={{
                    width: '100%',
                    justifyContent: 'flex-start',
                    gap: 1.25,
                    minHeight: 52,
                    px: 1.5,
                    py: 1,
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

      <Box sx={{ display: 'flex', flexWrap: 'wrap', alignItems: 'flex-start', gap: 2 }}>
        <Paper
          variant="outlined"
          component="section"
          aria-label="Stage visualization"
          sx={{ flex: '999 1 640px', minWidth: 0, overflow: 'hidden' }}
        >
          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: 1.5,
              px: 2,
              py: 1.5,
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
            <Typography sx={{ p: 4 }} color="text.secondary">
              Waiting for the run to finish…
            </Typography>
          ) : step?.id === 'pyramid' || step?.id === 'dog' ? (
            <LayerGrid
              runId={detectionRunId}
              layers={run.data?.layers ?? []}
              kind={step.id === 'pyramid' ? 'gaussian' : 'dog'}
            />
          ) : (
            <Box sx={{ p: 2.5, bgcolor: '#0b0d11' }}>
              <StageOverlay
                step={step}
                imageId={imageId}
                imageSize={image.data}
                candidates={candidates.data ?? []}
                keypoints={keypoints}
                thresholds={thresholds}
                showRejected={showRejected}
              />
            </Box>
          )}

          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: 1.5,
              px: 2,
              py: 1.5,
              borderTop: 1,
              borderColor: 'divider',
            }}
          >
            <Typography
              sx={{ fontFamily: monoFontFamily, fontSize: 12, color: 'text.secondary', flex: 1 }}
            >
              Thresholds from the detector: contrast {thresholds.contrastThreshold?.toFixed(4)}, r ={' '}
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

        <Box
          component="aside"
          aria-label="Stage explanation"
          sx={{ flex: '1 1 340px', minWidth: 0, display: 'flex', flexDirection: 'column', gap: 2 }}
        >
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

function passesUserThresholds(
  c: CandidateDto,
  t: { contrastThreshold?: number; edgeThreshold?: number },
) {
  if (c.contrast === null || c.contrast < (t.contrastThreshold ?? 0)) return false
  return c.edgeRatio !== null && c.edgeRatio < maxEdgeRatio(t.edgeThreshold ?? 10)
}

function Explanation({ step }: { step: PipelineStep }) {
  return (
    <Section title="What happens">
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

/** Octave × layer tiles of Gaussian or DoG layers. */
function LayerGrid({
  runId,
  layers,
  kind,
}: {
  runId: string
  layers: readonly LayerInfo[]
  kind: 'gaussian' | 'dog'
}) {
  const ofKind = layers.filter((l) => l.kind === kind && l.octave !== null && l.layer !== null)
  const octaves = [...new Set(ofKind.map((l) => l.octave ?? 0))]
    .sort((a, b) => a - b)
    .slice(0, MAX_OCTAVES_SHOWN)
  const columns = Math.max(1, ...ofKind.map((l) => (l.layer ?? 0) + 1))
  return (
    <Box sx={{ p: 2, display: 'flex', flexDirection: 'column', gap: 2, bgcolor: '#0b0d11' }}>
      {octaves.map((octave) => {
        const row = ofKind
          .filter((l) => l.octave === octave)
          .sort((a, b) => (a.layer ?? 0) - (b.layer ?? 0))
        const first = row[0]
        return (
          <Box key={octave} sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
            <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 1.25 }}>
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
                display: 'grid',
                gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))`,
                gap: 1,
              }}
            >
              {row.map((layer) => (
                <Box
                  component="figure"
                  key={layer.id}
                  sx={{ m: 0, display: 'flex', flexDirection: 'column', gap: 0.5 }}
                >
                  <Box
                    component="img"
                    loading="lazy"
                    src={layerUrl(runId, layer.id)}
                    alt={`${kind === 'gaussian' ? 'Gaussian' : 'DoG'} layer ${layer.layer ?? 0} of octave ${octave}`}
                    sx={{
                      width: '100%',
                      aspectRatio: `${layer.width} / ${layer.height}`,
                      objectFit: 'cover',
                      display: 'block',
                      borderRadius: 1,
                      border: 1,
                      borderColor: 'divider',
                      imageRendering: layer.width < 300 ? 'pixelated' : 'auto',
                    }}
                  />
                  <Typography
                    component="figcaption"
                    sx={{ fontFamily: monoFontFamily, fontSize: 11, color: 'text.secondary' }}
                  >
                    {kind === 'gaussian'
                      ? `σ = ${layer.sigma.toFixed(2)}`
                      : `D${(layer.layer ?? 0) + 1} · σ ${layer.sigma.toFixed(2)}`}
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
  candidates,
  keypoints,
  thresholds,
  showRejected,
}: {
  step: PipelineStep | undefined
  imageId: string
  imageSize: { width: number; height: number }
  candidates: readonly CandidateDto[]
  keypoints: ReturnType<typeof useDetection>['data'] extends infer D
    ? D extends { keypoints: infer K }
      ? K
      : never
    : never
  thresholds: { contrastThreshold?: number; edgeThreshold?: number }
  showRejected: boolean
}) {
  const shapes = useMemo(() => {
    const kept: KeypointShape[] = []
    const rejected: KeypointShape[] = []
    const add = (
      list: KeypointShape[],
      x: number,
      y: number,
      scale: number,
      color: string,
      dashed = false,
      angle?: number,
    ) => {
      list.push({ x, y, radius: Math.max(scale * 1.6, 3) / 2, color, dashed, angle })
    }
    if (step?.id === 'extrema') {
      for (const c of candidates)
        add(
          c.rejection ? rejected : kept,
          c.x,
          c.y,
          c.scale,
          c.rejection ? REJECTED : tokens.keypoint,
          Boolean(c.rejection),
        )
    } else if (step?.id === 'contrast') {
      for (const c of candidates) {
        const ok = c.contrast !== null && c.contrast >= (thresholds.contrastThreshold ?? 0)
        add(ok ? kept : rejected, c.x, c.y, c.scale, ok ? tokens.keypoint : REJECTED, !ok)
      }
    } else if (step?.id === 'edge') {
      for (const c of candidates) {
        if (c.contrast === null || c.contrast < (thresholds.contrastThreshold ?? 0)) continue
        const ok = passesUserThresholds(c, thresholds)
        add(ok ? kept : rejected, c.x, c.y, c.scale, ok ? tokens.keypoint : REJECTED, !ok)
      }
    } else {
      for (const i of visibleIndices(keypoints, thresholds)) {
        const k = keypoints[i]
        if (k)
          add(
            kept,
            k.x,
            k.y,
            k.scale,
            step?.id === 'descriptor' ? 'rgba(255,181,71,0.35)' : tokens.keypoint,
            false,
            k.angle,
          )
      }
    }
    return { kept, rejected }
  }, [step, candidates, keypoints, thresholds])

  const windows = useMemo(() => {
    if (step?.id !== 'descriptor') return []
    return visibleIndices(keypoints, thresholds)
      .flatMap((i) => (keypoints[i] ? [keypoints[i]] : []))
      .sort((a, b) => b.scale * b.response - a.scale * a.response)
      .slice(0, DESCRIPTOR_WINDOWS)
  }, [step, keypoints, thresholds])

  const draw = useCallback<DrawFunction>(
    (context, transform, size) => {
      if (showRejected) drawKeypoints(context, transform, size, shapes.rejected)
      drawKeypoints(context, transform, size, shapes.kept)
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
    [shapes, windows, showRejected],
  )

  const label =
    step?.id === 'descriptor'
      ? `${shapes.kept.length} descriptors · ${windows.length} descriptor windows shown`
      : `${shapes.kept.length} kept · ${shapes.rejected.length} rejected at this stage`

  return (
    <>
      <ImageStage
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
      <Typography sx={{ mt: 1, fontFamily: monoFontFamily, fontSize: 12, color: 'text.secondary' }}>
        {label}
      </Typography>
    </>
  )
}
