import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Paper from '@mui/material/Paper'
import Typography from '@mui/material/Typography'
import { useCallback, useMemo } from 'react'

import { useWorkspace } from '../../app/workspace'
import { imageContentUrl } from '../../shared/api/client'
import {
  useAlgorithms,
  useCreateDetection,
  useDetection,
  useImage,
  useRun,
} from '../../shared/api/queries'
import { useRunProgress } from '../../shared/api/realtime'
import { drawKeypoints, type KeypointShape } from '../../shared/canvas/draw'
import { CanvasLayer } from '../../shared/components/CanvasLayer'
import { EmptyState } from '../../shared/components/EmptyState'
import { ImageStage, StageImage } from '../../shared/components/ImageStage'
import { ParameterForm } from '../../shared/components/ParameterForm'
import { RunProgressBar } from '../../shared/components/RunProgressBar'
import { Section } from '../../shared/components/Section'
import { octaveColor } from '../../shared/colormaps'
import { normalizeValues, parseSchema } from '../../shared/schema/parameterSchema'
import { filterFromParameters, visibleIndices } from './filtering'

const SIFT_STAGES = [
  'load',
  'gaussian-pyramid',
  'dog',
  'extrema',
  'refine',
  'contrast',
  'edge',
  'orientation',
  'heatmaps',
  'descriptor',
]

/** Detector view: image with keypoints, algorithm parameters and live run progress. */
export function DetectorPage() {
  const { imageId, algorithmId, parameters, detectionRunId, setParameters, setDetectionRun } =
    useWorkspace()
  const algorithms = useAlgorithms()
  const image = useImage(imageId)
  const run = useRun(detectionRunId)
  useRunProgress(detectionRunId)
  const detection = useDetection(detectionRunId, run.data)
  const create = useCreateDetection()

  const algorithm = algorithms.data?.find((a) => a.id === algorithmId)
  const fields = useMemo(() => parseSchema(algorithm?.parameterSchema), [algorithm])
  const values = useMemo(
    () => normalizeValues(fields, parameters[algorithmId]),
    [fields, parameters, algorithmId],
  )

  const keypoints = detection.data?.keypoints
  const shapes = useMemo<KeypointShape[]>(() => {
    if (!keypoints) return []
    return visibleIndices(keypoints, filterFromParameters(values)).flatMap((i) => {
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
  }, [keypoints, values])

  const draw = useCallback(
    (
      context: CanvasRenderingContext2D,
      transform: Parameters<typeof drawKeypoints>[1],
      size: Parameters<typeof drawKeypoints>[2],
    ) => {
      drawKeypoints(context, transform, size, shapes)
    },
    [shapes],
  )

  if (!imageId || !image.data) {
    return (
      <EmptyState title="Upload an image to start">
        Salient detects SIFT keypoints, shows every step of the pipeline and lets you inspect
        individual keypoints. Use “Upload image” in the top bar.
      </EmptyState>
    )
  }

  const runDetection = () => {
    create.mutate(
      { imageId, algorithmId, parameters: values },
      {
        onSuccess: (created) => {
          setDetectionRun(created.runId)
        },
      },
    )
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <RunProgressBar run={run.data} expectedStages={SIFT_STAGES} title="Run" />
      <Box sx={{ display: 'flex', flexWrap: 'wrap', alignItems: 'flex-start', gap: 2 }}>
        <Paper
          variant="outlined"
          sx={{ flex: '999 1 640px', minWidth: 0, p: 2.5, bgcolor: '#0b0d11' }}
        >
          <ImageStage
            imageSize={{ width: image.data.width, height: image.data.height }}
            label="Image with keypoints"
          >
            {(view) => (
              <>
                <StageImage
                  src={imageContentUrl(imageId)}
                  size={image.data}
                  view={view}
                  alt="Uploaded image"
                  filter="brightness(0.7) saturate(0.6)"
                />
                <CanvasLayer transform={view.transform} size={view.container} draw={draw} />
              </>
            )}
          </ImageStage>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1 }}>
            {keypoints
              ? `${String(shapes.length)} of ${String(keypoints.length)} keypoints shown`
              : 'Run the detector to see keypoints.'}
          </Typography>
        </Paper>
        <Box
          sx={{ flex: '1 1 340px', minWidth: 0, display: 'flex', flexDirection: 'column', gap: 2 }}
        >
          <Section title="Parameters">
            <ParameterForm
              fields={fields}
              values={values}
              onChange={(name, value) => {
                setParameters(algorithmId, { ...values, [name]: value })
              }}
            />
            <Button
              variant="contained"
              size="large"
              startIcon={<PlayArrowIcon />}
              onClick={runDetection}
              disabled={
                create.isPending || run.data?.status === 'running' || run.data?.status === 'queued'
              }
            >
              Run
            </Button>
            {create.error && (
              <Typography color="error" variant="body2">
                {create.error.message}
              </Typography>
            )}
          </Section>
        </Box>
      </Box>
    </Box>
  )
}
