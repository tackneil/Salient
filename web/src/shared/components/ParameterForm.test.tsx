import { ThemeProvider } from '@mui/material/styles'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import { theme } from '../../app/theme'
import { defaultValues, parseSchema } from '../schema/parameterSchema'
import { ParameterForm } from './ParameterForm'

const schema = {
  type: 'object',
  properties: {
    contrastThreshold: {
      type: 'number',
      title: 'Contrast threshold',
      description: 'Rejects weak extrema.',
      minimum: 0.005,
      maximum: 0.1,
      'x-step': 0.0025,
      default: 0.04,
      'x-order': 1,
    },
    layersPerOctave: {
      type: 'integer',
      title: 'Layers per octave',
      minimum: 1,
      maximum: 6,
      default: 3,
      'x-order': 0,
    },
    upsampleInput: {
      type: 'boolean',
      title: 'Upsample input',
      default: false,
      'x-advanced': true,
      'x-order': 2,
    },
  },
}

describe('parseSchema', () => {
  it('reads fields in server order with ranges, steps and defaults', () => {
    const fields = parseSchema(schema)

    expect(fields.map((f) => f.name)).toEqual([
      'layersPerOctave',
      'contrastThreshold',
      'upsampleInput',
    ])
    expect(fields[0]?.step).toBe(1)
    expect(fields[1]).toMatchObject({ minimum: 0.005, maximum: 0.1, step: 0.0025, type: 'number' })
    expect(fields[2]?.advanced).toBe(true)
    expect(defaultValues(fields)).toEqual({
      layersPerOctave: 3,
      contrastThreshold: 0.04,
      upsampleInput: false,
    })
  })

  it('returns no fields for unexpected input', () => {
    expect(parseSchema(null)).toEqual([])
    expect(parseSchema({ properties: 5 })).toEqual([])
  })
})

describe('ParameterForm', () => {
  it('renders sliders with formatted values and hides advanced fields until expanded', async () => {
    const fields = parseSchema(schema)
    const onChange = vi.fn()
    render(
      <ThemeProvider theme={theme}>
        <ParameterForm fields={fields} values={defaultValues(fields)} onChange={onChange} />
      </ThemeProvider>,
    )

    expect(screen.getByRole('slider', { name: 'Contrast threshold' })).toHaveValue('0.04')
    expect(screen.getByText('0.0400')).toBeInTheDocument()
    expect(screen.getByRole('slider', { name: 'Layers per octave' })).toHaveValue('3')
    // Collapsed advanced fields are hidden from the accessibility tree.
    expect(screen.queryByRole('switch', { name: /Upsample input/ })).toBeNull()

    await userEvent.click(screen.getByRole('button', { name: 'Advanced' }))
    const toggle = screen.getByRole('switch', { name: /Upsample input/ })
    await userEvent.click(toggle)

    expect(onChange).toHaveBeenCalledWith('upsampleInput', true)
  })
})
