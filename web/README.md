# Salient web

React + TypeScript + Vite frontend for Salient, built with MUI. See [ADR-0011](../docs/adr/0011-frontend-stack.md) for the stack.

## Requirements

Node.js 24 (see `.nvmrc`).

## Scripts

| Command                                   | Purpose                                                                                                                    |
| ----------------------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| `npm run dev`                             | Dev server; proxies `/api`, `/hubs` and `/health` to the API at `http://localhost:5139` (override with `SALIENT_API_URL`). |
| `npm run build`                           | Type-check and build to `dist/`.                                                                                           |
| `npm run typecheck`                       | Type-check only.                                                                                                           |
| `npm run lint`                            | ESLint with type-aware rules.                                                                                              |
| `npm run format` / `npm run format:check` | Prettier.                                                                                                                  |
