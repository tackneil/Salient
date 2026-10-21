import Box from '@mui/material/Box'
import Typography from '@mui/material/Typography'

import { monoFontFamily, tokens } from '../../app/theme'

interface ThresholdHistogramProps {
  /** Survivor counts for evenly spaced threshold values from min to max. */
  counts: readonly number[]
  min: number
  max: number
  value: number
  /** Exact number of keypoints kept at the current value; the nearest bar is only an approximation. */
  currentCount?: number
  height?: number
}

/**
 * Bars above a threshold slider showing how many keypoints survive at each value (design: "keypoints vs threshold").
 * Bars on the active side of the current value are highlighted; a dashed marker shows the current value.
 */
export function ThresholdHistogram({
  counts,
  min,
  max,
  value,
  currentCount,
  height = 56,
}: ThresholdHistogramProps) {
  const peak = Math.max(1, ...counts)
  const step = counts.length > 1 ? (max - min) / (counts.length - 1) : 1
  const position = Math.min(1, Math.max(0, (value - min) / (max - min || 1)))
  const index = Math.round((value - min) / step)
  const current = currentCount ?? counts[Math.min(counts.length - 1, Math.max(0, index))] ?? 0

  return (
    <Box
      aria-hidden
      sx={{
        position: 'relative',
        height,
        display: 'flex',
        alignItems: 'flex-end',
        gap: '1px',
        px: 0.75,
      }}
    >
      {counts.map((count, i) => {
        const v = min + i * step
        const active = v <= value + 1e-12
        return (
          <Box
            key={v}
            sx={{
              flex: '1 1 0',
              height: `${(count / peak) * 100}%`,
              minHeight: count > 0 ? '1px' : 0,
              bgcolor: active ? '#3f5f93' : '#2a3242',
              borderRadius: '1px 1px 0 0',
            }}
          />
        )
      })}
      <Box
        sx={{
          position: 'absolute',
          top: 0,
          bottom: 0,
          left: `calc(6px + (100% - 12px) * ${position})`,
          borderLeft: `1.5px dashed ${tokens.keypoint}`,
        }}
      />
      <Typography
        sx={{
          position: 'absolute',
          top: -2,
          left: `calc(6px + (100% - 12px) * ${position})`,
          transform: position > 0.7 ? 'translateX(calc(-100% - 6px))' : 'translateX(6px)',
          fontFamily: monoFontFamily,
          fontSize: 11,
          color: tokens.keypoint,
          whiteSpace: 'nowrap',
        }}
      >
        {current} kept
      </Typography>
    </Box>
  )
}
