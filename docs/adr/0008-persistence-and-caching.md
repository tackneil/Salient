# ADR-0008: Persistence and result caching

- Status: Accepted
- Date: 2026-10-20

## Context

Runs produce:
- metadata (algorithm, parameters, timings, counts);
- structured results (keypoints, matches, benchmark curves);
- binary artifacts (encoded image layers).

Users rerun the same image with the same parameters often, for example when switching between views. A comparison and run history are expected later.

## Decision

- **Metadata and structured results:** SQLite through EF Core.
  - Tables: `Images`, `Runs`, `RunStages`.
  - Large result payloads (keypoints, matches, curves) are stored as compressed JSON blobs on disk and referenced from `Runs`.
- **Binary artifacts:** stored on the local file system under a configurable root (`Storage:Root`), laid out as `images/{sha256}` and `runs/{runId}/layers/{layerId}.webp`.
- **Uploaded images** are identified by the SHA-256 of their decoded pixels plus their size, so re-uploads are deduplicated.
- **Cache key** for a run: `sha256(imageId + algorithmId + algorithmVersion + canonicalJson(parameters) + detailLevel)`. A job whose key already has a completed run returns that run.
  - `algorithmVersion` is a constant in each algorithm class. It is bumped whenever the output changes, which invalidates old cached results.
- **Retention:** a background cleanup removes runs and images older than a configurable age (default 7 days) and keeps total storage under a configurable cap.

## Consequences

- Zero external infrastructure: one file and one folder. Fits a single-container deployment (ADR-0013).
- Swapping SQLite for Postgres, or the file system for object storage, is possible behind the `IRunStore` and `IBlobStore` interfaces, but not planned.
- Forgetting to bump `algorithmVersion` serves stale results. A unit test asserts that the version changes whenever the golden output changes.
