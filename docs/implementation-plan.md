# Salient — Implementation plan

This plan turns the accepted ADRs (`docs/adr/`) and the design mockup ("Salient UI" canvas) into working software. Each phase ends with a **definition of done** that can be checked. Phases are ordered by dependency; the [Parallel work](#parallel-work) section lists what can run at the same time.

> **Status (2026-10-20):** all phases 0–9 are complete. Deviations from the original plan are recorded in the ADR amendments:
> - ADR-0003, ADR-0004 and ADR-0006 were extended for exploration runs, diagnostics and heatmaps;
> - ADR-0009 replaced ImageSharp with SkiaSharp;
> - ADR-0011 moved from Node 22 to Node 24 and chose ESLint over oxlint.
>
> The CI workflow exists but has not run: there is no remote yet.

## Milestones

| Milestone | Phases | Outcome |
|-----------|--------|---------|
| **M1 — Correct SIFT** | 0–3 | Own SIFT detector and descriptor in C#, verified against OpenCV by golden tests. |
| **M2 — Matching & evaluation** | 4–5 | Pair matching with RANSAC and the robustness bench as libraries. |
| **M3 — Service** | 6 | API with jobs, SignalR progress, storage, caching and layer rendering. |
| **M4 — Product** | 7–9 | Frontend with all five views, a Docker image and a README with screenshots. |

---

## Phase 0 — Prerequisites and scaffolding

**Tasks**
- Upgrade Node.js to 24 LTS (current Vite requires 20.19 or newer; the machine had 20.11).
- `git init`. Add `.gitignore` (dotnet + node), `.editorconfig`, `global.json` (SDK 10.0.x, `rollForward: latestFeature`), `Directory.Build.props` (nullable, implicit usings, `TreatWarningsAsErrors`, analysis level), `Directory.Packages.props`.
- Create `Salient.slnx` with every project from ADR-0002, including empty test projects and `benchmarks/Salient.Benchmarks`.
- Scaffold `web/` with Vite (React + TypeScript), MUI, ESLint and Prettier.
- Add the architecture test enforcing the project dependency rules.
- Set up the GitHub Actions workflow: build, test, and the web lint, typecheck and build steps.
- Add `LICENSE` (MIT) and a README stub.

**Done when** `dotnet build` and `dotnet test` pass with zero warnings, `npm run build` passes in `web/`, and CI is green on the first push.

## Phase 1 — Core image primitives (`Salient.Core`)

**Tasks**
- `ImageF`: a single-channel float image with width, height, stride and row spans, normalized to [0, 1].
- Conversions from 8-bit grayscale. Color-to-gray conversion happens in the API layer.
- Gaussian kernel generation (radius ⌈3σ⌉, normalized) and separable convolution with mirrored borders, using SIMD row passes and `Parallel.For`.
- Downsampling by 2 (taking every second pixel of an already blurred image) and bilinear resize, used by the 4 MP input limit.
- Image subtraction (signed), finite-difference gradients, bilinear sampling.
- BenchmarkDotNet suites for convolution and resize.

**Done when** convolution matches a naive 2-D reference within 1e-5, the benchmarks run, and the API has XML documentation.

## Phase 2 — SIFT detector (`Salient.Features.Sift`)

One class per stage, matching the stage ids in ADR-0004.

| Stage id | Class | Notes |
|----------|-------|-------|
| `gaussian-pyramid` | `GaussianPyramid` | s + 3 layers per octave, σ₀ = 1.6, assumed input blur 0.5, incremental σ. The next octave comes from layer s (σ = 2σ₀), downsampled. Optional ×2 upsampling of the first octave as a parameter. |
| `dog` | `DogPyramid` | **Signed** difference; no absolute value. |
| `extrema` | `ExtremaDetector` | 26-neighbour strict extrema over all valid pixels, with a correct border of 1 px (or the descriptor radius). |
| `refine` | `KeypointRefiner` | Taylor refinement. First derivatives use central differences **divided by 2**. Iterate from the **accumulated** position, at most 5 iterations; reject when the point leaves the valid range or det(H) = 0. |
| `contrast` | `ContrastFilter` | Reject when \|D(x̂)\| < threshold, with threshold 0.04 / s in [0, 1] intensity units (OpenCV convention), exposed as the user-facing 0.04. |
| `edge` | `EdgeFilter` | Reject when det(H) ≤ 0 **or** tr² / det ≥ (r + 1)² / r. |
| `orientation` | `OrientationAssigner` | 36-bin histogram, Gaussian window 1.5σ, smoothing, every peak ≥ 80% becomes a keypoint, parabolic peak interpolation. |
| `heatmaps` | `HeatmapBuilder` | Only with `Detail == Full`. Builds the DoG response, gradient magnitude, contrast and edge-ratio maps at input resolution (ADR-0004). |

**Tasks**
- Contracts in `Salient.Features` (ADR-0003, ADR-0004):
  - `DetectionResult`, `DetectionOptions` and `KeypointDiagnostics`;
  - `IPipelineObserver` with candidate-set, image-layer and heatmap artifacts.
- The parameter record `SiftParameters` with attributes (ADR-0003): octaves, layers, σ₀, contrast threshold, edge threshold, upsample flag. Add a `Permissive` helper that sets the thresholds to the slider extremes used by exploration runs.
- Diagnostics per keypoint: contrast in threshold units (|D(x̂)| · s), edge ratio and the 36-bin orientation histogram.
- `IPipelineObserver` reporting with the three detail levels.
- Synthetic tests (ADR-0012):
  - a single Gaussian blob gives the right position and scale;
  - a sub-pixel-shifted blob is localized to its sub-pixel position;
  - a straight edge yields candidates that are all removed by edge rejection;
  - the DoG has the right sign;
  - pyramid layers have the intended blur (incremental σ);
  - a 90° image rotation shifts the orientation by 90°;
  - diagnostics reproduce the thresholds exactly.

**Done when** the synthetic tests pass, and the detector runs on the sample image with a keypoint count within ±20% of OpenCV for equivalent parameters.

## Phase 3 — SIFT descriptor and the OpenCV reference

**Tasks**
- `DescriptorBuilder`: 4×4×8 histogram over a rotated window, trilinear interpolation, normalize, clip at 0.2, renormalize. `DescriptorKind.Float`.
- `Salient.Reference.OpenCv`: adapters for OpenCV SIFT, ORB and AKAZE behind the same interfaces, with coordinate and angle conversion to the conventions in ADR-0003.
- Golden tests (`Category=Golden`): keypoint repeatability ≥ 85% versus OpenCV, and inlier ratio ≥ 90% on a synthetic homography pair.
- Expose per-keypoint debug data for the inspector: the orientation histogram and the descriptor.

**Done when** the golden tests pass locally and in CI. **This completes milestone M1.**

## Phase 4 — Matching (`Salient.Matching`)

**Tasks**
- Brute-force k-NN matcher (k = 2) with L2 for float descriptors and Hamming for binary ones; parallel over queries.
- Lowe's ratio test and an optional cross-check.
- Homography: normalized DLT (Hartley normalization) and RANSAC with a seeded random generator, a reprojection threshold, an adaptive iteration count and a final refit on the inliers.
- Tests: recover a known homography from synthetic correspondences with 30% outliers. Real-image test: rotation of 10° with scale 0.85, the same pair as in the mockup.

**Done when** the recovered rotation and scale are within 0.2° and 0.5% on the synthetic pair.

## Phase 5 — Robustness bench (`Salient.Evaluation`)

**Tasks**
- Transformations with ground-truth homographies: rotation, scale, Gaussian blur, additive noise (seeded), brightness shift. Perspective later.
- Metrics:
  - repeatability (keypoints within ε px after projection, among those visible in both images);
  - matching score (correct matches after the ratio test divided by visible keypoints).
- A sweep runner: families × values × detectors. Progress goes through `IPipelineObserver`-style callbacks, and the runner supports cancellation.

**Done when** a full default sweep (5 families × about 8 values × SIFT, ORB, AKAZE) produces curves comparable to the ones precomputed for the mockup, in under 60 s on the development machine. **This completes milestone M2.**

## Phase 6 — API (`Salient.Api`)

**Tasks**
- Storage: EF Core with SQLite (`Images`, `Runs`, `RunStages`), `IBlobStore` on the file system, and a retention background service (ADR-0008).
- Upload endpoint: SkiaSharp decode (ADR-0009), convert to gray, enforce the 4 MP limit, deduplicate by SHA-256.
- Algorithm registry and `GET /api/algorithms` with JSON Schemas generated from the parameter records.
- Job queue: a bounded `Channel<Job>` with a `BackgroundService` and cancellation. Endpoints:
  - `POST /api/runs`, `GET /api/runs/{id}`, `DELETE /api/runs/{id}`;
  - `POST /api/matches`;
  - `POST /api/benchmarks`.
- SignalR hub `/hubs/runs` with typed events, run groups and a state replay on subscribe (ADR-0005).
- Layer rendering to WebP:
  - grayscale pyramid layers;
  - diverging colormap for DoG;
  - viridis heatmaps for gradient and contrast;
  - magma heatmap for the edge ratio.
  
  Layers are served with immutable cache headers (ADR-0006).
- Keypoint, inspector, match and benchmark result endpoints.
- Result cache by content key, with `algorithmVersion` (ADR-0008).
- Problem details, Brotli compression, rate limiting, request size limits, health check.
- OpenAPI document. If the OpenCV native library fails to load, the reference algorithms are not registered, so the API still works without it.
- API tests with `WebApplicationFactory`: run lifecycle, cache hit, cancellation, validation errors.

**Done when** a run can be created with curl, progress events are visible in a SignalR test client, the layers open in a browser, and the API tests pass. **This completes milestone M3.**

## Phase 7 — Frontend foundation

**Tasks**
- MUI theme from the mockup: colors, Inter and JetBrains Mono, shape, component overrides, dark-first.
- App shell: app bar with navigation, Live connection indicator and the upload action. Routes from ADR-0011.
- Generated API client (`npm run api:generate`) and the TanStack Query setup. A CI drift check fails if the committed client is out of date.
- `useRunProgress(runId)` hook over SignalR, updating the query cache.
- Shared components:
  - `ImageStage` (zoom and pan with layered rendering);
  - `KeypointCanvas` (canvas drawing, DPR-aware, hit-testing grid);
  - `HeatmapLegend`;
  - `ParameterForm` (built from the JSON Schema);
  - `RunProgressBar`.
- Vitest tests for the schema form, the hit-testing and the zoom math.

**Done when** an uploaded image is shown in `ImageStage` with zoom and pan, the parameter form is generated from `GET /api/algorithms`, and a run shows live stage progress.

## Phase 8 — Frontend features

Build each view to match its artboard in the mockup.

1. **Detector:**
   - run strip with stage chips;
   - heatmap toggle with opacity and legend;
   - octave filter;
   - threshold sliders with keypoint-count histograms;
   - keypoint inspector (patch, orientation histogram, descriptor rose grid, stats);
   - timing waterfall.
2. **Pipeline:** a 7-step stepper, pyramid and DoG tile grids, kept and rejected keypoints per stage, explanations with formulas, a candidate funnel and "Show code" links to the GitHub source of each stage class.
3. **Matching:** pair upload, match lines on canvas, a ratio-test histogram split into correct and wrong matches, a RANSAC switch, the homography matrix and the recovered rotation and scale.
4. **Robustness:** family tabs, a value slider, a transformed preview, a MUI X line chart with detector toggles, a table at the current value and the generated insight text.
5. **Compare with OpenCV:** a compact dialog opened from the Detector toolbar, with overlay and side-by-side modes, counts, timing, repeatability and mean offset.

**Done when** all five views work against the real API and look like the mockup, and keyboard navigation and visible focus work in each view.

## Phase 9 — Packaging and polish

**Tasks**
- A multi-stage Dockerfile and `docker-compose.yml` with a `/data` volume (ADR-0013).
- Sample gallery from `samples/` (shuttle and a few standard test images with permissive licenses).
- Playwright end-to-end flows: detect and inspect, match a pair, run a short sweep.
- Performance pass: profile a 1 MP run and hit the target in ADR-0009 or record the actual numbers.
- `README.md` with screenshots, the architecture overview, how to run and a link to the coursework origin. Add `THIRD-PARTY-NOTICES.md`.

**Done when** `docker compose up` gives a working app on a clean machine and CI builds the image. **This completes milestone M4.**

---

## Design coverage

Which data each part of the design mockup needs, and which phase provides it.

| Screen / element | Data | Provided by |
|------------------|------|-------------|
| Detector: keypoint overlay, octave filter | Keypoints (position, scale, angle, octave) | Phase 2 |
| Detector: contrast and edge sliders with survival histograms | Exploration run with permissive thresholds; per-keypoint `Contrast` and `EdgeRatio` | Phase 2 (data), Phase 6 (exploration run), Phase 8 (client filtering) |
| Detector: heatmaps (DoG, gradient, edge ratio, contrast) | Heatmap artifacts | Phase 2 (maps), Phase 6 (rendering) |
| Detector: keypoint inspector | Contrast, edge ratio, orientation histogram; descriptor; patch | Phase 2 (diagnostics), Phase 3 (descriptor), Phase 6 (per-keypoint endpoint) |
| Detector: run strip, timing waterfall | Stage events and durations | Phase 2 (observer), Phase 6 (SignalR) |
| Pipeline: pyramid and DoG tiles | Image layer artifacts | Phase 2 |
| Pipeline: kept and rejected per stage, funnel | Candidate set artifacts, stage summaries | Phase 2 |
| Pipeline: descriptor windows, "Show code" | Descriptor stage; one class per stage | Phase 3 |
| Matching | Matches, ratios, inlier flags, homography | Phase 4 |
| Robustness | Sweep curves (repeatability, matching score) | Phase 5 |
| Compare with OpenCV | Reference keypoints, repeatability, timings | Phase 3 (reference), Phase 6 (endpoint) |

## Parallel work

After Phase 1, three independent streams can run side by side:

| Stream | Phases | Touches |
|--------|--------|---------|
| Algorithms | 2 → 3 → 5 | `src/Salient.Features.Sift`, `src/Salient.Reference.OpenCv`, `src/Salient.Evaluation` |
| Matching | 4 | `src/Salient.Matching` |
| Frontend | 7 (against mocked API data, e.g. the mockup's precomputed JSON) | `web/` |

Phase 6 needs the contracts from Phases 2–5, and Phase 8 needs Phase 6.

## Out of scope for v1

- Run history and comparison between runs.
- Additional own detectors: Harris, FAST, and own implementations of ORB and AKAZE. The OpenCV references for ORB and AKAZE are in scope.
- Perspective transformations in the bench; panorama stitching.
- User accounts and multi-instance deployment.
