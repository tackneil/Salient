# ADR-0005: Job-based execution with SignalR progress

- Status: Accepted
- Date: 2026-10-20

## Context

A detection run on a typical image takes hundreds of milliseconds. A full pipeline run with artifacts takes longer, and a robustness sweep (several transformations × several values × several detectors) takes tens of seconds. The UI shows live, stage-by-stage progress and must allow cancellation.

## Decision

All computations are **jobs**:

1. `POST /api/runs` (detection), `POST /api/matches` (pair matching) or `POST /api/benchmarks` (robustness sweep) validates the input, stores the job and immediately returns `202 Accepted` with a `runId`.
2. A `BackgroundService` reads jobs from a bounded `Channel<Job>` and executes them. The degree of parallelism is configurable; the default is the number of cores divided by 2.
3. Progress is pushed through a SignalR hub, `/hubs/runs`. Clients join a group per `runId` and receive typed events:
   - `StageStarted`
   - `StageCompleted` (with duration and counts)
   - `RunCompleted`
   - `RunFailed`
4. Results are fetched with `GET /api/runs/{runId}` and related resource endpoints (ADR-0006).
5. `DELETE /api/runs/{runId}` cancels a queued or running job through its `CancellationToken`.

A result that is already cached (ADR-0008) completes immediately. The client still receives `RunCompleted`, so there is a single code path.

## Consequences

- Long and short operations share one model. The UI never blocks on an HTTP request.
- The client must handle the race in which a run completes before it has subscribed. On subscribe, the hub replays the current state of the run.
- The in-process queue is lost on restart. That is acceptable for v1: jobs are short-lived and can be resubmitted. A durable queue is out of scope.
