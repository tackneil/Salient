# ADR-0003: Feature algorithm contract and parameter schema

- Status: Accepted
- Date: 2026-10-20

## Context

Salient starts with one algorithm, its own SIFT, but must accept more detectors later: Harris, FAST, ORB, AKAZE, and OpenCV reference implementations. Adding an algorithm must not require changes to the frontend. The settings panel, the comparison dialog and the robustness bench must work with any registered algorithm.

## Decision

**Contracts** (in `Salient.Features`):

```csharp
public interface IFeatureAlgorithm
{
    AlgorithmDescriptor Descriptor { get; }   // id, display name, family, capabilities, parameter type
}

public interface IFeatureDetector<TParams> : IFeatureAlgorithm where TParams : class
{
    IReadOnlyList<Keypoint> Detect(ImageF image, TParams parameters, IPipelineObserver? observer, CancellationToken ct);
}

public interface IDescriptorExtractor<TParams> : IFeatureAlgorithm where TParams : class
{
    DescriptorSet Compute(ImageF image, IReadOnlyList<Keypoint> keypoints, TParams parameters, CancellationToken ct);
}
```

- An algorithm that both detects and describes, such as SIFT, implements both interfaces.
- `Capabilities` flags tell the UI what to offer: `Detects`, `Describes`, `ExposesPipeline` and `Reference`.

**Keypoint conventions:**
- `X` and `Y` are in pixels of the input image, measured from the top-left pixel center.
- `Scale` is the σ of the detection scale, in input-image pixels.
- `Angle` is in degrees, counterclockwise, in [0, 360).
- `Response` is the contrast |D(x̂)|, with intensities normalized to [0, 1].
- `Octave` and `Layer` are integers.
- Descriptors are stored as `float[]`, or `byte[]` for binary descriptors, with a declared `DescriptorKind` (`Float` or `Binary`). The matcher uses the kind to choose L2 or Hamming distance.

**Parameter schema:**
- Parameters are immutable `record` types annotated with attributes: `[Range(min, max)]`, `[Step]`, `[Display(Name, Description)]`, `[Group]` and `[Advanced]`.
- At startup the API builds a JSON Schema for each registered algorithm from these attributes. `GET /algorithms` exposes it.
- The frontend renders the settings panel from that schema: sliders for bounded numbers, switches for booleans, selects for enums.
- Defaults come from the record's default constructor values.

**Registration:** algorithms are registered in DI through an `AddFeatureAlgorithm<TAlgorithm>()` extension. The registry is the single source of truth for `GET /algorithms`.

## Consequences

- Adding an algorithm means adding one class, one parameter record and one registration line. The UI follows automatically.
- Clear coordinate and angle conventions prevent off-by-half-pixel and sign bugs when comparing implementations.
- Algorithm-specific visualizations, such as the SIFT descriptor rose plot, still need dedicated frontend components. They are chosen by `Capabilities` and the descriptor kind, not by algorithm id.
