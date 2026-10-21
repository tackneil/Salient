import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Checkbox from '@mui/material/Checkbox'
import FormControlLabel from '@mui/material/FormControlLabel'
import FormGroup from '@mui/material/FormGroup'
import Paper from '@mui/material/Paper'
import Slider from '@mui/material/Slider'
import Tab from '@mui/material/Tab'
import Tabs from '@mui/material/Tabs'
import ToggleButton from '@mui/material/ToggleButton'
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup'
import Typography from '@mui/material/Typography'
import { ChartsReferenceLine } from '@mui/x-charts/ChartsReferenceLine'
import { LineChart } from '@mui/x-charts/LineChart'
import { useMemo, useState } from 'react'
import { Link as RouterLink } from 'react-router'

import { monoFontFamily, tokens } from '../../app/theme'
import { useWorkspace } from '../../app/workspace'
import {
  imageContentUrl,
  type SweepResult,
  type TransformationFamily,
} from '../../shared/api/client'
import {
  useAlgorithms,
  useBenchmark,
  useCreateBenchmark,
  useImage,
  useRun,
} from '../../shared/api/queries'
import { useRunProgress } from '../../shared/api/realtime'
import { EmptyState } from '../../shared/components/EmptyState'
import { RunProgressBar } from '../../shared/components/RunProgressBar'
import { fillCardSx, sideColumnSx, viewSx, VIEW_GAP } from '../../shared/layout'

const FAMILIES: {
  id: TransformationFamily
  label: string
  parameter: string
  format: (v: number) => string
}[] = [
  { id: 'rotation', label: 'Rotation', parameter: 'Angle', format: (v) => `${v}°` },
  { id: 'scale', label: 'Scale', parameter: 'Scale', format: (v) => `×${v.toFixed(2)}` },
  { id: 'blur', label: 'Blur', parameter: 'Blur σ', format: (v) => `σ ${v}` },
  { id: 'noise', label: 'Noise', parameter: 'Noise σ', format: (v) => `σ ${v}` },
  {
    id: 'brightness',
    label: 'Brightness',
    parameter: 'Brightness shift',
    format: (v) => (v > 0 ? `+${v}` : `${v}`),
  },
]

const METRICS = [
  {
    id: 'matchingScore',
    label: 'Correct matches',
    description: 'Correct ratio-test matches per visible reference keypoint',
  },
  {
    id: 'repeatability',
    label: 'Repeatability',
    description: 'Keypoints re-detected within 3 px at a consistent scale',
  },
  {
    id: 'matchPrecision',
    label: 'Precision',
    description: 'Share of ratio-test matches that are correct',
  },
] as const

type MetricId = (typeof METRICS)[number]['id']
const SERIES_COLORS = [tokens.keypoint, tokens.primary, '#c792ea', '#5fd08f']

