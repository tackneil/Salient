import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Collapse from '@mui/material/Collapse'
import FormControlLabel from '@mui/material/FormControlLabel'
import MenuItem from '@mui/material/MenuItem'
import Slider from '@mui/material/Slider'
import Switch from '@mui/material/Switch'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { useId, useState, type ReactNode } from 'react'

import { monoFontFamily } from '../../app/theme'
import { decimalsFor, type ParameterField, type ParameterValues } from '../schema/parameterSchema'

interface ParameterFormProps {
  fields: readonly ParameterField[]
  values: ParameterValues
  onChange: (name: string, value: unknown) => void
  /** Extra content rendered above a field's control, for example a survival histogram. */
  above?: Partial<Record<string, ReactNode>>
  disabled?: boolean
}

/** Renders algorithm settings from the API's parameter schema, so new algorithms need no UI changes (ADR-0003). */
export function ParameterForm({ fields, values, onChange, above, disabled }: ParameterFormProps) {
  const [showAdvanced, setShowAdvanced] = useState(false)
  const regular = fields.filter((f) => !f.advanced)
  const advanced = fields.filter((f) => f.advanced)

  const render = (field: ParameterField) => (
    <ParameterControl
      key={field.name}
      field={field}
      value={values[field.name] ?? field.defaultValue}
      onChange={(v) => {
        onChange(field.name, v)
      }}
      above={above?.[field.name]}
      disabled={disabled}
    />
  )

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.25 }}>
      {regular.map(render)}
      {advanced.length > 0 && (
        <Box>
          <Button
            size="small"
            onClick={() => {
              setShowAdvanced((s) => !s)
            }}
            endIcon={
              <ExpandMoreIcon
                sx={{
                  transform: showAdvanced ? 'rotate(180deg)' : 'none',
                  transition: 'transform .2s',
                }}
              />
            }
            aria-expanded={showAdvanced}
          >
            Advanced
          </Button>
          <Collapse in={showAdvanced}>
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.25, pt: 1.5 }}>
              {advanced.map(render)}
            </Box>
          </Collapse>
        </Box>
      )}
    </Box>
  )
}

function ParameterControl({
  field,
  value,
  onChange,
  above,
  disabled,
}: {
  field: ParameterField
  value: unknown
  onChange: (value: unknown) => void
  above?: ReactNode
  disabled?: boolean
}) {
  const id = useId()

  if (field.type === 'boolean') {
    return (
      <FormControlLabel
        control={
          <Switch
            checked={value === true}
            onChange={(_, checked) => {
              onChange(checked)
            }}
            disabled={disabled}
          />
        }
        label={
          <Box>
            <Typography sx={{ fontWeight: 500 }}>{field.title}</Typography>
            {field.description && (
              <Typography variant="caption" color="text.secondary">
                {field.description}
              </Typography>
            )}
          </Box>
        }
      />
    )
  }

  if (field.options) {
    return (
      <TextField
        select
        size="small"
        label={field.title}
        value={typeof value === 'string' ? value : ''}
        onChange={(e) => {
          onChange(e.target.value)
        }}
        helperText={field.description}
        disabled={disabled}
      >
        {field.options.map((o) => (
          <MenuItem key={o} value={o}>
            {o}
          </MenuItem>
        ))}
      </TextField>
    )
  }

  const numeric = typeof value === 'number' ? value : Number(field.defaultValue ?? 0)
  const min = field.minimum ?? 0
  const max = field.maximum ?? Math.max(1, numeric * 2)
  const decimals = decimalsFor(field)

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
        <Typography component="label" id={`${id}-label`} sx={{ fontWeight: 500 }}>
          {field.title}
        </Typography>
        <Typography sx={{ fontFamily: monoFontFamily, fontSize: 13, color: 'primary.main' }}>
          {numeric.toFixed(decimals)}
        </Typography>
      </Box>
      {above}
      <Slider
        aria-labelledby={`${id}-label`}
        aria-describedby={field.description ? `${id}-help` : undefined}
        size="small"
        min={min}
        max={max}
        step={field.step ?? (max - min) / 100}
        value={numeric}
        onChange={(_, v) => {
          onChange(Array.isArray(v) ? v[0] : v)
        }}
        disabled={disabled}
      />
      {field.description && (
        <Typography id={`${id}-help`} variant="caption" color="text.secondary">
          {field.description}
        </Typography>
      )}
    </Box>
  )
}
