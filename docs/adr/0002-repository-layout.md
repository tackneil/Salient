# ADR-0002: Repository layout and project boundaries

- Status: Accepted
- Date: 2026-10-20

## Context

Salient contains:
- numerical code (image math, SIFT);
- matching and evaluation logic;
- an optional native dependency, OpenCV, used as a reference;
- a web API and a single-page frontend.

The algorithm code must stay testable and reusable without the web host or the native OpenCV binaries.

## Decision

The repository is a monorepo with this layout:

```
salient/
├── src/
│   ├── Salient.Core/              # Image<T>, convolution, pyramids, geometry. No external dependencies.
│   ├── Salient.Features/          # Contracts: IFeatureDetector, IDescriptorExtractor, Keypoint, parameter schema.
│   ├── Salient.Features.Sift/     # Own SIFT implementation.
│   ├── Salient.Matching/          # Brute-force matcher, ratio test, RANSAC, homography.
│   ├── Salient.Evaluation/        # Robustness bench: transformations, repeatability, matching score.
│   ├── Salient.Reference.OpenCv/  # OpenCvSharp adapters (SIFT, ORB, AKAZE) behind the same contracts.
│   └── Salient.Api/               # ASP.NET Core host: REST, SignalR hub, job queue, storage, SPA hosting.
├── tests/                         # One test project per src project that has logic worth testing.
├── benchmarks/Salient.Benchmarks/ # BenchmarkDotNet.
├── web/                           # React + Vite + TypeScript + MUI.
├── samples/                       # Sample images for the gallery and for tests.
└── docs/adr/
```

Dependency rules:
- `Core` depends on nothing.
- `Features` depends on `Core`.
- `Features.Sift` and `Matching` depend only on `Core` and `Features`.
- `Evaluation` depends on `Core`, `Features` and `Matching`, because it measures matching score.
- `Reference.OpenCv` is the only project that references OpenCvSharp.
- `Api` composes everything.
- No project below `Api` references ASP.NET Core.

Build settings are shared through `Directory.Build.props`. Package versions are managed centrally in `Directory.Packages.props`. The SDK is pinned in `global.json` (.NET 10).

## Consequences

- The algorithm libraries build and test without native binaries or a web host.
- The OpenCV reference can be left out of a deployment by not registering it.
- More projects means more ceremony. Each new algorithm family gets its own `Salient.Features.<Name>` project only when it is large enough to justify one.
- An architecture test (NetArchTest or a simple reflection test) enforces the dependency rules.
