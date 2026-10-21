import Box from '@mui/material/Box'
import Slider from '@mui/material/Slider'
import Typography from '@mui/material/Typography'
import { useId } from 'react'

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

/** Colorbar for a server-rendered layer (ADR-0006), with an optional opacity control for the layer. */
export function HeatmapLegend({
  title,
  legend,
  opacity,
}: {
  title: string
  legend: LayerLegend
  opacity?: { value: number; onChange: (value: number) => void }
}) {
  const labelId = useId()
  return (
    <Box
      // Pointer events must not reach the viewer underneath, which would start panning.
      onPointerDown={
        opacity
          ? (e) => {
              e.stopPropagation()
            }
          : undefined
      }
      sx={{
        p: 1,
        borderRadius: 1,
        bgcolor: 'rgba(11,13,17,0.88)',
        display: 'flex',
        flexDirection: 'column',
        gap: 0.75,
        width: 210,
        pointerEvents: opacity ? 'auto' : 'none',
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
      {opacity && (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25 }}>
          <Typography variant="caption" color="text.secondary" id={labelId}>
            Opacity
          </Typography>
          <Slider
            size="small"
            min={0.1}
            max={1}
            step={0.05}
            value={opacity.value}
            aria-labelledby={labelId}
            onChange={(_, v) => {
              opacity.onChange(Number(Array.isArray(v) ? v[0] : v))
            }}
          />
        </Box>
      )}
    </Box>
  )
}
