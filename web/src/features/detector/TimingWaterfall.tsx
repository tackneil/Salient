import Box from '@mui/material/Box'
import Typography from '@mui/material/Typography'

import { monoFontFamily, tokens } from '../../app/theme'
import type { StageInfo } from '../../shared/api/client'
import { labelFor } from '../../shared/stages'

/** Stages laid out on a shared time axis (design: timing waterfall). */
export function TimingWaterfall({
  stages,
  keypoints,
}: {
  stages: readonly StageInfo[]
  keypoints?: number
}) {
  const total = stages.reduce((sum, s) => sum + (s.durationMs ?? 0), 0)
  const durations = stages.map((s) => s.durationMs ?? 0)
  const rows = stages.map((stage, i) => ({
    stage,
    start: durations.slice(0, i).reduce((sum, d) => sum + d, 0),
    duration: durations[i] ?? 0,
  }))

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.25 }}>
      <Box sx={{ display: 'flex', flexWrap: 'wrap', alignItems: 'baseline', gap: '6px 16px' }}>
        <Typography variant="overline" component="h2" color="text.secondary">
          Timing
        </Typography>
        <Typography sx={{ fontFamily: monoFontFamily, fontSize: 12, color: 'text.secondary' }}>
          {keypoints !== undefined ? `${keypoints} keypoints · ` : ''}
          {total.toFixed(0)} ms total
        </Typography>
      </Box>
      <Box
        role="list"
        aria-label="Stage timings"
        sx={{ display: 'flex', flexDirection: 'column', gap: 0.5 }}
      >
        {rows.map(({ stage, start, duration }) => (
          <Box
            role="listitem"
            key={stage.id}
            sx={{
              display: 'grid',
              gridTemplateColumns: '130px minmax(0, 1fr) 60px',
              alignItems: 'center',
              gap: 1.25,
            }}
          >
            <Typography sx={{ fontSize: 12 }}>{labelFor(stage.id)}</Typography>
            <Box sx={{ position: 'relative', height: 10 }}>
              <Box
                sx={{
                  position: 'absolute',
                  top: 0,
                  bottom: 0,
                  left: `${total > 0 ? (start / total) * 100 : 0}%`,
                  width: `max(2px, ${total > 0 ? (duration / total) * 100 : 0}%)`,
                  borderRadius: '3px',
                  bgcolor: stage.state === 'running' ? tokens.primary : '#4e7bc4',
                }}
              />
            </Box>
            <Typography sx={{ fontFamily: monoFontFamily, fontSize: 12, textAlign: 'right' }}>
              {stage.durationMs === null ? '…' : `${duration < 1 ? '<1' : duration.toFixed(0)} ms`}
            </Typography>
          </Box>
        ))}
      </Box>
    </Box>
  )
}
