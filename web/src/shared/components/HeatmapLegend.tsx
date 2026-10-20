import Box from '@mui/material/Box'
import Typography from '@mui/material/Typography'

import { monoFontFamily } from '../../app/theme'
import type { LayerLegend } from '../api/client'
import { colormapGradient } from '../colormaps'

const format = (value: number) => {
  const magnitude = Math.abs(value)
  if (magnitude === 0) return '0'
  if (magnitude >= 100) return value.toFixed(0)
  if (magnitude >= 1) return value.toFixed(2)
  return value.toPrecision(2)
}

/** Colorbar for a server-rendered layer (ADR-0006). */
export function HeatmapLegend({ title, legend }: { title: string; legend: LayerLegend }) {
  return (
    <Box
      sx={{
        p: 1,
        borderRadius: 1,
        bgcolor: 'rgba(11,13,17,0.85)',
        display: 'flex',
        flexDirection: 'column',
        gap: 0.75,
        minWidth: 200,
        pointerEvents: 'none',
      }}
    >
      <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{title}</Typography>
      <Box sx={{ height: 8, borderRadius: 1, background: colormapGradient(legend.colormap) }} />
      <Box
        sx={{
          display: 'flex',
          justifyContent: 'space-between',
          fontFamily: monoFontFamily,
          fontSize: 11,
          color: 'text.secondary',
        }}
      >
        <span>{format(legend.min)}</span>
        {legend.scale === 'log' && <span>log</span>}
        <span>{legend.max >= 1000 ? `≥ ${format(legend.max)}` : format(legend.max)}</span>
      </Box>
    </Box>
  )
}
