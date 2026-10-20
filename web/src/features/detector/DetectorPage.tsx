import CompareIcon from '@mui/icons-material/CompareOutlined'
import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import MenuItem from '@mui/material/MenuItem'
import Paper from '@mui/material/Paper'
import Slider from '@mui/material/Slider'
import TextField from '@mui/material/TextField'
import ToggleButton from '@mui/material/ToggleButton'
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup'
import Typography from '@mui/material/Typography'
import { useCallback, useEffect, useMemo, useState, type KeyboardEvent } from 'react'

import { monoFontFamily, tokens } from '../../app/theme'
import { useWorkspace } from '../../app/workspace'
import { imageContentUrl, layerUrl } from '../../shared/api/client'
import {
  useAlgorithms,
  useCreateDetection,
  useDetection,
  useImage,
  useRun,
} from '../../shared/api/queries'
import { useRunProgress } from '../../shared/api/realtime'
import { drawKeypoints, type KeypointShape } from '../../shared/canvas/draw'
import { octaveColor } from '../../shared/colormaps'
import { CanvasLayer, type DrawFunction } from '../../shared/components/CanvasLayer'
import { EmptyState } from '../../shared/components/EmptyState'
import { HeatmapLegend } from '../../shared/components/HeatmapLegend'
import { ImageStage, StageImage } from '../../shared/components/ImageStage'
import { ParameterForm } from '../../shared/components/ParameterForm'
import { RunProgressBar } from '../../shared/components/RunProgressBar'
import { SampleGallery } from '../../shared/components/SampleGallery'
import { Section } from '../../shared/components/Section'
import { normalizeValues, parseSchema } from '../../shared/schema/parameterSchema'
import { SIFT_DETECTION_STAGES } from '../../shared/stages'
import { PointGrid } from '../../shared/viewport/pointGrid'
import { CompareDialog } from './CompareDialog'
import {
  filterFromParameters,
  octaveGroup,
  sameScaleSpace,
  survivalCurve,
  visibleIndices,
} from './filtering'
import { KeypointInspector } from './KeypointInspector'
import { ThresholdHistogram } from './ThresholdHistogram'
import { TimingWaterfall } from './TimingWaterfall'

const HEATMAPS = [
  { id: 'none', label: 'None', title: '' },
  { id: 'heat_dogResponse', label: 'DoG', title: 'DoG response D(x, y, σ)' },
  { id: 'heat_gradientMagnitude', label: 'Gradient', title: 'Gradient magnitude |∇L|' },
  { id: 'heat_edgeRatio', label: 'Edge ratio', title: 'Edge ratio tr²(H) / det(H)' },
  { id: 'heat_contrast', label: 'Contrast', title: 'Contrast |D| · s' },
] as const

const OCTAVE_FILTERS = [
  { value: 'all', label: 'All' },
  { value: '0', label: '1' },
  { value: '1', label: '2' },
  { value: '2', label: '3+' },
] as const

const COMING_SOON = ['Harris', 'FAST']
const HISTOGRAM_BINS = 40

