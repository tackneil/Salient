# ADR-0001: Record architecture decisions

- Status: Accepted
- Date: 2026-10-20

## Context

Salient grows out of a 2019 coursework project: a C++ SIFT detector. The new project adds a .NET backend, a React frontend and room for more feature-detection algorithms. Several decisions made up front, such as the algorithm contract, the execution model and the rendering approach, shape everything that follows. Their reasoning must stay available to anyone reading the code later.

## Decision

We record every significant architectural decision as a short Markdown file in `docs/adr/`, numbered sequentially, in the format of the template in `docs/adr/README.md`.

An accepted record is not rewritten. A changed decision gets a new record that supersedes the old one, and the old record's status is updated to point to it.

## Consequences

- The reasons behind the structure live next to the code and are versioned with it.
- Changing a recorded decision takes a deliberate step: writing a new ADR.
