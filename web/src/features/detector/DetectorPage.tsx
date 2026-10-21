import CompareIcon from '@mui/icons-material/CompareOutlined'
import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import MenuItem from '@mui/material/MenuItem'
import Paper from '@mui/material/Paper'
import TextField from '@mui/material/TextField'
import ToggleButton from '@mui/material/ToggleButton'
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup'
import Tooltip from '@mui/material/Tooltip'
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
import { fillCardSx, sideColumnSx, viewSx, VIEW_GAP } from '../../shared/layout'
import { defaultValues, normalizeValues, parseSchema } from '../../shared/schema/parameterSchema'
import { SIFT_DETECTION_STAGES } from '../../shared/stages'
import { PointGrid } from '../../shared/viewport/pointGrid'
import { CompareDialog } from './CompareDialog'
import { filterFromParameters, sameScaleSpace, survivalCurve, visibleIndices } from './filtering'
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

  return (
    <Box sx={viewSx}>
      <RunProgressBar
        run={run.data}
        expectedStages={algorithmId === 'sift' ? SIFT_DETECTION_STAGES : ['load', 'opencv']}
        title="Run"
        detailsLabel="Show timing breakdown"
        details={
          run.data && run.data.stages.length > 0 ? (
            <TimingWaterfall stages={run.data.stages} keypoints={visible.length} />
          ) : undefined
        }
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
          <Paper variant="outlined" component="section" aria-label="Image viewer" sx={fillCardSx}>
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
                    sx={{ whiteSpace: 'nowrap' }}
                  >
                    {h.label}
                  </ToggleButton>
                ))}
              </ToggleButtonGroup>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <Typography variant="body2" color="text.secondary" id="octave-label">
                  Octave
                </Typography>
                <ToggleButtonGroup
                  size="small"
                  exclusive
                  value={octave}
                  onChange={(_, value: string | null) => {
                    if (value) setOctave(value)
                  }}
                  aria-labelledby="octave-label"
                >
                  {OCTAVE_FILTERS.map((o) => (
                    <ToggleButton key={o.value} value={o.value} sx={{ minWidth: 40 }}>
                      {o.label}
                    </ToggleButton>
                  ))}
                </ToggleButtonGroup>
              </Box>
              <Box sx={{ flex: '1 1 0 !important' }} />
              <ToggleButton
                size="small"
                value="keypoints"
                selected={showKeypoints}
                onChange={() => {
                  setShowKeypoints((s) => !s)
                }}
                sx={{ gap: 1, whiteSpace: 'nowrap' }}
              >
                Keypoints
                <Box component="span" sx={{ fontFamily: monoFontFamily, color: 'text.secondary' }}>
                  {visible.length}
                </Box>
              </ToggleButton>
              {canCompare && (
                <Tooltip title="Compare with OpenCV">
                  <Button
                    size="small"
                    variant="outlined"
                    color="inherit"
                    aria-label="Compare with OpenCV"
                    startIcon={<CompareIcon />}
                    onClick={() => {
                      setCompareOpen(true)
                    }}
                    sx={{ borderColor: tokens.borderStrong, color: 'text.secondary' }}
                  >
                    Compare
                  </Button>
                </Tooltip>
              )}
            </Box>
            <Box
              sx={{ flex: '1 1 0', minHeight: 0, p: 1.5, bgcolor: '#0b0d11', position: 'relative' }}
            >
              <ImageStage
                fill
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
                  </>
                )}
              </ImageStage>
              {heatLayer && heat && (
                <Box sx={{ position: 'absolute', left: 24, bottom: 24 }}>
                  <HeatmapLegend
                    title={heat.title}
                    legend={heatLayer.legend}
                    opacity={{ value: heatOpacity, onChange: setHeatOpacity }}
                  />
                </Box>
              )}
            </Box>
          </Paper>

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

        <Box component="aside" aria-label="Algorithm settings" sx={sideColumnSx(360)}>
          <Section title="Algorithm" gap={1.25}>
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
            <Typography variant="caption" color="text.secondary">
              Coming soon: {COMING_SOON.join(' · ')}
            </Typography>
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
                          height={72}
                        />
                      ),
                      edgeThreshold: (
                        <ThresholdHistogram
                          counts={histograms.edge}
                          min={edgeField.minimum ?? 2}
                          max={edgeField.maximum ?? 30}
                          value={Number(values.edgeThreshold)}
                          currentCount={visible.length}
                          height={48}
                        />
                      ),
                    }
                  : undefined
              }
            />
            <Box sx={{ display: 'flex', gap: 1 }}>
              <Tooltip title="Thresholds apply instantly; other parameters need a new run.">
                <Box component="span" sx={{ flex: 1, display: 'flex' }}>
                  <Button
                    variant="contained"
                    size="large"
                    fullWidth
                    startIcon={<PlayArrowIcon />}
                    onClick={startRun}
                    disabled={busy}
                    color={stale ? 'secondary' : 'primary'}
                  >
                    {busy ? 'Running…' : stale ? 'Run with new scale space' : 'Run again'}
                  </Button>
                </Box>
              </Tooltip>
              <Button
                size="large"
                onClick={() => {
                  setParameters(algorithmId, defaultValues(fields))
                }}
              >
                Reset
              </Button>
            </Box>
            {create.error && (
              <Typography color="error" variant="body2" role="alert">
                {create.error.message}
              </Typography>
            )}
          </Section>
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
