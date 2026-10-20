# ADR-0004: Pipeline observation for step-by-step visualization

- Status: Accepted
- Date: 2026-10-20

## Context

The Pipeline view shows every stage of SIFT:
- the Gaussian pyramid and DoG images;
- the candidates kept and rejected by each filter;
- orientations and descriptors.

The live progress bar and the timing waterfall need per-stage events and durations. Collecting this data costs memory and time, and a normal detection run or a benchmark sweep must not pay that cost.

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

- Stage ids are stable strings declared by each algorithm: `load`, `gaussian-pyramid`, `dog`, `extrema`, `refine`, `contrast`, `edge`, `orientation`, `descriptor`. The UI uses them for labels and ordering.
- `StageSummary` carries counts: candidates in, kept and rejected.
- Pipeline artifacts are image layers (octave, layer, kind, an `ImageF` reference) or candidate sets (keypoints plus the rejection reason).
- With `Detail == Progress`, the algorithm reports only stage start and completion. This is the default for interactive runs.
- With `Detail == Full`, the algorithm also emits artifacts. This is used by the Pipeline view, and the run stores them (ADR-0008).
- With a `null` observer or `Detail == None`, the algorithm skips all reporting. This is used by benchmarks and tests.

## Consequences

- One implementation serves fast runs and fully instrumented runs. There is no separate "educational" copy of the algorithm.
- Each stage must be a clearly delimited step in the code. That is also how the "Show code" links in the UI work: every stage maps to one class.
- Image artifacts can be large. They are encoded and stored by the API layer, never held in the observer longer than needed.
