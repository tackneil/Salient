# ADR-0013: Deployment

- Status: Accepted
- Date: 2026-10-20

## Context

Salient is a portfolio project. It needs to run locally with one command and be deployable as a public demo cheaply. The OpenCV reference adapters need native OpenCV libraries at runtime.

## Decision

- **Single container:** `Salient.Api` serves the API, the SignalR hub and the built SPA (`web/dist` copied to `wwwroot`). It uses a multi-stage Dockerfile:
  1. Node stage builds the frontend;
  2. SDK stage builds and publishes the API;
  3. a runtime stage based on the ASP.NET image for a Linux distribution that the OpenCvSharp runtime package supports.
- **Persistent data** (SQLite file, blobs) lives on a mounted volume at `/data`.
- **Local development:** `dotnet run` for the API with the Vite dev server proxying `/api` and `/hubs` to it. `docker compose up` runs the production-like container.
- **Public demo safeguards:**
  - upload size limit (10 MB) and pixel limit (ADR-0009);
  - ASP.NET Core rate limiting per IP for job creation;
  - a cap on concurrent jobs;
  - storage retention (ADR-0008).
- **OpenCV runtime package:** the image uses `OpenCvSharp4.official.runtime.linux-x64.slim`. The full package is built with OpenCV's GUI module and needs GTK, cairo and X11, none of which are in the ASP.NET image. The Windows runtime is referenced only when building on Windows. The resulting image is about 820 MB. *(Amended 2026-10-20 during Phase 9.)*
- **OpenCV is optional at runtime:** if the native library fails to load, the reference algorithms are not registered, and the UI hides "Compare with OpenCV" and the reference curves.
- **Hosting target:** decided later. Any container host with a persistent volume works, for example Azure Container Apps, Fly.io or a small VPS.

## Consequences

- One artifact to build, run and deploy. No CORS configuration in production, because everything is same-origin.
- The image is larger because of the OpenCV native libraries, roughly an extra 100 MB or more. That is acceptable for this project.
- Horizontal scaling is not supported: the queue is in-process and the storage is local. This is out of scope for v1.
