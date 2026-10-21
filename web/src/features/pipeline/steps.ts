/** The seven pipeline steps of the design, with the explanations shown beside each (design artboard "Pipeline"). */
export interface PipelineStep {
  id: 'pyramid' | 'dog' | 'extrema' | 'contrast' | 'edge' | 'orientation' | 'descriptor'
  title: string
  stageIds: readonly string[]
  section: string
  sourcePath: string
  text: string
  formula: string
}

export const PIPELINE_STEPS: readonly PipelineStep[] = [
  {
    id: 'pyramid',
    title: 'Gaussian pyramid',
    stageIds: ['gaussian-pyramid'],
    section: 'section 3',
    sourcePath: 'src/Salient.Features.Sift/GaussianPyramid.cs',
    text: 'The image is blurred with Gaussians of growing σ = σ₀·kⁱ, k = 2^(1/s). Each next octave starts from the layer with σ = 2σ₀, downsampled by two, so the scale axis continues smoothly across octaves.',
    formula: 'L(x, y, σ) = G(x, y, σ) ∗ I(x, y)',
  },
  {
    id: 'dog',
    title: 'Difference of Gaussians',
    stageIds: ['dog'],
    section: 'section 3',
    sourcePath: 'src/Salient.Features.Sift/DogPyramid.cs',
    text: 'Adjacent layers are subtracted. DoG approximates the scale-normalized Laplacian σ²∇²G and responds to blobs whose size matches the scale. The sign is kept: blue is negative, orange is positive.',
    formula: 'D(x, y, σ) = L(x, y, kσ) − L(x, y, σ)',
  },
  {
    id: 'extrema',
    title: 'Scale-space extrema',
    stageIds: ['extrema', 'refine'],
    section: 'sections 3.1, 4',
    sourcePath: 'src/Salient.Features.Sift/KeypointRefiner.cs',
    text: 'Every pixel is compared with its 26 neighbours: 8 in its own layer and 9 in each adjacent scale. Candidates are then refined to sub-pixel accuracy with a Taylor expansion of D; those that do not converge are rejected as unstable.',
    formula: 'x̂ = −(∂²D/∂x²)⁻¹ · ∂D/∂x',
  },
  {
    id: 'contrast',
    title: 'Contrast threshold',
    stageIds: ['contrast'],
    section: 'section 4',
    sourcePath: 'src/Salient.Features.Sift/ContrastFilter.cs',
    text: 'Low-contrast extrema are unstable under noise. The DoG value at the refined location is compared against the threshold, with intensities normalized to [0, 1].',
    formula: '|D(x̂)| · s = |D + ½ ∂Dᵀ/∂x · x̂| · s < t  →  reject',
  },
  {
    id: 'edge',
    title: 'Edge rejection',
    stageIds: ['edge'],
    section: 'section 4.1',
    sourcePath: 'src/Salient.Features.Sift/EdgeFilter.cs',
    text: 'DoG responds strongly along edges, where the location is poorly defined. The ratio of principal curvatures comes from the 2×2 Hessian; points with det(H) ≤ 0 are rejected too.',
    formula: 'tr(H)² / det(H) < (r + 1)² / r',
  },
  {
    id: 'orientation',
    title: 'Orientation',
    stageIds: ['orientation'],
    section: 'section 5',
    sourcePath: 'src/Salient.Features.Sift/OrientationAssigner.cs',
    text: 'A 36-bin histogram of gradient directions is built around each keypoint, weighted by magnitude and a Gaussian window. The peak, and every peak above 80% of it, becomes a keypoint orientation.',
    formula: 'θ = argmax hist(atan2(∂L/∂y, ∂L/∂x))',
  },
  {
    id: 'descriptor',
    title: 'Descriptor',
    stageIds: ['descriptor'],
    section: 'section 6',
    sourcePath: 'src/Salient.Features.Sift/DescriptorBuilder.cs',
    text: 'A 4×4 grid of cells is laid over the keypoint, rotated to its orientation. Each cell holds an 8-bin gradient histogram: 128 values, normalized, clipped at 0.2 and normalized again.',
    formula: 'd ∈ ℝ¹²⁸,  d ← normalize(min(normalize(d), 0.2))',
  },
]

/** Base URL for "Show code" links; set VITE_SOURCE_BASE_URL to the repository's file browser, e.g. ".../blob/main/". */
const configuredSourceBase = import.meta.env.VITE_SOURCE_BASE_URL as string | undefined
export const sourceBaseUrl: string | undefined =
  configuredSourceBase !== undefined && configuredSourceBase.length > 0
    ? configuredSourceBase
    : undefined
