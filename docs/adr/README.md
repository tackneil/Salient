# Architecture Decision Records

This folder records the significant architectural decisions for Salient. Each record is short, immutable once accepted, and superseded by a new record rather than edited.

| #    | Decision                                                                  | Status   |
|------|---------------------------------------------------------------------------|----------|
| 0001 | [Record architecture decisions](0001-record-architecture-decisions.md)   | Accepted |
| 0002 | [Repository layout and project boundaries](0002-repository-layout.md)     | Accepted |
| 0003 | [Feature algorithm contract and parameter schema](0003-feature-algorithm-contract.md) | Accepted |
| 0004 | [Pipeline observation for step-by-step visualization](0004-pipeline-observation.md) | Accepted |
| 0005 | [Job-based execution with SignalR progress](0005-job-based-execution.md) | Accepted |
| 0006 | [Transport of images and feature data](0006-data-transport.md)            | Accepted |
| 0007 | [Canvas-based rendering of keypoints and matches](0007-canvas-rendering.md) | Accepted |
| 0008 | [Persistence and result caching](0008-persistence-and-caching.md)         | Accepted |
| 0009 | [Image processing and performance](0009-image-processing-and-performance.md) | Accepted |
| 0010 | [API contract and client generation](0010-api-contract.md)                | Accepted |
| 0011 | [Frontend stack](0011-frontend-stack.md)                                  | Accepted |
| 0012 | [Testing strategy](0012-testing-strategy.md)                              | Accepted |
| 0013 | [Deployment](0013-deployment.md)                                          | Accepted |
| 0014 | [Project conventions](0014-project-conventions.md)                        | Accepted |
| 0015 | [Every view fits one viewport](0015-single-viewport-layout.md)            | Accepted |
| 0016 | [No CI](0016-no-ci.md)                                                    | Accepted |

## Template

```markdown
# ADR-NNNN: Title

- Status: Proposed | Accepted | Superseded by ADR-XXXX
- Date: YYYY-MM-DD

## Context
What problem are we solving, and what constraints apply?

## Decision
What we will do.

## Consequences
What becomes easier, what becomes harder, and what we must watch.
```
