/** One parameter field described by the API's JSON Schema (ADR-0003). */
export interface ParameterField {
  name: string
  type: 'number' | 'integer' | 'boolean' | 'string'
  title: string
  description?: string
  minimum?: number
  maximum?: number
  step?: number
  defaultValue: unknown
  group?: string
  advanced: boolean
  order: number
  options?: string[]
}

export type ParameterValues = Record<string, unknown>

const asNumber = (v: unknown) => (typeof v === 'number' ? v : undefined)
const asString = (v: unknown) => (typeof v === 'string' ? v : undefined)

/** Reads the fields of a parameter schema, ordered as declared on the server. Unknown shapes yield no fields. */
export function parseSchema(schema: unknown): ParameterField[] {
  if (!schema || typeof schema !== 'object') return []
  const properties = (schema as { properties?: unknown }).properties
  if (!properties || typeof properties !== 'object') return []

  return Object.entries(properties as Record<string, unknown>)
    .map(([name, raw], index): ParameterField => {
      const p = (raw ?? {}) as Record<string, unknown>
      const type = asString(p.type)
      const options = Array.isArray(p.enum)
        ? p.enum.filter((o): o is string => typeof o === 'string')
        : undefined
      return {
        name,
        type: type === 'integer' || type === 'boolean' || type === 'number' ? type : 'string',
        title: asString(p.title) ?? name,
        description: asString(p.description),
        minimum: asNumber(p.minimum),
        maximum: asNumber(p.maximum),
        step: asNumber(p['x-step']) ?? (type === 'integer' ? 1 : undefined),
        defaultValue: p.default,
        group: asString(p['x-group']),
        advanced: p['x-advanced'] === true,
        order: asNumber(p['x-order']) ?? index,
        options,
      }
    })
    .sort((a, b) => a.order - b.order)
}

export function defaultValues(fields: readonly ParameterField[]): ParameterValues {
  return Object.fromEntries(fields.map((f) => [f.name, f.defaultValue]))
}

/** Keeps known fields only and fills missing ones with defaults. */
export function normalizeValues(
  fields: readonly ParameterField[],
  values: ParameterValues | undefined,
): ParameterValues {
  return Object.fromEntries(fields.map((f) => [f.name, values?.[f.name] ?? f.defaultValue]))
}

/** Number of decimals that represent the step exactly, for value labels. */
export function decimalsFor(field: ParameterField): number {
  if (field.type === 'integer') return 0
  const step = field.step ?? 0.01
  const text = String(step)
  const dot = text.indexOf('.')
  return dot < 0 ? 0 : Math.min(6, text.length - dot - 1)
}
