# ADR-0014: Project conventions

- Status: Accepted
- Date: 2026-10-20

## Context

Consistent conventions keep a solo portfolio project readable and make it easy for others to contribute or review.

## Decision

- **Language:** all code, identifiers, comments, UI text, commit messages and documentation are in English.
- **License:** MIT. Third-party licenses (SkiaSharp, OpenCV, OpenCvSharp, MUI) are listed in `THIRD-PARTY-NOTICES.md`.
- **Branching:** `master` is always releasable. Work happens in short-lived branches merged through pull requests.
- **Commits:** Conventional Commits (`feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`), with an optional scope (`feat(sift): ...`).
- **CI:** GitHub Actions:
  - `dotnet build` with warnings as errors;
  - `dotnet test`;
  - `npm ci`, `npm run lint`, `npm run typecheck`, `npm test`, `npm run build`;
  - the API client drift check (ADR-0010);
  - Docker image build on `master`.
- **C# style:**
  - `.editorconfig` with nullable reference types enabled;
  - file-scoped namespaces;
  - `TreatWarningsAsErrors` in `Directory.Build.props`;
  - analyzers at the `latest-recommended` level.
- **TypeScript style:** `strict: true`, ESLint and Prettier, with no `any` outside generated code.
- **Repository name:** `salient`, or `salient-vision` if `salient` is taken on GitHub.

## Consequences

- A uniform history and style from the first commit.
- Warnings-as-errors and strict TypeScript slow the first steps slightly, and save time later.
