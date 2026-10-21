# ADR-0014: Project conventions

- Status: Accepted
- Date: 2026-10-20

## Context

Consistent conventions keep a solo portfolio project readable and make it easy for others to contribute or review.

## Decision

- **Language:** all code, identifiers, comments, UI text, commit messages and documentation are in English.
- **License:** MIT. Third-party licenses (SkiaSharp, OpenCV, OpenCvSharp, MUI) are listed in `THIRD-PARTY-NOTICES.md`.
- **Branching:** `main` is always releasable. (Dated 2026-10-21: the branch was renamed from `master`.) Work happens in short-lived branches merged through pull requests.
- **Commits:** Conventional Commits (`feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`), with an optional scope (`feat(sift): ...`).
- **CI:** none (dated 2026-10-21, see ADR-0016). The checks are run locally.
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
