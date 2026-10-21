import CheckIcon from '@mui/icons-material/Check'
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutlineOutlined'
import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import CircularProgress from '@mui/material/CircularProgress'
import IconButton from '@mui/material/IconButton'
import LinearProgress from '@mui/material/LinearProgress'
import Paper from '@mui/material/Paper'
import Popover from '@mui/material/Popover'
import Typography from '@mui/material/Typography'
import { useEffect, useRef, useState, type ReactNode } from 'react'

import { monoFontFamily, tokens } from '../../app/theme'
import type { RunInfo } from '../api/client'
import { labelFor } from '../stages'

/**
 * Live stage chips and an overall progress line for a run (ADR-0005). The chips stay on one line and scroll
 * sideways as stages arrive; `details`, when given, opens from a chevron as a panel over the view (ADR-0015).
 */
export function RunProgressBar({
  run,
  expectedStages,
  title,
  details,
  detailsLabel = 'Show details',
}: {
  run: RunInfo | undefined
  expectedStages: readonly string[]
  title: string
  details?: ReactNode
  detailsLabel?: string
}) {
  const [anchor, setAnchor] = useState<HTMLElement | null>(null)
  const [open, setOpen] = useState(false)
  const list = useRef<HTMLOListElement>(null)
  const stages = run?.stages

  // Keep the stage that is running in view.
  useEffect(() => {
    const element = list.current
    const current = element?.querySelector<HTMLElement>('[aria-current="step"]')
    if (!element || !current) return
    const right = current.offsetLeft + current.offsetWidth + 8
    if (right > element.scrollLeft + element.clientWidth) {
      element.scrollLeft = right - element.clientWidth
    }
  }, [stages])

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
    <Paper
      ref={setAnchor}
      variant="outlined"
      aria-label="Run progress"
      sx={{ flex: '0 0 auto', overflow: 'hidden' }}
    >
      <Box
        sx={{
          display: 'flex',
          flexWrap: 'nowrap',
          alignItems: 'center',
          gap: 2,
          pl: 2,
          pr: 1,
          py: 1,
        }}
      >
        <Box
          sx={{
            flex: '0 0 auto',
            display: 'flex',
            alignItems: 'baseline',
            gap: 1.25,
            width: 230,
            whiteSpace: 'nowrap',
          }}
        >
          <Typography sx={{ fontWeight: 600 }}>{title}</Typography>
          <Typography
            variant="caption"
            color={run.status === 'failed' ? 'error' : 'text.secondary'}
            role="status"
            noWrap
          >
            {status}
          </Typography>
        </Box>
        <Box
          component="ol"
          ref={list}
          aria-label="Stages"
          sx={{
            position: 'relative',
            listStyle: 'none',
            m: 0,
            py: 0.5,
            px: 0,
            display: 'flex',
            flexWrap: 'nowrap',
            gap: 0.75,
            flex: '1 1 0',
            minWidth: 0,
            overflowX: 'auto',
            overflowY: 'hidden',
            scrollbarWidth: 'none',
            '&::-webkit-scrollbar': { display: 'none' },
          }}
        >
          {run.stages.map((stage) => (
            <Box
              component="li"
              key={stage.id}
              aria-current={stage.state === 'running' ? 'step' : undefined}
              sx={{ flex: '0 0 auto' }}
            >
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
            </Box>
          ))}
          {!finished &&
            run.status !== 'failed' &&
            pending.map((id) => (
              <Box component="li" key={id} sx={{ flex: '0 0 auto' }}>
                <Chip
                  size="small"
                  label={labelFor(id)}
                  variant="outlined"
                  sx={{ color: 'text.secondary' }}
                />
              </Box>
            ))}
          {run.status === 'failed' && (
            <Box component="li" sx={{ flex: '0 0 auto' }}>
              <Chip size="small" color="error" icon={<ErrorOutlineIcon />} label="Failed" />
            </Box>
          )}
        </Box>
        {details && (
          <IconButton
            aria-label={detailsLabel}
            aria-haspopup="dialog"
            aria-expanded={open}
            onClick={() => {
              setOpen((o) => !o)
            }}
            sx={{
              flex: '0 0 auto',
              border: 1,
              borderColor: open ? 'primary.main' : tokens.borderStrong,
              bgcolor: open ? '#26324a' : 'transparent',
            }}
          >
            <ExpandMoreIcon
              fontSize="small"
              sx={{ transform: open ? 'rotate(180deg)' : 'none', transition: 'transform .15s' }}
            />
          </IconButton>
        )}
      </Box>
      <LinearProgress variant="determinate" value={progress} sx={{ height: 3 }} />
      {details && (
        <Popover
          open={open}
          anchorEl={anchor}
          onClose={() => {
            setOpen(false)
          }}
          anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
          transformOrigin={{ vertical: 'top', horizontal: 'right' }}
          slotProps={{
            paper: {
              'aria-label': detailsLabel,
              sx: {
                mt: 1,
                width: 520,
                maxWidth: 'calc(100vw - 32px)',
                p: 2,
                bgcolor: '#1a1e26',
                border: 1,
                borderColor: tokens.borderStrong,
                backgroundImage: 'none',
              },
            },
          }}
        >
          {details}
        </Popover>
      )}
    </Paper>
  )
}
