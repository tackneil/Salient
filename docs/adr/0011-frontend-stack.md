# ADR-0011: Frontend stack

- Status: Accepted
- Date: 2026-10-20

## Context

The frontend is the main showcase of the project. It needs a polished, consistent look, complex interactive views (image stage, charts, step-through pipeline) and a clean split between server data and UI state.

## Decision

| Concern | Choice |
|---------|--------|
| Language and build | TypeScript (strict), Vite |
| UI kit | MUI (Material UI) with a custom dark-first theme matching the design mockup |
| Charts | MUI X Charts (MIT community edition) |
| Server state | TanStack Query (via the generated client, ADR-0010) |
| UI state | Zustand: selected keypoint, overlay, filters, zoom |
| Routing | React Router: `/detector`, `/pipeline/:runId`, `/matching`, `/robustness` |
| Realtime | `@microsoft/signalr` wrapped in a hook that feeds TanStack Query caches |
| Fonts | Inter for UI text, JetBrains Mono for numbers |
| Lint and format | ESLint (typescript-eslint) and Prettier |

Folder structure inside `web/src`:

```
app/        # providers, theme, router, layout (app bar, navigation)
features/   # detector, pipeline, matching, robustness, compare: each owns its components, hooks and store slice
shared/     # api (generated + wrappers), components (ImageStage, KeypointCanvas, HeatmapLegend, ParameterForm), utils
```

The `ParameterForm` component renders algorithm settings from the JSON Schema in ADR-0003.

## Consequences

- One component library and one chart library keep the look consistent.
- The design mockup (canvas artifact "Salient UI") is the visual spec. Colors and typography from it become MUI theme tokens.
- The frontend targets Node.js 24 (Active LTS as of October 2026). It is pinned in `web/.nvmrc`, which CI reads.
- Linting uses ESLint with typescript-eslint (`strictTypeChecked`), not the oxlint setup that create-vite 9 generates by default. Type-aware rules matter more here than lint speed.
