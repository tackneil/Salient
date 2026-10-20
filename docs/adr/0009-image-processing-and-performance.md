# ADR-0009: Image processing and performance

- Status: Accepted
- Date: 2026-10-20

## Context

SIFT is dominated by Gaussian convolutions over float images. The coursework implementation used OpenCV for blurring and per-pixel `Mat::at` access. Salient implements the math itself, in managed code that must still be fast enough for interactive use.

## Decision

- **Image I/O:** SkiaSharp (MIT) decodes uploads (PNG, JPEG, WebP, GIF, BMP, with EXIF orientation) and encodes WebP layers. It is used only at the boundaries (in `Salient.Api`, behind `ImageCodec`), never inside algorithm code.
  - *Amended 2026-10-20 during Phase 6:* the original choice, SixLabors.ImageSharp, was dropped. Version 4.x needs a license key for Release builds, and 3.1.x has published high-severity vulnerabilities. Decoding untrusted uploads is exactly the attack surface those advisories cover. SkiaSharp needs a native library; `SkiaSharp.NativeAssets.Linux.NoDependencies` covers the Docker image.
- **In-memory representation:** `ImageF` in `Salient.Core`, a single-channel `float[]` with `Width`, `Height` and `Stride`. Intensities are normalized to [0, 1] on load. All thresholds follow Lowe's paper in that range.
- **Convolution:** separable Gaussian kernels with radius ⌈3σ⌉, mirrored borders, and incremental blurring (σ_inc = √(σ² − σ_prev²)).
- **Hot loops:**
  - `Span<float>` and `System.Numerics.Vector<float>` (SIMD) for row operations;
  - `Parallel.For` over rows for convolution and DoG;
  - array pooling (`ArrayPool<float>`) for temporary buffers.
- **Input limits:** images over 4 megapixels are downscaled on upload (configurable). The original size is kept in metadata, and coordinates are reported in the downscaled space.
- **Determinism:** no randomness in detection. RANSAC uses a seeded random generator, with the seed stored in the run.
- **Benchmarks:** BenchmarkDotNet suites for convolution, the pyramid and full detection guard against performance regressions.

## Consequences

- No native dependency in the core. The algorithm runs anywhere .NET runs.
- Writing SIMD code costs more than calling OpenCV, but the point of the project is to own the implementation.
- The target is under 500 ms for a full SIFT run on a 1 MP image on a typical laptop, without the pipeline artifacts. This target is re-evaluated after the first benchmarks.
