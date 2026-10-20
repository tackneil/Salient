# ADR-0012: Testing strategy

- Status: Accepted
- Date: 2026-10-20

## Context

The original coursework had several numerical bugs that produced plausible-looking but wrong output:
- the absolute value taken of the DoG;
- the contrast threshold applied in the wrong intensity scale;
- first derivatives off by a factor of two;
- an iteration bug in the sub-pixel refinement.

Bugs like these are invisible in screenshots. They are only caught by tests that check numbers.

## Decision

**Backend (xUnit):**
- **Unit tests** for math primitives: Gaussian kernel normalization, separable convolution against a naive 2-D reference, DoG sign, Hessian and its inverse, Taylor offset on synthetic quadratic surfaces, edge-ratio rejection on synthetic edges and corners.
- **Synthetic scenario tests:** a single Gaussian blob of known σ must yield one keypoint at the right position (±0.5 px) and scale (±10%). A straight edge must yield no keypoints after edge rejection.
- **Golden tests against OpenCV:** on images in `samples/`, Salient SIFT and OpenCV SIFT with equivalent parameters must reach:
  - keypoint repeatability ≥ 85% (match within 2 px and 20% scale);
  - descriptor matching that keeps an inlier ratio ≥ 90% on a known homography pair.

  These tests live in `Salient.Features.Sift.Tests`, reference `Salient.Reference.OpenCv` and are tagged `Category=Golden`.
- **Architecture test:** enforces the dependency rules from ADR-0002.
- **API tests:** `WebApplicationFactory` tests for the run lifecycle, cache hits, cancellation and problem details.

**Frontend:**
- Vitest and React Testing Library for components and hooks: the parameter form generated from a schema, the hit-testing grid, the zoom math.
- Playwright for two or three end-to-end flows: upload, run and inspect a keypoint; match a pair; run a short sweep.

**CI:**
- Every push: all backend tests, including golden tests (CI runs on Linux with OpenCV runtime binaries), plus Vitest.
- Pull requests to `master`: the Playwright suite as well.

## Consequences

- Algorithm changes are verified numerically, not by eye.
- Golden tests depend on OpenCV's behavior. An OpenCV upgrade can shift results, so thresholds are deliberately tolerant and the OpenCvSharp version is pinned.