/** Detector view (design artboard "Detector"). */
export function DetectorPage() {
  const {
    imageId,
    algorithmId,
    parameters,
    detectionRunId,
    setParameters,
    setDetectionRun,
    setAlgorithm,
    setImage,
  } = useWorkspace()
  const algorithms = useAlgorithms()
  const image = useImage(imageId)
  const run = useRun(detectionRunId)
  useRunProgress(detectionRunId)
  const detection = useDetection(detectionRunId, run.data)
  const create = useCreateDetection()

  const [heatmap, setHeatmap] = useState<string>('none')
  const [heatOpacity, setHeatOpacity] = useState(0.85)
  const [showKeypoints, setShowKeypoints] = useState(true)
  const [octave, setOctave] = useState<string>('all')
  // The selection belongs to one run; a new run starts without one.
  const [selection, setSelection] = useState<{ runId: string; index: number }>()
  const [compareOpen, setCompareOpen] = useState(false)

  const algorithm = algorithms.data?.find((a) => a.id === algorithmId)
  const fields = useMemo(() => parseSchema(algorithm?.parameterSchema), [algorithm])
  const values = useMemo(
    () => normalizeValues(fields, parameters[algorithmId]),
    [fields, parameters, algorithmId],
  )
  const filter = useMemo(
    () => ({
      ...filterFromParameters(values),
      octaveGroup: octave === 'all' ? undefined : Number(octave),
    }),
    [values, octave],
  )

  const keypoints = detection.data?.keypoints
  const selected = selection && selection.runId === detectionRunId ? selection.index : undefined
  const setSelected = useCallback(
    (index: number | undefined) => {
      setSelection(
        index === undefined || !detectionRunId ? undefined : { runId: detectionRunId, index },
      )
    },
    [detectionRunId],
  )
  const visible = useMemo(
    () => (keypoints ? visibleIndices(keypoints, filter) : []),
    [keypoints, filter],
  )
  const grid = useMemo(() => {
    if (!keypoints) return undefined
    const set = new Set(visible)
    return new PointGrid(keypoints, 16, (i) => set.has(i))
  }, [keypoints, visible])

  const ranParameters = detection.data?.parameters as Record<string, unknown> | undefined
  const stale = ranParameters !== undefined && !sameScaleSpace(ranParameters, values)
  const busy = create.isPending || run.data?.status === 'running' || run.data?.status === 'queued'
  const canCompare =
    algorithmId === 'sift' && Boolean(algorithms.data?.some((a) => a.id === 'opencv-sift'))

  const { mutate, isPending } = create
  const startRun = useCallback(() => {
    if (!imageId || fields.length === 0) return
    mutate(
      { imageId, algorithmId, parameters: values },
      {
        onSuccess: (created) => {
          setDetectionRun(created.runId)
        },
      },
    )
  }, [imageId, algorithmId, values, fields.length, mutate, setDetectionRun])

  // A new image or algorithm starts a detection automatically.
  useEffect(() => {
    if (imageId && !detectionRunId && fields.length > 0 && !isPending) startRun()
  }, [imageId, detectionRunId, fields.length, isPending, startRun])

  const shapes = useMemo<KeypointShape[]>(() => {
    if (!keypoints || !showKeypoints) return []
    return visible.flatMap((i) => {
      const k = keypoints[i]
      return k
        ? [
            {
              x: k.x,
              y: k.y,
              radius: Math.max(k.scale * 1.6, 3) / 2,
              angle: k.angle,
              color: octaveColor(k.octave),
            },
          ]
        : []
    })
  }, [keypoints, visible, showKeypoints])

  const selectedKeypoint = selected !== undefined ? keypoints?.[selected] : undefined
  const draw = useCallback<DrawFunction>(
    (context, transform, size) => {
      drawKeypoints(context, transform, size, shapes)
      if (selectedKeypoint) {
        const x = selectedKeypoint.x * transform.scale + transform.x
        const y = selectedKeypoint.y * transform.scale + transform.y
        const r = Math.max(6 * selectedKeypoint.scale * transform.scale, 8)
        context.strokeStyle = '#ffffff'
        context.lineWidth = 2
        context.shadowColor = 'rgba(255,255,255,.5)'
        context.shadowBlur = 10
        context.strokeRect(x - r, y - r, 2 * r, 2 * r)
        context.shadowBlur = 0
      }
    },
    [shapes, selectedKeypoint],
  )

  const contrastField = fields.find((f) => f.name === 'contrastThreshold')
  const edgeField = fields.find((f) => f.name === 'edgeThreshold')
  const histograms = useMemo(() => {
    if (!keypoints || !contrastField || !edgeField) return undefined
    const range = (min: number, max: number) =>
      Array.from(
        { length: HISTOGRAM_BINS },
        (_, i) => min + ((max - min) * i) / (HISTOGRAM_BINS - 1),
      )
    const contrastValues = range(contrastField.minimum ?? 0, contrastField.maximum ?? 0.1)
    const edgeValues = range(edgeField.minimum ?? 2, edgeField.maximum ?? 30)
    const base = { octaveGroup: filter.octaveGroup }
    return {
      contrast: survivalCurve(keypoints, contrastValues, (t) => ({
        ...base,
        contrastThreshold: t,
        edgeThreshold: filter.edgeThreshold,
      })),
      edge: survivalCurve(keypoints, edgeValues, (r) => ({
        ...base,
        contrastThreshold: filter.contrastThreshold,
        edgeThreshold: r,
      })),
    }
  }, [keypoints, contrastField, edgeField, filter])

  const octaveCounts = useMemo(() => {
    const counts = [0, 0, 0]
    if (keypoints) {
      for (const i of visibleIndices(keypoints, { ...filter, octaveGroup: undefined })) {
        const k = keypoints[i]
        if (k) counts[octaveGroup(k.octave)] = (counts[octaveGroup(k.octave)] ?? 0) + 1
      }
    }
    return counts
  }, [keypoints, filter])

  if (!imageId || !image.data) {
    return (
      <EmptyState
        title="Upload an image to start"
        action={
          <SampleGallery
            onImported={(sample) => {
              setImage(sample.id)
            }}
          />
        }
      >
        Salient detects SIFT keypoints, shows every step of the pipeline and lets you inspect
        individual keypoints. Use “Upload image” in the top bar.
      </EmptyState>
    )
  }

  const stepSelection = (delta: number) => {
    if (visible.length === 0) return
    const position = selected === undefined ? -1 : visible.indexOf(selected)
    setSelected(visible[(position + delta + visible.length) % visible.length])
  }

  const onStageKey = (event: KeyboardEvent) => {
    if (event.key === 'ArrowRight') stepSelection(1)
    else if (event.key === 'ArrowLeft') stepSelection(-1)
    else if (event.key === 'Escape') setSelected(undefined)
  }

  const heat = HEATMAPS.find((h) => h.id === heatmap)
  const heatLayer = run.data?.layers.find((l) => l.id === heatmap)
  const imageInfo = image.data
  const peakOctave = Math.max(1, ...octaveCounts)

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <RunProgressBar
        run={run.data}
        expectedStages={algorithmId === 'sift' ? SIFT_DETECTION_STAGES : ['load', 'opencv']}
        title="Run"
      />
      <Box sx={{ display: 'flex', flexWrap: 'wrap', alignItems: 'flex-start', gap: 2 }}>
        <Paper
          variant="outlined"
          component="section"
          aria-label="Image viewer"
          sx={{ flex: '999 1 640px', minWidth: 0, overflow: 'hidden' }}
        >
          <Box
            sx={{
              display: 'flex',
              flexWrap: 'wrap',
              alignItems: 'center',
              gap: '10px 12px',
              px: 2,
              py: 1.5,
              borderBottom: 1,
              borderColor: 'divider',
            }}
          >
            <Chip
              label={
                <span>
                  {imageInfo.fileName ?? 'image'}{' '}
                  <Box
                    component="span"
                    sx={{ fontFamily: monoFontFamily, color: 'text.secondary', ml: 0.5 }}
                  >
                    {imageInfo.width} × {imageInfo.height}
                  </Box>
                </span>
              }
            />
            <ToggleButtonGroup
              size="small"
              exclusive
              value={heatmap}
              onChange={(_, value: string | null) => {
                if (value) setHeatmap(value)
              }}
              aria-label="Heatmap overlay"
            >
              {HEATMAPS.map((h) => (
                <ToggleButton
                  key={h.id}
                  value={h.id}
                  disabled={h.id !== 'none' && !run.data?.layers.some((l) => l.id === h.id)}
                >
                  {h.label}
                </ToggleButton>
              ))}
            </ToggleButtonGroup>
            {heatmap !== 'none' && (
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, width: 150 }}>
                <Typography variant="caption" color="text.secondary" id="opacity-label">
                  Opacity
                </Typography>
                <Slider
                  size="small"
                  min={0.1}
                  max={1}
                  step={0.05}
                  value={heatOpacity}
                  aria-labelledby="opacity-label"
                  onChange={(_, v) => {
                    setHeatOpacity(Number(Array.isArray(v) ? v[0] : v))
                  }}
                />
              </Box>
            )}
            <ToggleButtonGroup
              size="small"
              exclusive
              value={octave}
              onChange={(_, value: string | null) => {
                if (value) setOctave(value)
              }}
              aria-label="Octave filter"
            >
              {OCTAVE_FILTERS.map((o) => (
                <ToggleButton key={o.value} value={o.value} sx={{ minWidth: 40 }}>
                  {o.label}
                </ToggleButton>
              ))}
            </ToggleButtonGroup>
            <Box sx={{ flex: 1 }} />
            <ToggleButton
              size="small"
              value="keypoints"
              selected={showKeypoints}
              onChange={() => {
                setShowKeypoints((s) => !s)
              }}
            >
              Keypoints
            </ToggleButton>
            {canCompare && (
              <Button
                size="small"
                variant="outlined"
                color="inherit"
                startIcon={<CompareIcon />}
                onClick={() => {
                  setCompareOpen(true)
                }}
                sx={{ borderColor: tokens.borderStrong, color: 'text.secondary' }}
              >
                Compare with OpenCV
              </Button>
            )}
          </Box>
          <Box sx={{ p: 2.5, bgcolor: '#0b0d11', position: 'relative' }}>
            <ImageStage
              imageSize={{ width: imageInfo.width, height: imageInfo.height }}
              label="Image with keypoints. Click a keypoint to inspect it; arrow keys move the selection."
              onImageClick={(p) => {
                setSelected(grid?.nearest(p.x, p.y, 25))
              }}
              onKeyDown={onStageKey}
            >
              {(view) => (
                <>
                  <StageImage
                    src={imageContentUrl(imageId)}
                    size={imageInfo}
                    view={view}
                    alt="Uploaded image"
                    filter="brightness(0.7) saturate(0.6)"
                  />
                  {heatLayer && detectionRunId && (
                    <StageImage
                      src={layerUrl(detectionRunId, heatLayer.id)}
                      size={imageInfo}
                      view={view}
                      opacity={heatOpacity}
                    />
                  )}
                  <CanvasLayer transform={view.transform} size={view.container} draw={draw} />
                  {heatLayer && heat && (
                    <Box sx={{ position: 'absolute', left: 12, bottom: 12 }}>
                      <HeatmapLegend title={heat.title} legend={heatLayer.legend} />
                    </Box>
                  )}
                </>
              )}
            </ImageStage>
          </Box>
          <Box sx={{ px: 2, py: 1.75, borderTop: 1, borderColor: 'divider' }}>
            {run.data && run.data.stages.length > 0 ? (
              <TimingWaterfall stages={run.data.stages} keypoints={visible.length} />
            ) : (
              <Typography variant="caption" color="text.secondary">
                Starting the detector…
              </Typography>
            )}
          </Box>
        </Paper>

        <Box
          component="aside"
          aria-label="Algorithm settings"
          sx={{ flex: '1 1 360px', minWidth: 0, display: 'flex', flexDirection: 'column', gap: 2 }}
        >
          <Section title="Algorithm">
            <TextField
              select
              size="small"
              label="Detector"
              value={algorithms.data ? algorithmId : ''}
              onChange={(e) => {
                setAlgorithm(e.target.value)
              }}
            >
              {algorithms.data?.map((a) => (
                <MenuItem key={a.id} value={a.id}>
                  {a.displayName}
                </MenuItem>
              ))}
            </TextField>
            <Box sx={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 0.75 }}>
              <Typography variant="caption" color="text.secondary" sx={{ mr: 0.5 }}>
                Coming soon:
              </Typography>
              {COMING_SOON.map((name) => (
                <Chip
                  key={name}
                  size="small"
                  label={name}
                  variant="outlined"
                  sx={{ borderStyle: 'dashed', color: 'text.secondary' }}
                />
              ))}
            </Box>
          </Section>

          <Section title="Parameters">
            <ParameterForm
              fields={fields}
              values={values}
              onChange={(name, value) => {
                setParameters(algorithmId, { ...values, [name]: value })
              }}
              above={
                histograms && contrastField && edgeField
                  ? {
                      contrastThreshold: (
                        <ThresholdHistogram
                          counts={histograms.contrast}
                          min={contrastField.minimum ?? 0}
                          max={contrastField.maximum ?? 0.1}
                          value={Number(values.contrastThreshold)}
                          currentCount={visible.length}
                        />
                      ),
                      edgeThreshold: (
                        <ThresholdHistogram
                          counts={histograms.edge}
                          min={edgeField.minimum ?? 2}
                          max={edgeField.maximum ?? 30}
                          value={Number(values.edgeThreshold)}
                          currentCount={visible.length}
                          height={40}
                        />
                      ),
                    }
                  : undefined
              }
            />
            <Button
              variant="contained"
              size="large"
              startIcon={<PlayArrowIcon />}
              onClick={startRun}
              disabled={busy}
              color={stale ? 'secondary' : 'primary'}
            >
              {busy ? 'Running…' : stale ? 'Run with new scale space' : 'Run again'}
            </Button>
            <Typography variant="caption" color="text.secondary">
              Thresholds apply instantly; other parameters need a new run.
            </Typography>
            {create.error && (
              <Typography color="error" variant="body2" role="alert">
                {create.error.message}
              </Typography>
            )}
          </Section>

          {keypoints && (
            <Section
              title="Result"
              action={
                <Typography sx={{ fontFamily: monoFontFamily, fontSize: 22 }}>
                  {visible.length}
                </Typography>
              }
            >
              {octaveCounts.map((count, i) => {
                const label = OCTAVE_FILTERS[i + 1]?.label ?? String(i + 1)
                return (
                  <Box
                    key={label}
                    sx={{
                      display: 'grid',
                      gridTemplateColumns: '76px 1fr 40px',
                      alignItems: 'center',
                      gap: 1.25,
                    }}
                  >
                    <Typography variant="body2" color="text.secondary">
                      Octave {label}
                    </Typography>
                    <Box
                      sx={{ height: 8, borderRadius: 1, bgcolor: '#222733', overflow: 'hidden' }}
                    >
                      <Box
                        sx={{
                          height: '100%',
                          width: `${(count / peakOctave) * 100}%`,
                          bgcolor: octaveColor(i),
                          borderRadius: 1,
                        }}
                      />
                    </Box>
                    <Typography
                      sx={{ fontFamily: monoFontFamily, fontSize: 13, textAlign: 'right' }}
                    >
                      {count}
                    </Typography>
                  </Box>
                )
              })}
            </Section>
          )}

          {selected !== undefined && selectedKeypoint && detectionRunId && (
            <KeypointInspector
              runId={detectionRunId}
              imageId={imageId}
              index={selected}
              keypoint={selectedKeypoint}
              onPrevious={() => {
                stepSelection(-1)
              }}
              onNext={() => {
                stepSelection(1)
              }}
            />
          )}
        </Box>
      </Box>
      {canCompare && compareOpen && (
        <CompareDialog
          open={compareOpen}
          onClose={() => {
            setCompareOpen(false)
          }}
          image={imageInfo}
          parameters={values}
        />
      )}
    </Box>
  )
}
