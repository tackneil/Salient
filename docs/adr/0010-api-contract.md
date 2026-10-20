# ADR-0010: API contract and client generation

- Status: Accepted
- Date: 2026-10-20

## Context

The frontend and the backend share many types: algorithm descriptors, parameter schemas, keypoints, run states and SignalR events. Maintaining them by hand on both sides leads to drift.

## Decision

- **REST:** ASP.NET Core minimal APIs, grouped by feature (`/api/images`, `/api/algorithms`, `/api/runs`, `/api/matches`, `/api/benchmarks`). The OpenAPI document is produced by the built-in `Microsoft.AspNetCore.OpenApi` and served at `/openapi/v1.json`.
- **TypeScript client:** generated from the OpenAPI document into `web/src/shared/api/generated/` by an npm script (`npm run api:generate`). Candidates are orval (typed TanStack Query hooks) and openapi-typescript with openapi-fetch; the choice is made when the frontend is scaffolded. Generated files are committed, so the frontend builds without a running backend.
- **SignalR:** a strongly typed hub (`Hub<IRunClient>`). The TypeScript types for hub methods and events are generated with TypedSignalR.Client.TypeScript, or written by hand if the generator does not fit the build. The event shapes are covered by a contract test either way.
- **Errors:** RFC 9457 problem details (`Results.Problem`, `ValidationProblem`) for every error response.
- **Versioning:** a single version (`v1`) for now. Breaking changes in a public deployment would introduce `v2` routes.

## Consequences

- A backend change that breaks the frontend fails at TypeScript compile time once the client is regenerated.
- CI must check that the committed client matches the current API: it regenerates the client and fails on any diff.
