# Salient

An interactive workbench for feature detection and matching: see every step of SIFT, inspect individual keypoints, match image pairs and measure how detectors hold up under rotation, scale, blur, noise and brightness changes.

Salient grows out of a 2019 bachelor's coursework project ([SIFTDetector](https://github.com/Xobnail/SIFTDetector)). It reimplements SIFT in C# and wraps it in a .NET API and a React + MUI frontend.

> Status: scaffolding (Phase 0). See [docs/implementation-plan.md](docs/implementation-plan.md) and the [architecture decisions](docs/adr/README.md).

## Stack

- **Backend:** .NET 10, ASP.NET Core minimal APIs, SignalR, EF Core + SQLite, SkiaSharp
- **Algorithms:** own SIFT implementation in C#; OpenCvSharp as an optional reference
- **Frontend:** React, TypeScript, Vite, MUI, MUI X Charts, TanStack Query, Zustand

## License

MIT