/** Robustness view (design artboard "Robustness bench"). */
export function RobustnessPage() {
  const { imageId, benchmarkRunId, setBenchmarkRun } = useWorkspace()
  const image = useImage(imageId)
  const algorithms = useAlgorithms()
  const create = useCreateBenchmark()
  const run = useRun(benchmarkRunId)
  useRunProgress(benchmarkRunId)
  const benchmark = useBenchmark(benchmarkRunId, run.data)

  const describing = useMemo(
    () => algorithms.data?.filter((a) => a.capabilities.includes('Describes')) ?? [],
    [algorithms.data],
  )
  const [chosen, setChosen] = useState<string[]>()
  const selectedAlgorithms = chosen ?? describing.map((a) => a.id)
  const [familyId, setFamilyId] = useState<TransformationFamily>('rotation')
  const [valueIndex, setValueIndex] = useState(3)
  const [metric, setMetric] = useState<MetricId>('matchingScore')
  const [hidden, setHidden] = useState<string[]>([])

  if (!imageId || !image.data) {
    return (
      <EmptyState
        title="Upload an image first"
        action={
          <Button component={RouterLink} to="/detector" variant="contained">
            Go to the detector
          </Button>
        }
      >
        The bench distorts the current image and measures how well each detector finds and matches
        the same points.
      </EmptyState>
    )
  }

  const family = FAMILIES.find((f) => f.id === familyId) ?? FAMILIES[0]
  const sweep: SweepResult | undefined = benchmark.data?.families.find((f) => f.family === familyId)
  const values = sweep?.values ?? []
  const index = Math.min(valueIndex, Math.max(0, values.length - 1))
  const value = values[index] ?? 0
  const busy = create.isPending || run.data?.status === 'running' || run.data?.status === 'queued'
  const aspect = image.data.width / image.data.height

  const runSweep = () => {
    create.mutate(
      { imageId, algorithmIds: selectedAlgorithms, families: null },
      {
        onSuccess: (created) => {
          setBenchmarkRun(created.runId)
        },
      },
    )
  }

  return (
    <Box sx={viewSx}>
      <Paper
        variant="outlined"
        component="section"
        aria-label="Sweep settings"
        sx={{
          flex: '0 0 auto',
          px: 2,
          py: 0.5,
          display: 'flex',
          flexWrap: 'nowrap',
          alignItems: 'center',
          gap: 3,
          overflowX: 'auto',
        }}
      >
        <FormGroup row aria-label="Detectors" sx={{ flexWrap: 'nowrap' }}>
          {describing.map((a) => (
            <FormControlLabel
              key={a.id}
              label={a.displayName}
              sx={{ whiteSpace: 'nowrap' }}
              control={
                <Checkbox
                  size="small"
                  checked={selectedAlgorithms.includes(a.id)}
                  onChange={(_, checked) => {
                    setChosen(
                      checked
                        ? [...selectedAlgorithms, a.id]
                        : selectedAlgorithms.filter((id) => id !== a.id),
                    )
                  }}
                />
              }
            />
          ))}
        </FormGroup>
        <Box sx={{ flex: 1 }} />
        <Button
          variant="contained"
          startIcon={<PlayArrowIcon />}
          onClick={runSweep}
          disabled={busy || selectedAlgorithms.length === 0}
          sx={{ flex: '0 0 auto', whiteSpace: 'nowrap' }}
        >
          {busy ? 'Sweeping…' : 'Run sweep'}
        </Button>
      </Paper>

      {run.data && run.data.status !== 'completed' && (
        <RunProgressBar run={run.data} expectedStages={['load', 'sweep']} title="Sweep" />
      )}

      <Paper
        variant="outlined"
        sx={{
          flex: '0 0 auto',
          px: 2,
          py: 0.5,
          display: 'flex',
          flexWrap: 'nowrap',
          alignItems: 'center',
          gap: 3,
          overflowX: 'auto',
        }}
      >
        <Tabs
          value={familyId}
          onChange={(_, v: TransformationFamily) => {
            setFamilyId(v)
            setValueIndex(0)
          }}
          aria-label="Transformation family"
          sx={{ flex: '0 0 auto', minHeight: 40, '& .MuiTab-root': { minHeight: 40 } }}
        >
          {FAMILIES.map((f) => (
            <Tab key={f.id} value={f.id} label={f.label} />
          ))}
        </Tabs>
        <Box
          sx={{ flex: '1 0 280px', display: 'flex', alignItems: 'center', gap: 1.5, minWidth: 240 }}
        >
          <Typography
            id="value-label"
            variant="body2"
            color="text.secondary"
            sx={{ whiteSpace: 'nowrap' }}
          >
            {family?.parameter}
          </Typography>
          <Slider
            size="small"
            min={0}
            max={Math.max(0, values.length - 1)}
            step={1}
            value={index}
            disabled={values.length === 0}
            aria-labelledby="value-label"
            aria-valuetext={family?.format(value)}
            onChange={(_, v) => {
              setValueIndex(Number(Array.isArray(v) ? v[0] : v))
            }}
          />
          <Typography
            sx={{
              fontFamily: monoFontFamily,
              fontSize: 14,
              color: 'primary.main',
              minWidth: 64,
              textAlign: 'right',
            }}
          >
            {values.length > 0 ? family?.format(value) : '—'}
          </Typography>
        </Box>
      </Paper>

      <Box sx={{ flex: '1 1 0', minHeight: 0, display: 'flex', gap: VIEW_GAP }}>
        <Paper
          variant="outlined"
          component="aside"
          aria-label="Image pair and values"
          sx={{ ...sideColumnSx(380), p: 2, gap: 1.5, boxSizing: 'border-box' }}
        >
          <Typography
            variant="overline"
            component="h2"
            color="text.secondary"
            sx={{ lineHeight: 1.5 }}
          >
            Pair
          </Typography>
          <Preview imageId={imageId} aspect={aspect} caption="A · reference" />
          <Preview
            imageId={imageId}
            aspect={aspect}
            caption={`B · ${family?.label.toLowerCase() ?? ''} ${values.length > 0 ? (family?.format(value) ?? '') : ''}`}
            family={familyId}
            value={value}
          />
          {sweep && <ValueTable sweep={sweep} index={index} label={family?.format(value) ?? ''} />}
        </Paper>

        <Paper
          variant="outlined"
          component="section"
          aria-label="Robustness chart"
          sx={{ ...fillCardSx, p: 2, gap: 1.5 }}
        >
          <Box
            sx={{
              flex: '0 0 auto',
              display: 'flex',
              flexWrap: 'nowrap',
              alignItems: 'center',
              gap: 2,
            }}
          >
            <Typography component="h2" noWrap sx={{ fontSize: 16, fontWeight: 600, flex: 1 }}>
              {METRICS.find((m) => m.id === metric)?.label} vs {family?.parameter.toLowerCase()}
            </Typography>
            <ToggleButtonGroup
              size="small"
              exclusive
              value={metric}
              onChange={(_, v: MetricId | null) => {
                if (v) setMetric(v)
              }}
              aria-label="Metric"
              sx={{ flex: '0 0 auto' }}
            >
              {METRICS.map((m) => (
                <ToggleButton key={m.id} value={m.id} sx={{ whiteSpace: 'nowrap' }}>
                  {m.label}
                </ToggleButton>
              ))}
            </ToggleButtonGroup>
          </Box>
          {sweep ? (
            <SweepChart
              sweep={sweep}
              metric={metric}
              value={value}
              format={family?.format ?? String}
              hidden={hidden}
              onToggle={(id) => {
                setHidden((h) => (h.includes(id) ? h.filter((x) => x !== id) : [...h, id]))
              }}
            />
          ) : (
            <Typography
              color="text.secondary"
              sx={{ flex: 1, display: 'grid', placeItems: 'center', textAlign: 'center' }}
            >
              {busy
                ? 'The sweep is running; curves appear when it finishes.'
                : 'Run a sweep to compare detectors on this image.'}
            </Typography>
          )}
          {sweep && (
            <Insight
              sweep={sweep}
              index={index}
              label={family?.format(value) ?? ''}
              format={family?.format ?? String}
            />
          )}
          <Typography variant="caption" color="text.secondary" sx={{ flex: '0 0 auto' }}>
            A match is correct when the matched keypoint lies within 3 px of the ground-truth
            projection. Ratio test 0.8. {METRICS.find((m) => m.id === metric)?.description}.
          </Typography>
        </Paper>
      </Box>
    </Box>
  )
}

