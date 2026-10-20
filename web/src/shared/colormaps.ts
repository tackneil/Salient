/** Colormaps used by server-rendered layers (ADR-0006), as CSS gradients for legends. */
export const colormapStops: Record<string, string[]> = {
  gray: ['#000000', '#ffffff'],
  diverging: ['#5aa0ff', '#0e1218', '#ffa03c'],
  viridis: ['#440154', '#3b528b', '#21918c', '#5ec962', '#fde725'],
  magma: ['#000004', '#51127c', '#b73779', '#fc8961', '#fcfdbf'],
}

export function colormapGradient(name: string): string {
  const stops = colormapStops[name] ?? colormapStops.gray ?? []
  return `linear-gradient(90deg, ${stops.join(', ')})`
}

/** Keypoint colors per octave group, distinguishable by lightness as well as hue. */
export const octaveColors = ['#ffb547', '#7ab0ff', '#ff7eb6'] as const

export function octaveColor(octave: number): string {
  const group = Math.min(Math.max(octave, 0), 2)
  return octaveColors[group] ?? octaveColors[0]
}
