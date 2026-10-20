# ADR-0003: Feature algorithm contract and parameter schema

- Status: Accepted (amended 2026-10-20, before implementation, to cover the data the design mockup needs)
- Date: 2026-10-20

## Context

Salient starts with one algorithm, its own SIFT, but must accept more detectors later: Harris, FAST, ORB, AKAZE, and OpenCV reference implementations. Adding an algorithm must not require changes to the frontend. The settings panel, the comparison dialog and the robustness bench must work with any registered algorithm.

The design mockup ("Salient UI") also requires:
- **Threshold sliders that filter keypoints instantly, without a new run.** Each slider has a histogram of how many keypoints survive at every value.
- **A keypoint inspector** showing contrast, edge ratio, the orientation histogram and the descriptor of a single keypoint.

## Decision

**Contracts** (in `Salient.Features`):

```csharp
public interface IFeatureAlgorithm
{
    AlgorithmDescriptor Descriptor { get; }   // id, display name, family, capabilities, parameter type, version
}

public interface IFeatureDetector<TParams> : IFeatureAlgorithm where TParams : class
{
    DetectionResult Detect(ImageF image, TParams parameters, DetectionOptions? options,
                           IPipelineObserver? observer, CancellationToken ct);
}

public interface IDescriptorExtractor<TParams> : IFeatureAlgorithm where TParams : class
{
    DescriptorSet Compute(ImageF image, IReadOnlyList<Keypoint> keypoints, TParams parameters, CancellationToken ct);
}

public sealed record DetectionResult(IReadOnlyList<Keypoint> Keypoints, IReadOnlyList<KeypointDiagnostics>? Diagnostics);
public sealed record DetectionOptions(bool IncludeDiagnostics);
```

- An algorithm that both detects and describes, such as SIFT, implements both interfaces.
- `Capabilities` flags tell the UI what to offer: `Detects`, `Describes`, `ExposesPipeline` and `Reference`.

**Keypoint conventions:**
- `X` and `Y` are in pixels of the input image, measured from the top-left pixel center, with Y pointing down.
- `Scale` is the σ of the detection scale, in input-image pixels.
- `Angle` is in degrees, counterclockwise as seen on screen, in [0, 360). OpenCV stores the clockwise angle; adapters convert it with `360 − angle`.
- `Response` is the detector's strength. For SIFT it is |D(x̂)|, with intensities normalized to [0, 1], the same as OpenCV's `response`.
- `Octave` and `Layer` are integers.
- Descriptors are stored as `float[]`, or `byte[]` for binary descriptors, with a declared `DescriptorKind` (`Float` or `Binary`). The matcher uses the kind to choose L2 or Hamming distance.

**Keypoint diagnostics:** an optional list aligned by index with `Keypoints`, returned when `IncludeDiagnostics` is set. Every field is nullable, so each algorithm fills only what it has.
- `Contrast` is expressed in the same units as the algorithm's contrast-threshold parameter. For SIFT that is |D(x̂)| · s, so a keypoint survives a threshold `t` exactly when `Contrast ≥ t`.
- `EdgeRatio` is tr²(H) / det(H) of the 2×2 Hessian, or +∞ when det(H) ≤ 0. A keypoint survives edge threshold `r` exactly when `EdgeRatio < (r + 1)² / r`.
- `OrientationHistogram` is the smoothed 36-bin histogram the orientation was taken from.

**Client-side exploration:**
- For interactive views the API runs the detector with the **most permissive** thresholds the UI sliders allow: the lower bound of `[Range]` for contrast and the upper bound for the edge threshold. It also requests diagnostics.
- The client then filters by `Contrast` and `EdgeRatio` as the sliders move and draws the survival histograms from the same data.
- Orientation is assigned to every candidate that passes these permissive thresholds, so filtered keypoints keep their orientation.
- Benchmarks and matching use the user's actual thresholds.

**Parameter schema:**
- Parameters are immutable `record` types annotated with attributes: `[Range(min, max)]`, `[Step]`, `[Display(Name, Description, GroupName)]` and `[Advanced]`.
- At startup the API builds a JSON Schema for each registered algorithm from these attributes. `GET /algorithms` exposes it.
- The frontend renders the settings panel from that schema: sliders for bounded numbers, switches for booleans, selects for enums.
- Defaults come from the record's property initializers.

**Registration:** algorithms are registered in DI through an `AddFeatureAlgorithm<TAlgorithm>()` extension. The registry is the single source of truth for `GET /algorithms`.

## Consequences

- Adding an algorithm means adding one class, one parameter record and one registration line. The UI follows automatically.
- Clear coordinate and angle conventions prevent off-by-half-pixel and sign bugs when comparing implementations.
- An exploration run returns more keypoints than a normal run, sometimes several times more. That is the cost of instant sliders, and it stays within the payload sizes in ADR-0006.
- Algorithm-specific visualizations, such as the SIFT descriptor rose plot, still need dedicated frontend components. They are chosen by `Capabilities`, the available diagnostics and the descriptor kind, not by algorithm id.