/** One image of the pair, fitted into the height it is given; the transform shows what the sweep applies. */
function Preview({
  imageId,
  aspect,
  caption,
  family,
  value,
}: {
  imageId: string
  aspect: number
  caption: string
  family?: TransformationFamily
  value?: number
}) {
  const v = value ?? 0
  const transform =
    family === 'rotation' ? `rotate(${-v}deg)` : family === 'scale' ? `scale(${v})` : 'none'
  const filter =
    family === 'blur'
      ? `blur(${(v / 2.5).toFixed(2)}px)`
      : family === 'brightness'
        ? `brightness(${((128 + v) / 128).toFixed(2)})`
        : 'none'
  return (
    <Box
      component="figure"
      sx={{
        m: 0,
        flex: '1 1 0',
        minHeight: 96,
        display: 'flex',
        flexDirection: 'column',
        gap: 0.5,
      }}
    >
      <Box
        sx={{
          flex: '1 1 0',
          minHeight: 0,
          containerType: 'size',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <Box
          data-clips-by-design
          sx={{
            position: 'relative',
            flex: '0 0 auto',
            width: `min(100cqw, ${String(aspect * 100)}cqh)`,
            aspectRatio: String(aspect),
            borderRadius: 1,
            overflow: 'hidden',
            bgcolor: '#000',
          }}
        >
          <Box
            component="img"
            src={imageContentUrl(imageId)}
            alt={caption}
            sx={{
              position: 'absolute',
              inset: 0,
              width: '100%',
              height: '100%',
              objectFit: 'cover',
              transform,
              filter,
            }}
          />
          <Typography
            component="figcaption"
            variant="caption"
            sx={{
              position: 'absolute',
              left: 8,
              bottom: 8,
              px: 1,
              py: 0.25,
              borderRadius: 1,
              bgcolor: 'rgba(11,13,17,0.8)',
              color: 'text.primary',
            }}
          >
            {caption}
          </Typography>
          {family === 'noise' && (
            <Box
              aria-hidden
              sx={{
                position: 'absolute',
                inset: 0,
                opacity: Math.min(0.9, v / 40),
                backgroundImage: 'repeating-conic-gradient(#fff 0 25%, #000 0 50%)',
                backgroundSize: '3px 3px',
                mixBlendMode: 'overlay',
              }}
            />
          )}
        </Box>
      </Box>
    </Box>
  )
}

/** Detector toggles above a chart that fills the rest of the card. */
function SweepChart({
  sweep,
  metric,
  value,
  format,
  hidden,
  onToggle,
}: {
  sweep: SweepResult
  metric: MetricId
  value: number
  format: (v: number) => string
  hidden: readonly string[]
  onToggle: (id: string) => void
}) {
  const series = sweep.series
    .map((s, i) => ({ s, color: SERIES_COLORS[i % SERIES_COLORS.length] ?? tokens.keypoint }))
    .filter(({ s }) => !hidden.includes(s.extractorId))
  return (
    <Box sx={{ flex: '1 1 0', minHeight: 0, display: 'flex', flexDirection: 'column', gap: 1 }}>
      <Box
        sx={{ flex: '0 0 auto', display: 'flex', flexWrap: 'nowrap', gap: 0.75 }}
        role="group"
        aria-label="Detectors shown"
      >
        {sweep.series.map((s, i) => {
          const on = !hidden.includes(s.extractorId)
          return (
            <ToggleButton
              key={s.extractorId}
              size="small"
              value={s.extractorId}
              selected={on}
              onChange={() => {
                onToggle(s.extractorId)
              }}
              sx={{ gap: 1, whiteSpace: 'nowrap' }}
            >
              <Box
                sx={{
                  width: 18,
                  borderTop: `2.5px solid ${SERIES_COLORS[i % SERIES_COLORS.length] ?? tokens.keypoint}`,
                  opacity: on ? 1 : 0.4,
                }}
              />
              {s.displayName}
            </ToggleButton>
          )
        })}
      </Box>
      <Box sx={{ flex: '1 1 0', minHeight: 160 }}>
        <LineChart
          xAxis={[
            {
              data: [...sweep.values],
              scaleType: 'linear',
              valueFormatter: (v: number) => format(v),
            },
          ]}
          yAxis={[{ min: 0, max: 1, valueFormatter: (v: number) => `${Math.round(v * 100)}%` }]}
          series={series.map(({ s, color }) => ({
            id: s.extractorId,
            label: s.displayName,
            color,
            curve: 'linear' as const,
            data: s.points.map((p) => p.metrics[metric]),
            valueFormatter: (v: number | null) => (v === null ? '' : `${(v * 100).toFixed(1)}%`),
          }))}
          hideLegend
          grid={{ horizontal: true }}
        >
          <ChartsReferenceLine
            x={value}
            lineStyle={{ strokeDasharray: '4 4', stroke: '#e6e8ee', opacity: 0.6 }}
          />
        </LineChart>
      </Box>
    </Box>
  )
}

const pct = (v: number | undefined) => (v === undefined ? '—' : `${Math.round(v * 100)}%`)

/** Every detector's metrics at the selected value. */
function ValueTable({ sweep, index, label }: { sweep: SweepResult; index: number; label: string }) {
  const rows = sweep.series.map((s) => ({ s, metrics: s.points[index]?.metrics }))
  const headers = ['Detector', 'Correct', 'Repeatability', 'Precision']
  return (
    <Box
      component="table"
      sx={{ flex: '0 0 auto', width: '100%', borderCollapse: 'collapse', fontSize: 13 }}
    >
      <Box
        component="caption"
        sx={{ textAlign: 'left', fontSize: 12, color: 'text.secondary', pb: 0.75 }}
      >
        At {label}
      </Box>
      <thead>
        <tr>
          {headers.map((h, i) => (
            <Box
              component="th"
              scope="col"
              key={h}
              sx={{
                textAlign: i === 0 ? 'left' : 'right',
                py: 0.5,
                pl: i === 0 ? 0 : 1,
                fontWeight: 500,
                color: 'text.secondary',
                borderBottom: 1,
                borderColor: 'divider',
              }}
            >
              {h}
            </Box>
          ))}
        </tr>
      </thead>
      <tbody>
        {rows.map(({ s, metrics }, i) => (
          <tr key={s.extractorId}>
            <Box
              component="th"
              scope="row"
              sx={{
                textAlign: 'left',
                py: 0.75,
                fontWeight: 500,
                whiteSpace: 'nowrap',
                borderBottom: 1,
                borderColor: 'divider',
              }}
            >
              <Box
                component="span"
                sx={{
                  display: 'inline-block',
                  width: 10,
                  height: 10,
                  borderRadius: '50%',
                  bgcolor: SERIES_COLORS[i % SERIES_COLORS.length],
                  mr: 1,
                }}
              />
              {s.displayName}
            </Box>
            {[metrics?.matchingScore, metrics?.repeatability, metrics?.matchPrecision].map(
              (v, j) => (
                <Box
                  component="td"
                  key={['score', 'repeat', 'precision'][j]}
                  sx={{
                    textAlign: 'right',
                    py: 0.75,
                    pl: 1,
                    fontFamily: monoFontFamily,
                    borderBottom: 1,
                    borderColor: 'divider',
                  }}
                >
                  {pct(v)}
                </Box>
              ),
            )}
          </tr>
        ))}
      </tbody>
    </Box>
  )
}

/** A sentence about who wins at the selected value and where SIFT is weakest. */
function Insight({
  sweep,
  index,
  label,
  format,
}: {
  sweep: SweepResult
  index: number
  label: string
  format: (v: number) => string
}) {
  const rows = sweep.series.map((s) => ({ s, metrics: s.points[index]?.metrics }))
  const ranked = [...rows].sort(
    (a, b) => (b.metrics?.matchingScore ?? 0) - (a.metrics?.matchingScore ?? 0),
  )
  const best = ranked[0]
  const sift = sweep.series.find((s) => s.extractorId === 'sift')
  const worst = sift?.points.reduce(
    (w, p) => (p.metrics.matchingScore < w.metrics.matchingScore ? p : w),
    sift.points[0] ?? { value: 0, metrics: { matchingScore: 1 } as never },
  )

  return (
    <Box
      sx={{
        flex: '0 0 auto',
        px: 1.75,
        py: 1.25,
        borderRadius: 1.5,
        bgcolor: '#1e222b',
        display: 'flex',
        alignItems: 'baseline',
        gap: 1.5,
      }}
    >
      <Typography variant="overline" color="text.secondary" sx={{ flex: '0 0 auto' }}>
        Insight
      </Typography>
      <Typography sx={{ lineHeight: 1.5, color: '#d4d8e1' }}>
        {best?.metrics
          ? `At ${label}, ${best.s.displayName} keeps the most correct matches (${pct(best.metrics.matchingScore)}).`
          : 'No data at this value.'}
        {worst && sift
          ? ` ${sift.displayName} is weakest at ${format(worst.value)} with ${pct(worst.metrics.matchingScore)} correct matches.`
          : ''}
      </Typography>
    </Box>
  )
}
