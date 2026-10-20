import CheckIcon from '@mui/icons-material/Check'
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutlineOutlined'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import CircularProgress from '@mui/material/CircularProgress'
import LinearProgress from '@mui/material/LinearProgress'
import Paper from '@mui/material/Paper'
import Typography from '@mui/material/Typography'

import { monoFontFamily } from '../../app/theme'
import type { RunInfo } from '../api/client'
import { labelFor } from '../stages'

/** Live stage chips and an overall progress line for a run (ADR-0005). */
export function RunProgressBar({
  run,
  expectedStages,
  title,
}: {
  run: RunInfo | undefined
  expectedStages: readonly string[]
  title: string
}) {
  if (!run) return null
  const done = run.stages.filter((s) => s.state === 'completed').length
  const finished = run.status === 'completed'
  const progress = run.progress
    ? (run.progress.completed / Math.max(1, run.progress.total)) * 100
    : finished
      ? 100
      : (done / Math.max(1, expectedStages.length)) * 100
  const totalMs = run.stages.reduce((sum, s) => sum + (s.durationMs ?? 0), 0)
  const pending = expectedStages.filter((id) => !run.stages.some((s) => s.id === id))

  const status =
    run.status === 'completed'
      ? `completed in ${totalMs.toFixed(0)} ms`
      : run.status === 'failed'
        ? (run.error ?? 'failed')
        : run.status === 'canceled'
          ? 'canceled'
          : run.progress
            ? `${String(run.progress.completed)} / ${String(run.progress.total)} · ${run.progress.message ?? ''}`
            : run.status

  return (
    <Paper variant="outlined" sx={{ overflow: 'hidden' }} aria-label="Run progress">
      <Box
        sx={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 1, px: 2, py: 1.25 }}
      >
        <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 1.25, minWidth: 200 }}>
          <Typography sx={{ fontWeight: 600 }}>{title}</Typography>
          <Typography
            variant="caption"
            color={run.status === 'failed' ? 'error' : 'text.secondary'}
            role="status"
          >
            {status}
          </Typography>
        </Box>
        <Box
          component="ol"
          sx={{
            listStyle: 'none',
            m: 0,
            p: 0,
            display: 'flex',
            flexWrap: 'wrap',
            gap: 0.75,
            flex: '1 1 480px',
          }}
        >
          {run.stages.map((stage) => (
            <li key={stage.id}>
              <Chip
                size="small"
                icon={
                  stage.state === 'completed' ? (
                    <CheckIcon sx={{ '&&': { color: 'success.main' } }} />
                  ) : (
                    <CircularProgress size={12} thickness={6} />
                  )
                }
                label={
                  <span>
                    {labelFor(stage.id)}
                    {stage.durationMs !== null && (
                      <Box
                        component="span"
                        sx={{ ml: 0.75, fontFamily: monoFontFamily, color: 'success.light' }}
                      >
                        {stage.durationMs < 1 ? '<1' : stage.durationMs.toFixed(0)} ms
                      </Box>
                    )}
                  </span>
                }
                sx={{ bgcolor: stage.state === 'running' ? '#1f2d45' : '#1a2420' }}
              />
            </li>
          ))}
          {!finished &&
            run.status !== 'failed' &&
            pending.map((id) => (
              <li key={id}>
                <Chip
                  size="small"
                  label={labelFor(id)}
                  variant="outlined"
                  sx={{ color: 'text.secondary' }}
                />
              </li>
            ))}
          {run.status === 'failed' && (
            <li>
              <Chip size="small" color="error" icon={<ErrorOutlineIcon />} label="Failed" />
            </li>
          )}
        </Box>
        <Typography variant="caption" color="text.secondary">
          streamed via SignalR
        </Typography>
      </Box>
      <LinearProgress variant="determinate" value={progress} sx={{ height: 3 }} />
    </Paper>
  )
}
