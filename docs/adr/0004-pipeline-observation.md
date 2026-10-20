# ADR-0004: Pipeline observation for step-by-step visualization

- Status: Accepted (amended 2026-10-20, before implementation, to add heatmap artifacts)
- Date: 2026-10-20

## Context

The Pipeline view shows every stage of SIFT:
- the Gaussian pyramid and DoG images;
- the candidates kept and rejected by each filter;
- orientations and descriptors.

The Detector view overlays heatmaps of the DoG response, gradient magnitude, contrast and edge ratio. The live progress bar and the timing waterfall need per-stage events and durations. Collecting this data costs memory and time, and a normal detection run or a benchmark sweep must not pay that cost.

## Decision

Algorithms accept an optional `IPipelineObserver`:

```csharp
public interface IPipelineObserver
{
    PipelineDetail Detail { get; }   // None, Progress, Full
    void StageStarted(string stageId);
    void StageCompleted(string stageId, TimeSpan elapsed, StageSummary summary);
    void Artifact(string stageId, PipelineArtifact artifact); // only called when Detail == Full
}
```

- Stage ids are stable strings declared by each algorithm. For SIFT they are `gaussian-pyramid`, `dog`, `extrema`, `refine`, `contrast`, `edge`, `orientation`, `descriptor` and `heatmaps`. The `heatmaps` stage runs only with `Detail == Full`. The API adds `load` for decoding the upload.
- `StageSummary` carries counts: candidates in, kept and rejected.
- Artifacts come in three kinds:
  - image layers: octave, layer, kind (Gaussian or DoG), σ and the image;
  - candidate sets: every candidate a stage looked at, with position, scale, contrast, edge ratio and rejection reason;
  - heatmaps: one map per kind (DoG response, gradient magnitude, contrast, edge ratio) at input resolution, taken from the octave whose scale matches the input and the layer with σ = σ₀ · k.
- With `Detail == Progress`, the algorithm reports only stage start and completion.
- With `Detail == Full`, the algorithm also emits artifacts. Interactive runs from the Detector and Pipeline views use `Full`, so one cached run (ADR-0008) serves both views.
- With a `null` observer or `Detail == None`, the algorithm skips all reporting. This is used by benchmarks and tests.

## Consequences

- One implementation serves fast runs and fully instrumented runs. There is no separate "educational" copy of the algorithm.
- Each stage must be a clearly delimited step in the code. That is also how the "Show code" links in the UI work: every stage maps to one class.
- Image artifacts can be large. They are encoded and stored by the API layer, never held in the observer longer than needed.
