import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Collapse from '@mui/material/Collapse'
import FormControlLabel from '@mui/material/FormControlLabel'
import MenuItem from '@mui/material/MenuItem'
import Slider from '@mui/material/Slider'
import Switch from '@mui/material/Switch'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
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

type Block = { kind: 'row'; fields: ParameterField[] } | { kind: 'single'; field: ParameterField }

/** Short sliders that carry no extra content share a row; everything else gets the full width. */
function toBlocks(fields: readonly ParameterField[], above: ParameterFormProps['above']): Block[] {
  const blocks: Block[] = []
  for (const field of fields) {
    const compact = (field.type === 'number' || field.type === 'integer') && !above?.[field.name]
    const last = blocks[blocks.length - 1]
    if (compact && !field.options && last?.kind === 'row') last.fields.push(field)
    else if (compact && !field.options) blocks.push({ kind: 'row', fields: [field] })
    else blocks.push({ kind: 'single', field })
  }
  return blocks
}

/** Renders algorithm settings from the API's parameter schema, so new algorithms need no UI changes (ADR-0003). */
export function ParameterForm({ fields, values, onChange, above, disabled }: ParameterFormProps) {
  const [showAdvanced, setShowAdvanced] = useState(false)
  const regular = fields.filter((f) => !f.advanced)
  const advanced = fields.filter((f) => f.advanced)

  const render = (field: ParameterField, compact = false) => (
    <ParameterControl
      key={field.name}
      compact={compact}
      field={field}
      value={values[field.name] ?? field.defaultValue}
      onChange={(v) => {
        onChange(field.name, v)
      }}
      above={above?.[field.name]}
      disabled={disabled}
    />
  )

  const renderBlocks = (list: readonly ParameterField[]) =>
    toBlocks(list, above).map((block) =>
      block.kind === 'single' ? (
        render(block.field)
      ) : (
        <Box
          key={block.fields.map((f) => f.name).join('|')}
          sx={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(96px, 1fr))',
            alignItems: 'start',
            gap: 1.75,
          }}
        >
          {block.fields.map((f) => render(f, true))}
        </Box>
      ),
    )

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.75 }}>
      {renderBlocks(regular)}
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
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.75, pt: 1.5 }}>
              {renderBlocks(advanced)}
            </Box>
          </Collapse>
        </Box>
      )}
    </Box>
  )
}

/** A small "i" that carries a parameter's explanation, so slider rows stay one line tall. */
export function InfoHint({ text }: { text: string }) {
  return (
    <Tooltip title={text}>
      <Box
        component="span"
        tabIndex={0}
        role="img"
        aria-label={text}
        sx={{ display: 'inline-flex', verticalAlign: 'middle', color: 'text.secondary' }}
      >
        <InfoOutlinedIcon sx={{ fontSize: 16 }} />
      </Box>
    </Tooltip>
  )
}

function ParameterControl({
  field,
  value,
  onChange,
  above,
  disabled,
  compact = false,
}: {
  compact?: boolean
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
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.25, minWidth: 0 }}>
      <Box
        sx={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: compact ? 'flex-start' : 'baseline',
          gap: 1,
          // Short sliders share a row, so their labels may wrap; two lines are reserved to keep the sliders level.
          minHeight: compact ? 34 : undefined,
        }}
      >
        <Box sx={{ display: 'inline-flex', alignItems: 'baseline', gap: 0.5, minWidth: 0 }}>
          {compact ? (
            // A row of short sliders has no room for an icon; the label itself carries the explanation.
            <Tooltip title={field.description ?? ''} describeChild>
              <Typography
                component="label"
                id={`${id}-label`}
                sx={{ fontWeight: 500, fontSize: 13, lineHeight: 1.3, cursor: 'help' }}
              >
                {field.title}
              </Typography>
            </Tooltip>
          ) : (
            <>
              <Typography component="label" id={`${id}-label`} sx={{ fontWeight: 500 }}>
                {field.title}
              </Typography>
              {field.description && <InfoHint text={field.description} />}
            </>
          )}
        </Box>
        <Typography sx={{ fontFamily: monoFontFamily, fontSize: 13, color: 'primary.main' }}>
          {numeric.toFixed(decimals)}
        </Typography>
      </Box>
      {above}
      <Slider
        aria-labelledby={`${id}-label`}
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
    </Box>
  )
}
